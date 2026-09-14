using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private async Task<bool> EnsureSelectedHlsVerifiedAsync(
        MediaCandidate candidate,
        MediaDownloadPlan plan,
        CancellationToken cancellationToken)
    {
        var verified = new HashSet<string>(_verifiedClearHls.Keys, StringComparer.Ordinal);
        if (HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified)) return true;
        if (candidate.HlsManifest is not { IsMaster: true }) return false;

        var selected = new List<Uri>();
        if (plan.HlsVideoSource is not null) selected.Add(plan.HlsVideoSource);
        if (plan.HlsAudioSource is not null) selected.Add(plan.HlsAudioSource);
        if (selected.Count == 0 && plan.Source != candidate.Source) selected.Add(plan.Source);
        if (selected.Count == 0) return false;

        _browserHint.Text = "Проверяю выбранное качество HLS…";
        foreach (var source in selected.Distinct())
        {
            if (_verifiedClearHls.ContainsKey(source.AbsoluteUri)) continue;
            var routeProxy = EnsureRoutingProxy(source, candidate.Referer);
            var cookieHeader = await BuildBrowserCookieHeaderAsync(source);
            var userAgent = _mediaBrowser?.CoreWebView2?.Settings.UserAgent;
            var result = await new HlsPreflightClient().FetchAsync(
                source,
                new HlsPreflightFetchOptions(candidate.Referer, userAgent, routeProxy?.ProxyUrl, cookieHeader),
                cancellationToken);
            if (!result.Success)
            {
                DiagnosticHub.Log.Write("browser.hls.preflight", result.Blocked ? "blocked" : "failed",
                    "host=" + source.IdnHost + " " + (result.Blocked ? "encrypted" : "manifest unavailable"));
                _browserHint.Text = result.Blocked
                    ? "Выбранное качество использует зашифрованный HLS. Оно не поддерживается."
                    : "Не удалось проверить выбранное качество. Повторите попытку или запустите видео для резервного обнаружения.";
                return false;
            }
            _verifiedClearHls[source.AbsoluteUri] = 0;
            DiagnosticHub.Log.Write("browser.hls.preflight", "succeeded", "host=" + source.IdnHost + " clear HLS");
        }

        verified = new HashSet<string>(_verifiedClearHls.Keys, StringComparer.Ordinal);
        return HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified);
    }

    private async Task EnrichMediaDurationAsync(MediaCandidate candidate, long generation)
    {
        if (candidate.HlsManifest is not { IsMaster: true } manifest
            || manifest.DurationSeconds is > 0 || manifest.Variants.Count == 0) return;
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
            var cookieHeader = await BuildBrowserCookieHeaderAsync(variant.Uri);
            var userAgent = _mediaBrowser?.CoreWebView2?.Settings.UserAgent;
            var result = await new HlsPreflightClient().FetchAsync(variant.Uri,
                new HlsPreflightFetchOptions(candidate.Referer, userAgent, routeProxy?.ProxyUrl, cookieHeader), timeout.Token);
            if (!result.Success || result.Info?.DurationSeconds is not > 0) return;
            _verifiedClearHls[variant.Uri.AbsoluteUri] = 0;
            var duration = result.Info.DurationSeconds.Value;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
                if (!_mediaCandidateItems.TryGetValue(key, out var item) || item.Tag is not MediaCandidate current
                    || current.HlsManifest is not { IsMaster: true } currentManifest) return;
                var updated = current with { HlsManifest = currentManifest with { DurationSeconds = duration } };
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

    private async Task<string?> BuildBrowserCookieHeaderAsync(Uri source)
    {
        if (_mediaBrowser?.CoreWebView2 is null) return null;
        var cookies = await _mediaBrowser.CoreWebView2.CookieManager.GetCookiesAsync(source.AbsoluteUri);
        var safe = cookies
            .Where(cookie => cookie.Name.IndexOfAny(['\r', '\n', ';']) < 0
                && cookie.Value.IndexOfAny(['\r', '\n']) < 0)
            .Select(cookie => cookie.Name + "=" + cookie.Value)
            .ToArray();
        return safe.Length == 0 ? null : string.Join("; ", safe);
    }
}
