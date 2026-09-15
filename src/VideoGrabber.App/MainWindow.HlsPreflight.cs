using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private async Task<bool> EnsureSelectedHlsVerifiedAsync(
        MediaCandidate candidate,
        MediaDownloadPlan plan,
        CancellationToken cancellationToken, DownloadRouteScope routeScope)
    {
        if (!plan.IsResolved) return false;
        var verified = new HashSet<string>(_verifiedClearHls.Keys, StringComparer.Ordinal);
        if (HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified)) return true;
        if (candidate.HlsManifest is not { IsMaster: true }) return false;

        var selected = new List<Uri>();
        if (plan.HlsVideoSource is not null) selected.Add(plan.HlsVideoSource);
        if (plan.HlsAudioSource is not null) selected.Add(plan.HlsAudioSource);
        if (selected.Count == 0 && plan.ResolvedHlsLeaf && plan.Source != candidate.Source) selected.Add(plan.Source);
        if (selected.Count == 0) return false;

        _browserHint.Text = "Проверяю выбранное качество HLS…";
        var generation = Volatile.Read(ref _browserDiscoveryGeneration);
        foreach (var source in selected.Distinct())
        {
            if (_verifiedClearHls.ContainsKey(source.AbsoluteUri)) continue;
            var routeProxy = EnsureRoutingProxy(source, candidate.Referer, routeScope);
            var userAgent = _mediaBrowser?.CoreWebView2?.Settings.UserAgent;
            var result = await new HlsPreflightClient().FetchAsync(
                source,
                new HlsPreflightFetchOptions(candidate.Referer, userAgent, routeProxy?.ProxyUrl,
                    CookieProvider: (uri, token) => BuildBrowserCookieHeaderAsync(uri, generation, token)),
                cancellationToken);
            if (!HlsDownloadPolicy.IsVerifiedClearLeaf(result))
            {
                DiagnosticHub.Log.Write("browser.hls.preflight", result.Blocked ? "blocked" : "failed",
                    "host=" + source.IdnHost + " " + (result.Blocked ? "encrypted" : "manifest unavailable"));
                _browserHint.Text = result.Blocked
                    ? "Выбранное качество использует зашифрованный HLS. Оно не поддерживается."
                    : "Не удалось проверить выбранное качество. Повторите попытку или запустите видео для резервного обнаружения.";
                return false;
            }
            HlsDownloadPolicy.UpdateVerifiedClearLeafCache(_verifiedClearHls, source, result.Info);
            DiagnosticHub.Log.Write("browser.hls.preflight", "succeeded", "host=" + source.IdnHost + " clear HLS");
        }

        verified = new HashSet<string>(_verifiedClearHls.Keys, StringComparer.Ordinal);
        return HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified);
    }

    private async Task EnrichMediaDurationAsync(MediaCandidate candidate, long generation)
    {
        if (candidate.HlsManifest is not { IsMaster: true } manifest
            || (manifest.DurationSeconds is > 0 and <= HlsManifestParser.MaxDurationSeconds
                && !manifest.DurationInvalid)
            || manifest.Variants.Count == 0) return;
        var key = candidate.Source.AbsoluteUri;
        if (!_durationProbeInFlight.TryAdd(key, 0)) return;
        try
        {
            var variant = manifest.Variants
                .OrderBy(item => item.Bandwidth ?? long.MaxValue)
                .ThenBy(item => item.Height ?? int.MaxValue)
                .First();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var routeProxy = EnsureRoutingProxy(variant.Uri, candidate.Referer);
            var userAgent = _mediaBrowser?.CoreWebView2?.Settings.UserAgent;
            var result = await new HlsPreflightClient().FetchAsync(variant.Uri,
                new HlsPreflightFetchOptions(candidate.Referer, userAgent, routeProxy?.ProxyUrl,
                    CookieProvider: (uri, token) => BuildBrowserCookieHeaderAsync(uri, generation, token)), timeout.Token);
            var duration = HlsDownloadPolicy.RecordDurationProbeResult(_verifiedClearHls, variant.Uri, result);
            if (duration is null || result.Info is not { IsMaster: false, HasEndList: true, DurationInvalid: false }) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
                if (!_mediaCandidateItems.TryGetValue(key, out var item) || item.Tag is not MediaCandidate current
                    || current.HlsManifest is not { IsMaster: true } currentManifest) return;
                var updated = MediaCandidateMerge.ConfirmDuration(current, duration.Value);
                item.Tag = updated;
                var ordinal = updated.PageOrdinal ?? Math.Max(1, _mediaCandidatesBox.Items.IndexOf(item) + 1);
                item.Content = MediaCandidatePresentation.DisplayName(updated, ordinal, _browserMetadata);
                SyncQueuedCandidate(updated);
            });
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException)
        {
            DiagnosticHub.Log.Write("browser.hls.duration", "failed", ex.GetType().Name);
        }
        finally
        {
            _durationProbeInFlight.TryRemove(key, out _);
        }
    }

    private async Task<string?> BuildBrowserCookieHeaderAsync(Uri source, long generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                EnsureCurrentPage();
                var browser = _mediaBrowser?.CoreWebView2;
                if (browser is null) { completion.TrySetResult(null); return; }
                var cookies = await browser.CookieManager.GetCookiesAsync(source.AbsoluteUri);
                EnsureCurrentPage();
                if (!ReferenceEquals(browser, _mediaBrowser?.CoreWebView2))
                    throw new OperationCanceledException("Browser instance changed.");
                var safe = cookies
                    .Where(cookie => cookie.Name.IndexOfAny(['\r', '\n', ';']) < 0
                        && cookie.Value.IndexOfAny(['\r', '\n']) < 0)
                    .Select(cookie => cookie.Name + "=" + cookie.Value)
                    .ToArray();
                completion.TrySetResult(safe.Length == 0 ? null : string.Join("; ", safe));
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        })) throw new OperationCanceledException("Browser dispatcher unavailable.");
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        void EnsureCurrentPage()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration))
                throw new OperationCanceledException("Browser page changed.");
        }
    }
}
