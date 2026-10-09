using System.Security.Cryptography;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

public sealed class VerifiedInstallerDownloader(
    string directory, ClientReleaseVerifier verifier, HttpMessageHandler? handler = null, TimeSpan? bodyIdleTimeout = null) : IDisposable
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly AnonymousUpdateTransport _transport = new(handler);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _bodyIdleTimeout = bodyIdleTimeout ?? TimeSpan.FromSeconds(30);

    public async Task<string> DownloadAsync(SignedClientRelease envelope, bool rollback,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var manifest = verifier.Verify(envelope);
        var artifact = rollback ? manifest.Release.RollbackInstaller
            ?? throw new ClientReleaseRejectedException("rollback_missing") : manifest.Release.Installer;
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(_directory);
            if (new DirectoryInfo(_directory).LinkTarget is not null) throw new ClientReleaseRejectedException("path");
            var target = Path.Combine(_directory, "VideoGrabber-Setup-" + artifact.Sha256.ToLowerInvariant() + ".exe");
            if (File.Exists(target) && await MatchesAsync(target, artifact, cancellationToken).ConfigureAwait(false))
            { progress?.Report(1); return target; }
            using var response = await _transport.OpenAsync(artifact.Url, cancellationToken).ConfigureAwait(false);
            using var totalDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            totalDeadline.CancelAfter(TimeSpan.FromMinutes(30));
            cancellationToken = totalDeadline.Token;
            if (response.Content.Headers.ContentLength is { } expected && expected != artifact.SizeBytes)
                throw new ClientReleaseRejectedException("size");
            temporary = Path.Combine(_directory, "." + Guid.NewGuid().ToString("N") + ".part");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var chunk = new byte[65536];
                long size = 0;
                int count;
                while ((count = await ReadWithDeadlineAsync(source, chunk, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    size = checked(size + count);
                    if (size > artifact.SizeBytes) throw new ClientReleaseRejectedException("size");
                    hash.AppendData(chunk, 0, count);
                    await output.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    progress?.Report((double)size / artifact.SizeBytes);
                }
                if (size != artifact.SizeBytes) throw new ClientReleaseRejectedException("size");
                if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(artifact.Sha256)))
                    throw new ClientReleaseRejectedException("hash");
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (!await MatchesAsync(temporary, artifact, cancellationToken).ConfigureAwait(false))
                throw new ClientReleaseRejectedException("package");
            File.Move(temporary, target, overwrite: true);
            temporary = null;
            progress?.Report(1);
            return target;
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
            _gate.Release();
        }
    }

    private static async Task<bool> MatchesAsync(string path, ClientUpdateArtifact artifact, CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length != artifact.SizeBytes || file.LinkTarget is not null) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        var header = new byte[2];
        if (await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false) != 2 || header[0] != 'M' || header[1] != 'Z') return false;
        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(artifact.Sha256));
    }

    private async Task<int> ReadWithDeadlineAsync(Stream source, byte[] chunk, CancellationToken cancellationToken)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(_bodyIdleTimeout);
        return await source.ReadAsync(chunk, idle.Token).ConfigureAwait(false);
    }

    public void Dispose() { _transport.Dispose(); _gate.Dispose(); }
}
