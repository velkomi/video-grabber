using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Worker;

public sealed class ArtifactRetentionWorker(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<IReadOnlyList<RetentionCleanupResult>> CleanupAsync(
        IReadOnlyList<RetentionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var results = new List<RetentionCleanupResult>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!candidate.Tombstoned)
            {
                results.Add(new(candidate.ArtifactId, false, "not_tombstoned"));
                continue;
            }
            if (candidate.ActiveDeliveryOrReaderLease)
            {
                results.Add(new(candidate.ArtifactId, false, "active_delivery_or_reader"));
                continue;
            }
            if (candidate.RetainedUntil > _clock.GetUtcNow())
            {
                results.Add(new(candidate.ArtifactId, false, "retention_not_expired"));
                continue;
            }

            string root;
            string path;
            try
            {
                root = Path.GetFullPath(candidate.OwnedRoot);
                path = Path.GetFullPath(candidate.Path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                results.Add(new(candidate.ArtifactId, false, "invalid_path"));
                continue;
            }

            if (!File.Exists(path))
            {
                results.Add(new(candidate.ArtifactId, false, "already_missing"));
                continue;
            }

            var attributes = File.GetAttributes(path);
            if (!IsSafeOwnedPath(root, path, attributes))
            {
                results.Add(new(candidate.ArtifactId, false, "unsafe_path"));
                continue;
            }

            try
            {
                File.Delete(path);
                await RemoveEmptyParentsAsync(root, Path.GetDirectoryName(path), cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new(candidate.ArtifactId, true, "deleted"));
            }
            catch (UnauthorizedAccessException)
            {
                results.Add(new(candidate.ArtifactId, false, "permission_denied"));
            }
            catch (IOException)
            {
                results.Add(new(candidate.ArtifactId, false, "io_failure"));
            }
        }
        return results;
    }

    public static bool IsSafeOwnedPath(
        string ownedRoot,
        string path,
        FileAttributes attributes)
    {
        var root = Path.GetFullPath(ownedRoot);
        var full = Path.GetFullPath(path);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return full.StartsWith(prefix, comparison)
            && !string.Equals(full, root, comparison)
            && (attributes & FileAttributes.ReparsePoint) == 0
            && (attributes & FileAttributes.Directory) == 0;
    }

    private static async Task RemoveEmptyParentsAsync(
        string root,
        string? directory,
        CancellationToken cancellationToken)
    {
        var rootFull = Path.GetFullPath(root);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = Path.GetFullPath(directory);
            if (string.Equals(
                    current,
                    rootFull,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
                return;
            if (!Directory.Exists(current)
                || Directory.EnumerateFileSystemEntries(current).Any())
                return;
            Directory.Delete(current);
            directory = Path.GetDirectoryName(current);
            await Task.Yield();
        }
    }
}

public sealed class ArtifactRetentionHostedService(
    WorkerApiClient api,
    ArtifactRetentionWorker retention,
    IConfiguration configuration,
    ILogger<ArtifactRetentionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(configuration["VG_RETENTION_ENABLED"], "true", StringComparison.OrdinalIgnoreCase))
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var candidate = await api.ClaimRetentionAsync(stoppingToken).ConfigureAwait(false);
                if (candidate is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
                    continue;
                }
                var result = AssertSingle(await retention.CleanupAsync(
                    [candidate], stoppingToken).ConfigureAwait(false));
                await api.AcknowledgeRetentionAsync(result, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Retention worker iteration failed: {ExceptionType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static RetentionCleanupResult AssertSingle(IReadOnlyList<RetentionCleanupResult> results)
        => results.Count == 1
            ? results[0]
            : throw new InvalidDataException("Retention worker returned unexpected result count.");
}