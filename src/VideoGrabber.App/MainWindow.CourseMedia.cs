using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private async Task<IReadOnlyList<MediaCandidate>>
        DiscoverCourseMediaFromDomAsync(
            Uri lessonUri,
            CancellationToken token)
    {
        var players = await ReadCoursePlayerDomItemsAsync(token);
        DiagnosticHub.Log.Write(
            "course.media.dom",
            players.Count == 0 ? "observed" : "succeeded",
            "players=" + players.Count);
        if (players.Count == 0) return [];

        var result = new List<MediaCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var player in players.OrderBy(item => item.order))
        {
            token.ThrowIfCancellationRequested();
            Uri? playerUri = null;
            Uri? masterUri = null;

            if (!string.IsNullOrWhiteSpace(player.url)
                && MediaCandidate.TryCreate(
                    player.url,
                    "text/html",
                    lessonUri,
                    out var playerCandidate)
                && playerCandidate?.Kind == "GetCourse")
                playerUri = playerCandidate.Source;

            if (!string.IsNullOrWhiteSpace(player.masterUrl)
                && UrlPolicy.TryValidate(
                    player.masterUrl,
                    out var directMaster,
                    out _))
                masterUri = directMaster;

            if (masterUri is null && playerUri is not null)
            {
                var page = await FetchCourseTextAsync(
                    playerUri,
                    lessonUri,
                    lessonUri,
                    8_000_000,
                    token);

                if (page is not null
                    && GetCoursePlayerConfigParser.TryExtractMasterPlaylist(
                        page.Body,
                        playerUri,
                        out var parsedMaster)
                    && parsedMaster is not null)
                    masterUri = parsedMaster;
            }

            if (masterUri is null)
            {
                DiagnosticHub.Log.Write(
                    "course.media.resolve",
                    "failed",
                    "GetCourse player did not expose a master playlist");
                continue;
            }

            if (!seen.Add(masterUri.AbsoluteUri))
                continue;

            var manifest = await FetchCourseTextAsync(
                masterUri,
                lessonUri,
                playerUri ?? lessonUri,
                4_000_000,
                token);

            if (manifest is null
                || !HlsManifestParser.TryParse(
                    manifest.Body,
                    masterUri,
                    out var info)
                || info is null
                || !HlsDownloadPolicy.IsAllowed(info))
            {
                DiagnosticHub.Log.Write(
                    "course.media.resolve",
                    "failed",
                    "Master playlist unavailable or encrypted");
                continue;
            }

            if (!info.IsMaster)
            {
                DiagnosticHub.Log.Write(
                    "course.media.resolve",
                    "failed",
                    "GetCourse master endpoint returned a non-master HLS");
                continue;
            }

            var ordinal = Math.Max(1, player.order + 1);
            result.Add(new MediaCandidate(
                masterUri,
                lessonUri,

                "HLS",
                "master [course DOM]",
                HlsManifest: info,
                PageOrdinal: ordinal,
                PageSectionTitle: string.IsNullOrWhiteSpace(player.title)
                    ? null
                    : player.title));

            DiagnosticHub.Log.Write(
                "course.media.resolve",
                "succeeded",
                "GetCourse master resolved; variants="
                + info.Variants.Count);
        }

        return result;
    }

    private async Task<IReadOnlyList<CoursePlayerDomItem>>
        ReadCoursePlayerDomItemsAsync(
            CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2;
        if (core is null) return [];
        var lease = _browserPages.Capture();
        if (!IsCurrentBrowserPage(lease, core)) return [];

        const string script = """
            (() => {
              const clean = value =>
                (value || '').replace(/\s+/g, ' ').trim();
              const nodes = [
                ...document.querySelectorAll(
                  '[data-iframe-src*="/sign-player/"],' +
                  'iframe[src*="/sign-player/"],' +
                  '[data-master-play-list-url]')
              ];
              const seen = new Set();
              const result = [];
              nodes.forEach((node, index) => {
                const url =
                  node.getAttribute('data-iframe-src') ||
                  node.getAttribute('src') ||
                  '';
                const masterUrl =
                  node.getAttribute('data-master-play-list-url') ||
                  '';
                const key = url || masterUrl;
                if (!key || seen.has(key)) return;
                seen.add(key);

                const block = node.closest(
                  '.lite-block-live-wrapper,.lt-block,' +
                  '.builder-item,.row-section') || node.parentElement;
                const heading = block?.querySelector(
                  'h1,h2,h3,.f-header,.part-header');
                result.push({
                  url,
                  masterUrl,
                  title: clean(
                    heading?.innerText ||
                    heading?.textContent ||
                    ('Видео ' + (index + 1))),
                  order: result.length
                });
              });
              return result;
            })()
            """;

        try
        {
            var json = await core.ExecuteScriptAsync(script)
                .AsTask()
                .WaitAsync(token);

            token.ThrowIfCancellationRequested();
            if (!IsCurrentBrowserPage(lease, core)) return [];

            var source = json;
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind == JsonValueKind.String)
                    source = document.RootElement.GetString()
                        ?? "[]";
                else if (document.RootElement.ValueKind == JsonValueKind.Null)
                    return [];
            }

            return JsonSerializer.Deserialize<CoursePlayerDomItem[]>(source)
                ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write(
                "course.media.dom",
                "failed",
                ex.GetType().Name);
            return [];
        }
    }

    private async Task<CourseTextFetch?> FetchCourseTextAsync(
        Uri source,
        Uri routeAnchor,
        Uri requestReferer,
        int maxChars,
        CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2
            ?? throw new InvalidOperationException(
                "Встроенный браузер закрыт.");
        var current = source;

        for (var redirect = 0; redirect <= 6; redirect++)
        {
            token.ThrowIfCancellationRequested();
            if (!UrlPolicy.TryValidate(
                    current.AbsoluteUri,
                    out var safe,
                    out _)
                || safe is null)
                return null;
            current = safe;

            _ = DownloadRouteResolver.ResolveAdapterId(
                _routePolicy,
                _routes,
                current,
                routeAnchor);

            var connector = new RouteConnector(_routePolicy);
            using var handler = new SocketsHttpHandler
            {
                UseCookies = false,
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(25),
                ConnectCallback = (context, cancellationToken) =>
                    new ValueTask<Stream>(connector.OpenAsync(
                        context.DnsEndPoint.Host,
                        context.DnsEndPoint.Port,
                        cancellationToken))
            };

            using var http = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                current);
            request.Headers.Referrer = requestReferer;
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("*/*"));

            var userAgent = core.Settings.UserAgent;
            if (!string.IsNullOrWhiteSpace(userAgent))
                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    userAgent);

            var cookies = await core.CookieManager
                .GetCookiesAsync(current.AbsoluteUri)
                .AsTask()
                .WaitAsync(token);
            var cookieHeader = string.Join(
                "; ",
                cookies
                    .Where(cookie =>
                        !string.IsNullOrWhiteSpace(cookie.Name)
                        && cookie.Name.IndexOfAny(['\r', '\n', ';']) < 0
                        && cookie.Value.IndexOfAny(['\r', '\n']) < 0)
                    .Select(cookie =>
                        cookie.Name + "=" + cookie.Value));
            if (!string.IsNullOrWhiteSpace(cookieHeader))
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    cookieHeader);

            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token);
            if (IsCourseRedirect(response.StatusCode))
            {
                var location = response.Headers.Location;
                if (location is null) return null;
                current = location.IsAbsoluteUri
                    ? location
                    : new Uri(current, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                DiagnosticHub.Log.Write(
                    "course.http",
                    "failed",
                    "host=" + current.IdnHost
                    + " HTTP=" + (int)response.StatusCode);
                return null;
            }

            var declared = response.Content.Headers.ContentLength;
            if (declared is > 0
                && declared > Math.Max(1024, maxChars * 4L))
                return null;

            await using var input =
                await response.Content.ReadAsStreamAsync(token);
            var body = await ReadLimitedTextAsync(
                input,
                maxChars,
                token);
            return new CourseTextFetch(
                current,
                body,
                response.Content.Headers.ContentType?.MediaType);
        }

        return null;
    }
}
