using VideoGrabber.Core.Downloads;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    // WebView2/UI adaptation only; operation, cancellation and completion policy live in Infrastructure/Core.
    private sealed class BrowserDownloadPreparation(MainWindow window, MediaCandidate? selectedCandidate = null, int ordinal = 1,
        BrowserQueueContext? queueContext = null)
        : IBrowserDownloadPreparation
    {
        public async Task<PreparedBrowserDownload> PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token)
        {
            EnsureSession();
            var routeScope = new DownloadRouteScope(window._routePolicy, window._browserUsesSiteRoutes ? window._routeProxy : null);
            ScopedCookieFile? cookieFile = null;
            try
            {
                var selected = new PreparedDownload(intent.SelectedSource, null, null, null, null, null, null, false, false, null, null, null);
                if (selectedCandidate is not null)
                {
                    var candidate = queueContext is null
                        ? await window.RefreshCandidateBindingAsync(selectedCandidate).WaitAsync(token)
                        : selectedCandidate;
                    EnsureSession();
                    var plan = MediaDownloadPlanResolver.Resolve(candidate, intent.Quality, intent.AudioOnly);
                    using var preflight = CancellationTokenSource.CreateLinkedTokenSource(token);
                    preflight.CancelAfter(TimeSpan.FromSeconds(60));
                    selected = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
                        () => window.EnsureSelectedHlsVerifiedAsync(candidate, plan, preflight.Token, routeScope),
                        () =>
                        {
                            EnsureSession();
                            var duration = candidate.HlsManifest?.DurationSeconds;
                            var expectedDuration = duration is > 0 && double.IsFinite(duration.Value) ? duration : null;
                            bool? expectedAudio = intent.AudioOnly || plan.HlsAudioSource is not null ? true : null;
                            return Task.FromResult(new PreparedDownload(plan.Source, candidate.Referer, null, null, null,
                                plan.HlsVideoSource, plan.HlsAudioSource, plan.DirectManifest, plan.ResolvedHlsLeaf,
                                MediaCandidatePresentation.SuggestedBaseName(candidate, candidate.PageOrdinal ?? ordinal, intent.Quality, queueContext?.Metadata ?? window._browserMetadata),
                                expectedDuration, expectedAudio));
                        }, error => throw new InvalidOperationException(error ?? "Не удалось проверить выбранное качество HLS."));
                }
                EnsureSession();
                if (!window.RequiredComponentsAvailable())
                    throw new InvalidOperationException("Компоненты не установлены. Откройте раздел «Компоненты».");
                var embedded = BrowserDownloadSessionPolicy.UseEmbeddedSession(intent.CookieSelection);
                if (embedded && window._mediaBrowser?.CoreWebView2 is null)
                    throw new InvalidOperationException("Сначала войдите во встроенном браузере.");
                cookieFile = embedded ? await window.ExportBrowserSessionAsync(intent, selected, token, queueContext) : null;
                EnsureSession();
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

            void EnsureSession()
            {
                lease.Token.ThrowIfCancellationRequested();
                window.EnsureDownloadSession(intent, token, queueContext);
            }
        }
    }

    private async Task<ScopedCookieFile> ExportBrowserSessionAsync(
        UserDownloadIntent intent, PreparedDownload selected, CancellationToken cancellationToken, BrowserQueueContext? queueContext = null)
    {
        EnsureDownloadSession(intent, cancellationToken, queueContext);
        var browser = _mediaBrowser!.CoreWebView2;
        var sources = new[] { selected.Source, selected.Referer, queueContext?.Page ?? _browserPageUri, selected.HlsVideoSource, selected.HlsAudioSource }
            .Where(source => source is not null)
            .Cast<Uri>()
            .Distinct()
            .ToArray();
        var cookies = new List<BrowserCookie>();
        foreach (var source in sources)
        {
            EnsureDownloadSession(intent, cancellationToken, queueContext);
            var scopedCookies = await browser.CookieManager.GetCookiesAsync(source.AbsoluteUri).AsTask().WaitAsync(cancellationToken);
            EnsureDownloadSession(intent, cancellationToken, queueContext);
            if (!ReferenceEquals(browser, _mediaBrowser?.CoreWebView2))
                throw new OperationCanceledException("Browser instance changed.", cancellationToken);
            foreach (var cookie in scopedCookies)
                cookies.Add(new(cookie.Domain, cookie.Path, cookie.Name, cookie.Value, cookie.IsSecure, cookie.IsHttpOnly));
        }
        return ScopedCookieFile.Create(sources, cookies.Distinct());
    }

    private void EnsureDownloadSession(UserDownloadIntent intent, CancellationToken token, BrowserQueueContext? queueContext)
    {
        if (queueContext is null) { EnsureIntentSession(intent, token); return; }
        token.ThrowIfCancellationRequested();
        if (queueContext.SessionEpoch != _browserSessionEpoch || intent.SessionEpoch != queueContext.SessionEpoch)
            throw new OperationCanceledException("Browser session changed.", token);
    }
}
