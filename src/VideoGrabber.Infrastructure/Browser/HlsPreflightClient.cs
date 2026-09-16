using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record HlsPreflightFetchOptions(
    Uri? Referer = null,
    string? UserAgent = null,
    string? LocalProxy = null,
    string? CookieHeader = null,
    Func<Uri, CancellationToken, Task<string?>>? CookieProvider = null,
    Guid? EgressCapabilityId = null,
    Uri? EgressEndpoint = null);

public sealed record HlsPreflightFetchResult(
    bool Success,
    bool Blocked,
    HlsManifestInfo? Info,
    string? Error = null);

public sealed class HlsPreflightClient(
    Func<HlsPreflightFetchOptions, HttpMessageHandler>? handlerFactory = null,
    IManagedEgressSessionResolver? egressResolver = null)
{
    private readonly Func<HlsPreflightFetchOptions, HttpMessageHandler> _handlerFactory = handlerFactory ?? CreateHandler;
    private readonly IManagedEgressSessionResolver? _egressResolver = egressResolver;

    public async Task<HlsPreflightFetchResult> FetchAsync(Uri source, HlsPreflightFetchOptions options, CancellationToken cancellationToken)
    {
        var egress = ValidateEgress(options);
        if (!IsSafeHttp(source, egress)) return new(false, false, null, "Некорректная HLS-ссылка.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        var token = deadline.Token;
        using var handler = _handlerFactory(options);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var current = source;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var redirects = 0; ; redirects++)
        {
            token.ThrowIfCancellationRequested();
            if (!visited.Add(current.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped)))
                return new(false, false, null, "Циклическое перенаправление HLS.");
            var cookie = options.CookieProvider is not null
                ? await options.CookieProvider(current, token).WaitAsync(token).ConfigureAwait(false)
                : HasSameCookieScope(source, current) ? options.CookieHeader : null;
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.5));
            if (options.Referer is { } referer && IsSafeHttp(referer, egress)) request.Headers.Referrer = referer;
            if (!string.IsNullOrWhiteSpace(options.UserAgent) && options.UserAgent.Length <= 1024 && options.UserAgent.IndexOfAny(['\r', '\n']) < 0)
                request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
            if (!string.IsNullOrWhiteSpace(cookie) && cookie.Length <= 65536 && cookie.IndexOfAny(['\r', '\n']) < 0)
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (redirects >= 5) return new(false, false, null, "Слишком много перенаправлений HLS.");
                if (response.Headers.Location is not { } location
                    || !Uri.TryCreate(current, location, out var next) || !IsSafeHttp(next, egress)
                    || current.Scheme == "https" && next.Scheme != "https")
                    return new(false, false, null, "Недопустимое перенаправление HLS.");
                current = next;
                continue;
            }
            if (!response.IsSuccessStatusCode) return new(false, false, null, "HTTP " + (int)response.StatusCode);
            if (response.Content.Headers.ContentLength is > 4_000_000) return new(false, false, null, "HLS manifest слишком большой.");
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var body = await ReadLimitedTextAsync(stream, 4_000_000, token).ConfigureAwait(false);
            if (!HlsManifestParser.TryParse(body, current, out var info) || info is null)
                return new(false, false, null, "Ответ не является HLS manifest.");
            if (info.Variants.Any(item => !IsSafeHttp(item.Uri, egress))
                || info.AudioRenditions.Any(item => !IsSafeHttp(item.Uri, egress)))
                return new(false, false, null, "HLS manifest содержит недопустимую вложенную ссылку.");
            if (!HlsDownloadPolicy.IsAllowed(info))
                return new(false, true, info, "Зашифрованный HLS не поддерживается.");
            return new(true, false, info);
        }
    }

    private static bool HasSameCookieScope(Uri source, Uri target)
        => source.Scheme == target.Scheme && source.IdnHost == target.IdnHost && source.Port == target.Port
            && string.Equals(source.AbsolutePath, target.AbsolutePath, StringComparison.Ordinal);

    private EgressSessionLease? ValidateEgress(HlsPreflightFetchOptions options)
    {
        var hasAny = !string.IsNullOrWhiteSpace(options.LocalProxy)
            || options.EgressCapabilityId is not null || options.EgressEndpoint is not null;
        if (!hasAny) return null;
        if (string.IsNullOrWhiteSpace(options.LocalProxy)
            || options.EgressCapabilityId is not Guid id || options.EgressEndpoint is not Uri endpoint
            || _egressResolver is null)
            throw new InvalidOperationException("Managed egress capability is required for local proxy use.");
        if (!Uri.TryCreate(options.LocalProxy, UriKind.Absolute, out var proxy)
            || !string.Equals(proxy.AbsoluteUri, endpoint.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Egress endpoint mismatch.");
        var lease = _egressResolver.Resolve(id, endpoint);
        if (!string.Equals(lease.ProxyUri.AbsoluteUri, endpoint.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Egress capability endpoint mismatch.");
        return lease;
    }

    private static HttpMessageHandler CreateHandler(HlsPreflightFetchOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = false,
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        if (string.IsNullOrWhiteSpace(options.LocalProxy))
        {
            var connector = new RouteConnector(new SiteRouteSettings([]));
            handler.UseProxy = false;
            handler.ConnectCallback = (context, token) =>
                new ValueTask<Stream>(connector.OpenAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, token));
            return handler;
        }
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

    private static bool IsSafeHttp(Uri uri, EgressSessionLease? egress)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        if (UrlPolicy.TryValidate(uri.AbsoluteUri, out _, out _)) return true;
        return egress is { Policy.PublicOnly: false };
    }
}
