using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record HlsPreflightFetchOptions(
    Uri? Referer = null,
    string? UserAgent = null,
    string? LocalProxy = null,
    string? CookieHeader = null);

public sealed record HlsPreflightFetchResult(
    bool Success,
    bool Blocked,
    HlsManifestInfo? Info,
    string? Error = null);

public sealed class HlsPreflightClient(Func<HlsPreflightFetchOptions, HttpMessageHandler>? handlerFactory = null)
{
    private readonly Func<HlsPreflightFetchOptions, HttpMessageHandler> _handlerFactory = handlerFactory ?? CreateHandler;

    public async Task<HlsPreflightFetchResult> FetchAsync(Uri source, HlsPreflightFetchOptions options, CancellationToken cancellationToken)
    {
        if (!IsSafeHttp(source)) return new(false, false, null, "Некорректная HLS-ссылка.");
        using var handler = _handlerFactory(options);
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.5));
        if (options.Referer is { } referer && IsSafeHttp(referer)) request.Headers.Referrer = referer;
        if (!string.IsNullOrWhiteSpace(options.UserAgent) && options.UserAgent.Length <= 1024 && options.UserAgent.IndexOfAny(['\r', '\n']) < 0)
            request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
        if (!string.IsNullOrWhiteSpace(options.CookieHeader) && options.CookieHeader.Length <= 65536 && options.CookieHeader.IndexOfAny(['\r', '\n']) < 0)
            request.Headers.TryAddWithoutValidation("Cookie", options.CookieHeader);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new(false, false, null, "HTTP " + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength is > 4_000_000) return new(false, false, null, "HLS manifest слишком большой.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var body = await ReadLimitedTextAsync(stream, 4_000_000, cancellationToken).ConfigureAwait(false);
        if (!HlsManifestParser.TryParse(body, source, out var info) || info is null)
            return new(false, false, null, "Ответ не является HLS manifest.");
        if (!HlsDownloadPolicy.IsAllowed(info))
            return new(false, true, info, "Зашифрованный HLS не поддерживается.");
        return new(true, false, info);
    }

    private static HttpMessageHandler CreateHandler(HlsPreflightFetchOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = false,
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        if (string.IsNullOrWhiteSpace(options.LocalProxy)) return handler;
        if (!Uri.TryCreate(options.LocalProxy, UriKind.Absolute, out var proxy)
            || proxy.Scheme != "socks5" || proxy.Host != "127.0.0.1" || proxy.Port is < 1024 or > 65535
            || !string.IsNullOrEmpty(proxy.UserInfo) || proxy.AbsolutePath != "/" || !string.IsNullOrEmpty(proxy.Query))
            throw new InvalidOperationException("Invalid local routing endpoint.");
        handler.UseProxy = true;
        handler.Proxy = new WebProxy(proxy);
        return handler;
    }

    private static async Task<string> ReadLimitedTextAsync(Stream input, int maxChars, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input);
        var buffer = new char[8192];
        var text = new System.Text.StringBuilder(Math.Min(maxChars, 65536));
        while (text.Length <= maxChars)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maxChars + 1 - text.Length)), cancellationToken).ConfigureAwait(false);
            if (read == 0) return text.ToString();
            text.Append(buffer, 0, read);
        }
        throw new InvalidDataException("HLS manifest слишком большой.");
    }

    private static bool IsSafeHttp(Uri uri)
        => uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);
}
