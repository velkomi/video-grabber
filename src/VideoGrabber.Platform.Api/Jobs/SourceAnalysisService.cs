using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Jobs;

public sealed class SourceAnalysisService : IAsyncDisposable
{
    private const int MaxMetadataBytes = 4 * 1024 * 1024;
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly EgressProxy _egress;
    private readonly byte[] _key;
    private readonly string _ytDlp;
    private readonly string _denoPath;
    private readonly Uri _proxyUri;
    private readonly Uri? _socialProxyUri;
    private readonly Uri? _youtubePotProviderUri;

    public SourceAnalysisService(
        IConfiguration configuration,
        TimeProvider clock,
        EgressProxy egress)
    {
        var dsn = configuration.GetConnectionString("PlatformLedger")
            ?? configuration["VG_PLATFORM_LEDGER_DSN"]
            ?? throw new InvalidOperationException("Platform ledger DSN is required.");
        _dataSource = NpgsqlDataSource.Create(dsn);
        _clock = clock;
        _egress = egress;
        _key = ReadKey(configuration["VG_SOURCE_ENCRYPTION_KEY"]);
        _ytDlp = string.IsNullOrWhiteSpace(configuration["VG_YTDLP_PATH"])
            ? "yt-dlp"
            : configuration["VG_YTDLP_PATH"]!.Trim();
        _denoPath = string.IsNullOrWhiteSpace(configuration["VG_DENO_PATH"])
            ? "/usr/local/bin/deno"
            : configuration["VG_DENO_PATH"]!.Trim();
        var potProvider = configuration["VG_YOUTUBE_POT_PROVIDER_URL"];
        if (!string.IsNullOrWhiteSpace(potProvider))
        {
            if (!Uri.TryCreate(potProvider, UriKind.Absolute, out var parsedPot)
                || parsedPot.Scheme is not ("http" or "https")
                || !string.IsNullOrEmpty(parsedPot.UserInfo))
                throw new InvalidOperationException("YouTube PO-token provider URL is invalid.");
            _youtubePotProviderUri = parsedPot;
        }
        var proxy = configuration["VG_EGRESS_PROXY_URI"];
        if (!Uri.TryCreate(proxy, UriKind.Absolute, out var parsedProxy)
            || parsedProxy.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(parsedProxy.UserInfo))
            throw new InvalidOperationException("Validated worker egress proxy URI is required.");
        _proxyUri = parsedProxy;

        var socialProxy = configuration["VG_SOCIAL_EGRESS_PROXY_URI"];
        if (!string.IsNullOrWhiteSpace(socialProxy))
        {
            if (!Uri.TryCreate(socialProxy, UriKind.Absolute, out var parsedSocial)
                || parsedSocial.Scheme is not ("http" or "https" or "socks5" or "socks5h")
                || !string.IsNullOrEmpty(parsedSocial.UserInfo))
                throw new InvalidOperationException("Social egress proxy URI is invalid.");
            _socialProxyUri = parsedSocial;
        }
    }

