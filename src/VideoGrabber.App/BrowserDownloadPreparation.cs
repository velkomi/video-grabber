using VideoGrabber.Core.Downloads;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    // WebView2/UI adaptation only; operation, cancellation and completion policy live in Infrastructure/Core.
    private sealed class BrowserDownloadPreparation(MainWindow window, MediaCandidate? selectedCandidate = null, int ordinal = 1)
        : IBrowserDownloadPreparation
    {
        public async Task<PreparedBrowserDownload> PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token)
        {
            window.EnsureIntentSession(intent, token);
            var routeScope = new DownloadRouteScope(window._routePolicy, window._browserUsesSiteRoutes ? window._routeProxy : null);
            ScopedCookieFile? cookieFile = null;
            try
            {
                var selected = new PreparedDownload(intent.SelectedSource, null, null, null, null, null, null, false, false, null, null, null);
                if (selectedCandidate is not null)
                {
                    var candidate = await window.RefreshCandidateBindingAsync(selectedCandidate).WaitAsync(token);
                    window.EnsureIntentSession(intent, token);
                    var plan = MediaDownloadPlanResolver.Resolve(candidate, intent.Quality, intent.AudioOnly);
                    using var preflight = CancellationTokenSource.CreateLinkedTokenSource(token);
                    preflight.CancelAfter(TimeSpan.FromSeconds(60));
                    selected = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
                        () => window.EnsureSelectedHlsVerifiedAsync(candidate, plan, preflight.Token, routeScope),
                        () =>
                        {
                            window.EnsureIntentSession(intent, token);
                            var duration = candidate.HlsManifest?.DurationSeconds;
                            var expectedDuration = duration is > 0 && double.IsFinite(duration.Value) ? duration : null;
                            bool? expectedAudio = intent.AudioOnly || plan.HlsAudioSource is not null ? true : null;
                            return Task.FromResult(new PreparedDownload(plan.Source, candidate.Referer, null, null, null,
                                plan.HlsVideoSource, plan.HlsAudioSource, plan.DirectManifest, plan.ResolvedHlsLeaf,
                                MediaCandidatePresentation.SuggestedBaseName(candidate, candidate.PageOrdinal ?? ordinal, intent.Quality, window._browserMetadata),
                                expectedDuration, expectedAudio));
                        }, error => throw new InvalidOperationException(error ?? "Не удалось проверить выбранное качество HLS."));
                }
                window.EnsureIntentSession(intent, token);
                if (!window.RequiredComponentsAvailable())
                    throw new InvalidOperationException("Компоненты не установлены. Откройте раздел «Компоненты».");
                var embedded = BrowserDownloadSessionPolicy.UseEmbeddedSession(intent.CookieSelection);
                if (embedded && window._mediaBrowser?.CoreWebView2 is null)
                    throw new InvalidOperationException("Сначала войдите во встроенном браузере.");
                cookieFile = embedded ? await window.ExportBrowserSessionAsync(intent, selected, token) : null;
                window.EnsureIntentSession(intent, token);
                lease.Token.ThrowIfCancellationRequested();
                var agent = embedded ? window._mediaBrowser?.CoreWebView2.Settings.UserAgent : null;
                var proxy = window.EnsureRoutingProxy(selected.Source, selected.Referer, routeScope);
                return new PreparedBrowserDownload(selected with { CookiesFile = cookieFile?.Path, UserAgent = agent, LocalProxy = proxy?.ProxyUrl },
                    cookieFile is null ? [routeScope] : [routeScope, cookieFile]);
            }
            catch
            {
                try { cookieFile?.Dispose(); }
                finally { routeScope.Dispose(); }
                throw;
            }
        }
    }

    private async Task<ScopedCookieFile> ExportBrowserSessionAsync(
        UserDownloadIntent intent, PreparedDownload selected, CancellationToken cancellationToken)
    {
        EnsureIntentSession(intent, cancellationToken);
        var browser = _mediaBrowser!.CoreWebView2;
        var sources = new[] { selected.Source, selected.Referer, _browserPageUri, selected.HlsVideoSource, selected.HlsAudioSource }
            .Where(source => source is not null)
            .Cast<Uri>()
            .Distinct()
            .ToArray();
        var cookies = new List<BrowserCookie>();
        foreach (var source in sources)
        {
            EnsureIntentSession(intent, cancellationToken);
            var scopedCookies = await browser.CookieManager.GetCookiesAsync(source.AbsoluteUri).AsTask().WaitAsync(cancellationToken);
            EnsureIntentSession(intent, cancellationToken);
            if (!ReferenceEquals(browser, _mediaBrowser?.CoreWebView2))
                throw new OperationCanceledException("Browser instance changed.", cancellationToken);
            foreach (var cookie in scopedCookies)
                cookies.Add(new(cookie.Domain, cookie.Path, cookie.Name, cookie.Value, cookie.IsSecure, cookie.IsHttpOnly));
        }
        return ScopedCookieFile.Create(sources, cookies.Distinct());
    }
}
