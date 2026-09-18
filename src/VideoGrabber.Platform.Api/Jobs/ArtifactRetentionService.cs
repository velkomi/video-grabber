using System.Data;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Api.Operations;

namespace VideoGrabber.Platform.Api.Jobs;

public sealed class ArtifactRetentionService(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    TimeProvider clock,
    PlatformOperationalCounters counters)
{
    private readonly string? _root = NormalizeRoot(configuration["VG_RETENTION_ROOT"]);
    private readonly bool _enabled = ActivationQualified(
        configuration, clock.GetUtcNow());

    public async Task<RetentionCandidate?> ClaimExpiredAsync(
        CancellationToken cancellationToken)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_root))
            return null;
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        await using var select = new NpgsqlCommand("""
            select a.artifact_id,a.storage_path,a.retained_until
            from licensing.artifacts a
            where a.retained_until is not null
              and a.retained_until<=@now
              and a.expired_at is null
              and a.storage_path is not null
              and not exists(
                select 1 from licensing.delivery_attempts d
                where d.artifact_id=a.artifact_id
                  and d.state in ('pending','sending','delivery_unknown'))
            order by a.retained_until,a.artifact_id
            for update of a skip locked
            limit 1
            """, connection, transaction);
        select.Parameters.AddWithValue("now", now);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var artifactId = reader.GetGuid(0);
        var path = reader.GetString(1);
        var retainedUntil = reader.GetFieldValue<DateTimeOffset>(2);
        await reader.DisposeAsync();

        if (!IsOwnedPath(_root, path))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await using var update = new NpgsqlCommand("""
            update licensing.artifacts
            set expired_at=@now,unavailable_reason='retention_expired'
            where artifact_id=@artifact and expired_at is null
            """, connection, transaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("artifact", artifactId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new RetentionCandidate(
            artifactId,
            _root,
            path,
            retainedUntil,
            Tombstoned: true,
            ActiveDeliveryOrReaderLease: false);
    }

    public async Task AcknowledgeAsync(
        RetentionCleanupResult result,
        CancellationToken cancellationToken)
    {
        var reason = result.Deleted
            ? "retention_deleted"
            : "retention_cleanup_" + SafeReason(result.Reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.artifacts
            set unavailable_reason=@reason
            where artifact_id=@artifact and expired_at is not null
            """, connection);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("artifact", result.ArtifactId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        if (!result.Deleted
            && !string.Equals(result.Reason, "already_missing", StringComparison.Ordinal))
            counters.RecordRetentionCleanupFailure();
    }

    public async Task<IReadOnlyList<RetentionCandidate>> DryRunAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_root)) return [];
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select a.artifact_id,a.storage_path,a.retained_until,
                   exists(
                     select 1 from licensing.delivery_attempts d
                     where d.artifact_id=a.artifact_id
                       and d.state in ('pending','sending','delivery_unknown')) as active
            from licensing.artifacts a
            where a.retained_until is not null
              and a.retained_until<=@now
              and a.expired_at is null
              and a.storage_path is not null
            order by a.retained_until,a.artifact_id
            limit 100
            """, connection);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<RetentionCandidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(1);
            if (!IsOwnedPath(_root, path)) continue;
            result.Add(new RetentionCandidate(
                reader.GetGuid(0),
                _root,
                path,
                reader.GetFieldValue<DateTimeOffset>(2),
                Tombstoned: false,
                ActiveDeliveryOrReaderLease: reader.GetBoolean(3)));
        }
        return result;
    }

    private static bool ActivationQualified(
        IConfiguration configuration,
        DateTimeOffset now)
    {
        var root = NormalizeRoot(configuration["VG_RETENTION_ROOT"]);
        var ownedRootQualified = IsQualifiedOwnedRoot(root);
        var enabled = string.Equals(
            configuration["VG_RETENTION_ENABLED"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var qualified = string.Equals(
            configuration["VG_RETENTION_QUALIFIED"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var clean = string.Equals(
            configuration["VG_RETENTION_DRY_RUN_CLEAN"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var approved = string.Equals(
            configuration["VG_RETENTION_APPROVED"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var dryRunFresh = TryRecent(
            configuration["VG_RETENTION_DRY_RUN_UTC"],
            now,
            TimeSpan.FromHours(24));
        var backupCurrent = TryRecent(
            configuration["VG_LAST_BACKUP_UTC"],
            now,
            TimeSpan.FromMinutes(15));
        return PlatformMetrics.RetentionMayRun(
            enabled,
            qualified && ownedRootQualified,
            clean && dryRunFresh,
            backupCurrent,
            approved);
    }

    private static bool IsQualifiedOwnedRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        var full = Path.GetFullPath(root);
        var driveRoot = Path.GetPathRoot(full);
        if (string.Equals(
                full.TrimEnd(Path.DirectorySeparatorChar),
                driveRoot?.TrimEnd(Path.DirectorySeparatorChar),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
            return false;
        return true;
    }

    private static bool TryRecent(
        string? raw,
        DateTimeOffset now,
        TimeSpan maximumAge)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTimeOffset.TryParse(
                raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var timestamp))
            return false;
        var age = now - timestamp;
        return age >= TimeSpan.Zero && age <= maximumAge;
    }
    private static string? NormalizeRoot(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Path.GetFullPath(raw);
    }

    private static bool IsOwnedPath(string root, string path)
    {
        try
        {
            var rootFull = Path.GetFullPath(root);
            var full = Path.GetFullPath(path);
            var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return full.StartsWith(prefix, comparison)
                && !string.Equals(full, rootFull, comparison);
        }
        catch
        {
            return false;
        }
    }

    private static string SafeReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var safe = new string(value.ToLowerInvariant()
            .Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')
            .Take(48).ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }
}