    public async Task<AnalyzedMedia[]> AnalyzeAsync(
        Guid accountId,
        Uri source,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account is required.", nameof(accountId));
        await _egress.ValidatePublicTargetAsync(source, cancellationToken).ConfigureAwait(false);
        var metadata = await ProbeAsync(source, cancellationToken).ConfigureAwait(false);
        var mediaId = GetString(metadata, "id") ?? Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.AbsoluteUri))).ToLowerInvariant()[..24];
        var title = GetString(metadata, "title") ?? mediaId;
        if (title.Length > 200) title = title[..200];
        long? durationMs = null;
        if (metadata.TryGetProperty("duration", out var duration)
            && duration.ValueKind == JsonValueKind.Number
            && duration.TryGetDouble(out var seconds)
            && seconds >= 0 && seconds <= 24 * 60 * 60)
            durationMs = checked((long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero));

        var qualities = ReadQualities(metadata);
        var sourceId = "src_" + Guid.NewGuid().ToString("N");
        var expires = _clock.GetUtcNow().AddMinutes(30);
        var cipher = Encrypt(source.AbsoluteUri);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.sources(
              source_id,account_id,media_id,source_cipher,qualities,expires_at)
            values(@source,@account,@media,@cipher,@qualities::jsonb,@expires)
            """, connection);
        command.Parameters.AddWithValue("source", sourceId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("media", mediaId);
        command.Parameters.AddWithValue("cipher", cipher);
        command.Parameters.AddWithValue("qualities", JsonSerializer.Serialize(qualities));
        command.Parameters.AddWithValue("expires", expires);
        await command.ExecuteNonQueryAsync(cancellationToken);
        CryptographicOperations.ZeroMemory(cipher);
        return [new AnalyzedMedia(
            sourceId, mediaId, title, durationMs, qualities, "server_worker")];
    }

    public async Task<AnalyzedMedia> RegisterDesktopAsync(
        Guid accountId,
        RegisterDesktopSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account is required.", nameof(accountId));
        ArgumentNullException.ThrowIfNull(request);
        if (!IsDesktopQuality(request.Quality))
            throw new ArgumentException("Quality is invalid.", nameof(request));

        await _egress.ValidatePublicTargetAsync(
            request.Source, cancellationToken).ConfigureAwait(false);

        var sourceId = "src_" + Guid.NewGuid().ToString("N");
        var mediaId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Source.AbsoluteUri)))
            .ToLowerInvariant()[..24];
        var expires = _clock.GetUtcNow().AddMinutes(30);
        var cipher = Encrypt(request.Source.AbsoluteUri);
        var qualities = new[]
        {
            "best", "2160p", "1440p", "1080p", "720p", "480p", "360p"
        };

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                insert into licensing.sources(
                  source_id,account_id,media_id,source_cipher,qualities,expires_at)
                values(@source,@account,@media,@cipher,@qualities::jsonb,@expires)
                """, connection);
            command.Parameters.AddWithValue("source", sourceId);
            command.Parameters.AddWithValue("account", accountId);
            command.Parameters.AddWithValue("media", mediaId);
            command.Parameters.AddWithValue("cipher", cipher);
            command.Parameters.AddWithValue(
                "qualities", JsonSerializer.Serialize(qualities));
            command.Parameters.AddWithValue("expires", expires);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipher);
        }

        return new AnalyzedMedia(
            sourceId,
            mediaId,
            request.Source.Host,
            null,
            qualities,
            "desktop_worker");
    }

    public async Task<WorkerSourceDescriptor?> ResolveForDesktopAsync(
        Guid accountId,
        string sourceId,
        string quality,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty
            || string.IsNullOrWhiteSpace(sourceId)
            || string.IsNullOrWhiteSpace(quality))
            return null;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select source_cipher,qualities,expires_at
            from licensing.sources
            where source_id=@source and account_id=@account
            """, connection);
        command.Parameters.AddWithValue("source", sourceId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var encrypted = reader.GetFieldValue<byte[]>(0);
        var qualitiesJson = reader.GetFieldValue<string>(1);
        var expires = reader.GetFieldValue<DateTimeOffset>(2);
        if (expires <= _clock.GetUtcNow()) return null;

        var qualities = JsonSerializer.Deserialize<string[]>(qualitiesJson) ?? [];
        if (!qualities.Contains(quality, StringComparer.Ordinal)) return null;

        var uriText = Decrypt(encrypted);
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri)) return null;
        await _egress.ValidatePublicTargetAsync(
            uri, cancellationToken).ConfigureAwait(false);

        var height = ParseHeight(quality);
        var format = height is int h
            ? $"bestvideo[height={h}]+bestaudio/best[height={h}]"
            : "bestvideo+bestaudio/best";
        return new WorkerSourceDescriptor(
            sourceId, uri, format, null, height, "video/mp4", expires);
    }

    public async Task<WorkerSourceDescriptor?> ResolveForWorkerAsync(
        string sourceId,
        string quality,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(quality))
            return null;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select source_cipher,qualities,expires_at
            from licensing.sources
            where source_id=@source
            """, connection);
        command.Parameters.AddWithValue("source", sourceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var encrypted = reader.GetFieldValue<byte[]>(0);
        var qualitiesJson = reader.GetFieldValue<string>(1);
        var expires = reader.GetFieldValue<DateTimeOffset>(2);
        if (expires <= _clock.GetUtcNow()) return null;
        var qualities = JsonSerializer.Deserialize<string[]>(qualitiesJson) ?? [];
        if (!qualities.Contains(quality, StringComparer.Ordinal)) return null;
        var uriText = Decrypt(encrypted);
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri)) return null;
        await _egress.ValidatePublicTargetAsync(uri, cancellationToken).ConfigureAwait(false);
        var height = ParseHeight(quality);
        var format = height is int h
            ? $"bestvideo[height={h}]+bestaudio/best[height={h}]"
            : "bestvideo+bestaudio/best";
        return new WorkerSourceDescriptor(
            sourceId, uri, format, null, height, "video/mp4", expires);
    }

    private async Task<JsonElement> ProbeAsync(Uri source, CancellationToken cancellationToken)
    {
        var selectedProxy = IsSocialVideoHost(source) && _socialProxyUri is not null
            ? _socialProxyUri
            : _proxyUri;
        var arguments = new List<string>
        {
            "--dump-single-json", "--no-playlist", "--skip-download",
            "--no-warnings", "--js-runtimes", "deno:" + _denoPath,
            "--proxy", selectedProxy.AbsoluteUri
        };

        if (NeedsBrowserImpersonation(source))
            arguments.AddRange(["--impersonate", "chrome"]);

        if (IsYouTube(source) && _youtubePotProviderUri is not null)
        {
            arguments.AddRange([
                "--extractor-args",
                "youtube:player_client=mweb",
                "--extractor-args",
                "youtubepot-bgutilhttp:base_url=" + _youtubePotProviderUri.AbsoluteUri.TrimEnd('/')
            ]);
        }

        arguments.AddRange(["--", source.AbsoluteUri]);

        var start = new ProcessStartInfo(_ytDlp)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("yt-dlp could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var stdoutTask = ReadBoundedAsync(process.StandardOutput, MaxMetadataBytes, timeout.Token);
        var stderrTask = ReadBoundedAsync(process.StandardError, 256 * 1024, timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidDataException(ClassifyYtDlpFailure(stderr));

        using var document = JsonDocument.Parse(stdout);
        return document.RootElement.Clone();
    }

    private static bool IsYouTube(Uri source)
    {
        var host = source.Host.TrimEnd('.').ToLowerInvariant();
        return host is "youtu.be" or "youtube.com" or "www.youtube.com"
            or "m.youtube.com" or "music.youtube.com"
            || host.EndsWith(".youtube.com", StringComparison.Ordinal);
    }

    private static bool IsSocialVideoHost(Uri source)
        => IsYouTube(source) || NeedsBrowserImpersonation(source);

    private static bool NeedsBrowserImpersonation(Uri source)
    {
        var host = source.Host.TrimEnd('.').ToLowerInvariant();
        return host == "tiktok.com"
            || host.EndsWith(".tiktok.com", StringComparison.Ordinal)
            || host == "instagram.com"
            || host.EndsWith(".instagram.com", StringComparison.Ordinal)
            || host == "pinterest.com"
            || host.EndsWith(".pinterest.com", StringComparison.Ordinal)
            || host == "pin.it";
    }

    private static string ClassifyYtDlpFailure(string stderr)
    {
        if (stderr.Contains("This video is unavailable", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Video unavailable", StringComparison.OrdinalIgnoreCase))
            return "source_unavailable";
        if (stderr.Contains("Private video", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("members-only", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("login required", StringComparison.OrdinalIgnoreCase))
            return "source_login_required";
        if (stderr.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            return "source_rate_limited";
        if (stderr.Contains("python3", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase))
            return "source_runtime_incomplete";
        return "source_analysis_failed";
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader, int maximum, CancellationToken cancellationToken)
    {
        var buffer = new char[8192];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (builder.Length + read > maximum)
                throw new InvalidDataException("Tool metadata output exceeded limit.");
            builder.Append(buffer, 0, read);
        }
        return builder.ToString();
    }

    private static string[] ReadQualities(JsonElement metadata)
    {
        var heights = new SortedSet<int>();
        if (metadata.TryGetProperty("formats", out var formats)
            && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
                if (format.TryGetProperty("height", out var height)
                    && height.ValueKind == JsonValueKind.Number
                    && height.TryGetInt32(out var h)
                    && h is >= 144 and <= 4320)
                    heights.Add(h);
        }
        return heights.Count == 0
            ? ["best"]
            : heights.Reverse().Take(12).Select(h => h + "p").ToArray();
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private byte[] Encrypt(string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        CryptographicOperations.ZeroMemory(plain);
        return result;
    }

    private string Decrypt(byte[] value)
    {
        if (value.Length < 29) throw new InvalidDataException("Source envelope is invalid.");
        var nonce = value.AsSpan(0, 12);
        var tag = value.AsSpan(12, 16);
        var cipher = value.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        try { return Encoding.UTF8.GetString(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static bool IsDesktopQuality(string quality)
    {
        if (string.Equals(quality, "best", StringComparison.Ordinal))
            return true;
        return quality.EndsWith('p')
            && int.TryParse(quality.AsSpan(0, quality.Length - 1), out var height)
            && height is >= 144 and <= 4320;
    }

    private static int? ParseHeight(string quality)
        => quality.EndsWith('p')
           && int.TryParse(quality.AsSpan(0, quality.Length - 1), out var value)
            ? value
            : null;

    private static byte[] ReadKey(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("Source encryption key is required.");
        try
        {
            var key = Convert.FromBase64String(encoded);
            if (key.Length == 32) return key;
        }
        catch (FormatException) { }
        throw new InvalidOperationException("Source encryption key must be 32 bytes of Base64.");
    }

    public async ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(_key);
        await _dataSource.DisposeAsync();
    }
}
