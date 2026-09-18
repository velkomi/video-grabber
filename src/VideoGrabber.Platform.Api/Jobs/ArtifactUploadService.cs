using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public sealed class ArtifactUploadService : IAsyncDisposable
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly JobStore _jobs;
    private readonly string _root;
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private readonly long _maximumBytes;

    public ArtifactUploadService(
        IConfiguration configuration,
        TimeProvider clock,
        JobStore jobs)
    {
        var dsn = configuration.GetConnectionString("PlatformLedger")
            ?? configuration["VG_PLATFORM_LEDGER_DSN"]
            ?? throw new InvalidOperationException("Platform ledger DSN is required.");
        _dataSource = NpgsqlDataSource.Create(dsn);
        _clock = clock;
        _jobs = jobs;
        _root = Path.GetFullPath(configuration["VG_ARTIFACT_UPLOAD_ROOT"]
            ?? Path.Combine(AppContext.BaseDirectory, "artifact-uploads"));
        _ffmpeg = configuration["VG_FFMPEG_PATH"] ?? "ffmpeg";
        _ffprobe = configuration["VG_FFPROBE_PATH"] ?? "ffprobe";
        _maximumBytes = long.TryParse(configuration["VG_ARTIFACT_UPLOAD_MAX_BYTES"], out var max)
            && max is > 0 and <= 16L * 1024 * 1024 * 1024
                ? max
                : 4L * 1024 * 1024 * 1024;
    }

    public async Task<UploadTicket> CreateTicketAsync(
        Guid accountId,
        Guid deviceId,
        UploadTicketRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTicketRequest(request);
        if (!await _jobs.ValidateDesktopAttemptAsync(
                accountId, deviceId, request.Lease, cancellationToken))
            throw new UnauthorizedAccessException("desktop_attempt_scope_mismatch");
        if (request.Length > _maximumBytes)
            throw new InvalidDataException("artifact_too_large");

        var uploadId = Guid.NewGuid();
        var expires = _clock.GetUtcNow().Add(TicketLifetime);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.artifact_uploads(
              upload_id,account_id,device_id,job_id,attempt_id,fence,
              declared_length,declared_sha256,media_type,expires_at,state,created_at)
            values(@upload,@account,@device,@job,@attempt,@fence,
              @length,@sha,@media,@expires,'pending',@now)
            """, connection);
        command.Parameters.AddWithValue("upload", uploadId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("job", request.Lease.JobId);
        command.Parameters.AddWithValue("attempt", request.Lease.AttemptId);
        command.Parameters.AddWithValue("fence", request.Lease.Fence);
        command.Parameters.AddWithValue("length", request.Length);
        command.Parameters.AddWithValue("sha", request.Sha256.ToLowerInvariant());
        command.Parameters.AddWithValue("media", request.MediaType);
        command.Parameters.AddWithValue("expires", expires);
        command.Parameters.AddWithValue("now", _clock.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new UploadTicket(
            uploadId, request.Lease.JobId, request.Lease.AttemptId,
            request.Lease.Fence, _maximumBytes, expires);
    }

    public async Task<ArtifactReceipt> UploadAsync(
        Guid accountId,
        Guid deviceId,
        Guid uploadId,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength <= 0 || contentLength > _maximumBytes)
            throw new InvalidDataException("artifact_length_invalid");
        var row = await ClaimUploadAsync(
            accountId, deviceId, uploadId, contentLength, cancellationToken)
            ?? throw new KeyNotFoundException("Upload ticket was not found.");

        var directory = Path.Combine(
            _root,
            accountId.ToString("N"),
            row.JobId.ToString("N"),
            row.AttemptId.ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, uploadId.ToString("N") + ".upload");
        if (!IsWithin(_root, path))
            throw new UnauthorizedAccessException("upload_path_escape");

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            long total = 0;
            await using (var output = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > row.DeclaredLength || total > _maximumBytes)
                        throw new InvalidDataException("artifact_length_exceeded");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                }
                await output.FlushAsync(cancellationToken);
            }
            if (total != row.DeclaredLength || total != contentLength)
                throw new InvalidDataException("artifact_length_mismatch");
            var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(digest),
                    Encoding.ASCII.GetBytes(row.DeclaredSha256)))
                throw new InvalidDataException("artifact_digest_mismatch");

            await VerifyAsync(path, row.MediaType, cancellationToken);
            await MarkUploadedAsync(uploadId, path, cancellationToken);
            return new ArtifactReceipt(
                uploadId, digest, total, row.MediaType,
                "desktop-upload-" + uploadId.ToString("N"),
                path);
        }
        catch
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            await MarkFailedAsync(uploadId, CancellationToken.None);
            throw;
        }
    }

    public async Task<ArtifactReceipt> ValidateReceiptAsync(
        Guid accountId,
        Guid deviceId,
        AttemptLease lease,
        ArtifactReceipt supplied,
        CancellationToken cancellationToken)
    {
        if (!await _jobs.ValidateDesktopAttemptAsync(
                accountId, deviceId, lease, cancellationToken))
            throw new UnauthorizedAccessException("desktop_attempt_scope_mismatch");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select declared_length,declared_sha256,media_type,server_path
            from licensing.artifact_uploads
            where upload_id=@upload and account_id=@account and device_id=@device
              and job_id=@job and attempt_id=@attempt and fence=@fence
              and state='uploaded' and server_path is not null
            """, connection);
        command.Parameters.AddWithValue("upload", supplied.ArtifactId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("job", lease.JobId);
        command.Parameters.AddWithValue("attempt", lease.AttemptId);
        command.Parameters.AddWithValue("fence", lease.Fence);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new KeyNotFoundException("Verified upload was not found.");
        var length = reader.GetInt64(0);
        var sha = reader.GetString(1);
        var media = reader.GetString(2);
        var path = reader.GetString(3);
        if (supplied.Bytes != length
            || !string.Equals(supplied.MediaType, media, StringComparison.OrdinalIgnoreCase)
            || !FixedAsciiEquals(supplied.Sha256, sha))
            throw new UnauthorizedAccessException("artifact_receipt_mismatch");
        return new ArtifactReceipt(
            supplied.ArtifactId, sha, length, media,
            "desktop-upload-" + supplied.ArtifactId.ToString("N"), path);
    }

    private static bool FixedAsciiEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        var a = Encoding.ASCII.GetBytes(left.ToLowerInvariant());
        var b = Encoding.ASCII.GetBytes(right.ToLowerInvariant());
        try { return CryptographicOperations.FixedTimeEquals(a, b); }
        finally
        {
            CryptographicOperations.ZeroMemory(a);
            CryptographicOperations.ZeroMemory(b);
        }
    }
    private async Task<UploadRow?> ClaimUploadAsync(
        Guid accountId,
        Guid deviceId,
        Guid uploadId,
        long contentLength,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.artifact_uploads
            set state='receiving'
            where upload_id=@upload and account_id=@account and device_id=@device
              and state='pending' and expires_at>@now
              and declared_length=@length
            returning job_id,attempt_id,fence,declared_length,declared_sha256,media_type
            """, connection);
        command.Parameters.AddWithValue("upload", uploadId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("length", contentLength);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new UploadRow(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetString(4), reader.GetString(5));
    }

    private async Task VerifyAsync(
        string path,
        string mediaType,
        CancellationToken cancellationToken)
    {
        if (mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            var probe = await RunAsync(
                _ffprobe,
                ["-v","error","-show_streams","-show_format","-of","json","--",path],
                TimeSpan.FromMinutes(1),
                cancellationToken);
            if (probe != 0) throw new InvalidDataException("uploaded_media_probe_failed");
            var decode = await RunAsync(
                _ffmpeg,
                ["-v","error","-xerror","-i",path,"-f","null","-"],
                TimeSpan.FromHours(2),
                cancellationToken);
            if (decode != 0) throw new InvalidDataException("uploaded_media_decode_failed");
            return;
        }

        if (string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "application/x-subrip", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (bytes.Length == 0) throw new InvalidDataException("uploaded_text_empty");
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return;
        }

        throw new InvalidDataException("uploaded_media_type_rejected");
    }

    private static async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Verifier tool could not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try { await process.WaitForExitAsync(deadline.Token); }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return process.ExitCode;
    }

    private async Task MarkUploadedAsync(
        Guid uploadId,
        string path,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.artifact_uploads
            set state='uploaded',server_path=@path,uploaded_at=@now
            where upload_id=@upload and state='receiving'
            """, connection);
        command.Parameters.AddWithValue("path", path);
        command.Parameters.AddWithValue("now", _clock.GetUtcNow());
        command.Parameters.AddWithValue("upload", uploadId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Upload state transition failed.");
    }

    private async Task MarkFailedAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.artifact_uploads
            set state='failed'
            where upload_id=@upload and state='receiving'
            """, connection);
        command.Parameters.AddWithValue("upload", uploadId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsWithin(string root, string path)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(
            prefix,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static void ValidateTicketRequest(UploadTicketRequest request)
    {
        if (request.Length <= 0
            || request.Sha256.Length != 64
            || request.Sha256.Any(ch => !char.IsAsciiHexDigit(ch))
            || string.IsNullOrWhiteSpace(request.MediaType)
            || request.MediaType.Length > 128)
            throw new ArgumentException("Upload ticket request is invalid.");
    }

    public async ValueTask DisposeAsync()
        => await _dataSource.DisposeAsync();

    private sealed record UploadRow(
        Guid JobId,
        Guid AttemptId,
        long Fence,
        long DeclaredLength,
        string DeclaredSha256,
        string MediaType);
}
