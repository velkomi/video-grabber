using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private sealed record PendingHlsManifest(DevToolsHlsResponse Response, Uri Referer);

    private CoreWebView2DevToolsProtocolEventReceiver? _networkResponseReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _networkRequestReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _networkLoadingFinishedReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _networkLoadingFailedReceiver;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _networkResponseHandler;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _networkRequestHandler;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _networkLoadingFinishedHandler;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _networkLoadingFailedHandler;
    private CoreWebView2DevToolsProtocolEventReceiver? _targetAttachedReceiver;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _targetAttachedHandler;
    private CoreWebView2DevToolsProtocolEventReceiver? _targetDetachedReceiver;
    private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DevToolsProtocolEventReceivedEventArgs>? _targetDetachedHandler;
    private readonly ConcurrentDictionary<string, byte> _devToolsSessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DevToolsGetCourseResponse> _pendingGetCoursePlayers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DevToolsRequestContext> _networkRequestContexts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingHlsManifest> _pendingHlsManifests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Microsoft.UI.Xaml.Controls.ComboBoxItem> _mediaCandidateItems = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _verifiedClearHls = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _durationProbeInFlight = new(StringComparer.Ordinal);
    private IReadOnlyList<DevToolsFrameInfo> _browserFrames = [];
    private long _browserDiscoveryGeneration;
    private const string AutoAttachJson = "{\"autoAttach\":true,\"waitForDebuggerOnStart\":true,\"flatten\":true}";

    private async Task EnableDevToolsMediaDiscoveryAsync(CoreWebView2 core)
    {
        _targetAttachedReceiver = core.GetDevToolsProtocolEventReceiver("Target.attachedToTarget");
        _targetAttachedHandler = async (_, args) => await OnDevToolsTargetAttachedAsync(core, args.ParameterObjectAsJson);
        _targetAttachedReceiver.DevToolsProtocolEventReceived += _targetAttachedHandler;
        _targetDetachedReceiver = core.GetDevToolsProtocolEventReceiver("Target.detachedFromTarget");
        _targetDetachedHandler = (_, args) => OnDevToolsTargetDetached(args.ParameterObjectAsJson);
        _targetDetachedReceiver.DevToolsProtocolEventReceived += _targetDetachedHandler;

        _networkResponseReceiver = core.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        _networkRequestReceiver = core.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent");
        _networkLoadingFinishedReceiver = core.GetDevToolsProtocolEventReceiver("Network.loadingFinished");
        _networkLoadingFailedReceiver = core.GetDevToolsProtocolEventReceiver("Network.loadingFailed");
        _networkResponseHandler = (_, args) => OnDevToolsResponse(args.ParameterObjectAsJson, args.SessionId);
        _networkRequestHandler = (_, args) => OnDevToolsRequest(args.ParameterObjectAsJson, args.SessionId);
        _networkLoadingFinishedHandler = async (_, args) => await OnDevToolsLoadingFinishedAsync(core, args.ParameterObjectAsJson, args.SessionId);
        _networkLoadingFailedHandler = (_, args) => OnDevToolsLoadingFailed(args.ParameterObjectAsJson, args.SessionId);
        _networkResponseReceiver.DevToolsProtocolEventReceived += _networkResponseHandler;
        _networkRequestReceiver.DevToolsProtocolEventReceived += _networkRequestHandler;
        _networkLoadingFinishedReceiver.DevToolsProtocolEventReceived += _networkLoadingFinishedHandler;
        _networkLoadingFailedReceiver.DevToolsProtocolEventReceived += _networkLoadingFailedHandler;

        await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        await core.CallDevToolsProtocolMethodAsync("Target.setAutoAttach", AutoAttachJson);
        DiagnosticHub.Log.Write("browser.devtools", "succeeded", "Network enabled; iframe auto-attach enabled; HLS sniffer active");
    }

    private void OnDevToolsResponse(string json, string? sessionId)
    {
        var page = _browserPageUri;
        if (page is null) return;
        var generation = Volatile.Read(ref _browserDiscoveryGeneration);
        TrimPendingStateIfNeeded();

        if (DevToolsGetCourseResponseParser.TryParsePlayerResponse(json, page, out var player) && player is not null)
        {
            _pendingGetCoursePlayers[RequestKey(sessionId, player.RequestId)] = player;
            DiagnosticHub.Log.Write("browser.player", "observed", player.SafeDisplay + " waiting for response body");
            return;
        }

        if (DevToolsHlsSnifferParser.TryParseHlsResponse(json, out var hls) && hls is not null)
        {
            var key = RequestKey(sessionId, hls.RequestId);
            var referer = _networkRequestContexts.TryGetValue(key, out var context) ? context.Referer : page;
            _pendingHlsManifests[key] = new PendingHlsManifest(hls, referer);
            DiagnosticHub.Log.Write("browser.hls", "observed", hls.SafeDisplay, jobId: "browser-" + generation);
            return;
        }

        if (DevToolsMediaEventParser.TryParseResponse(json, page, out var candidate) && candidate is not null && candidate.Kind is not ("GetCourse" or "HLS"))
        {
            QueueMediaCandidate(candidate, generation);
            return;
        }
        if (DevToolsMediaEventParser.TrySummarizeResponse(json, out var summary) && summary is { IsMediaLike: true })
            DiagnosticHub.Log.Write("browser.devtools.observe", "event", summary.SafeDisplay);
    }

    private void OnDevToolsRequest(string json, string? sessionId)
    {
        var page = _browserPageUri;
        if (page is null) return;
        var generation = Volatile.Read(ref _browserDiscoveryGeneration);
        if (DevToolsHlsSnifferParser.TryParseRequest(json, page, out var context) && context is not null)
        {
            if (_networkRequestContexts.Count > 5000) _networkRequestContexts.Clear();
            _networkRequestContexts[RequestKey(sessionId, context.RequestId)] = context;
        }
        if (!DevToolsMediaEventParser.TryParseRequest(json, page, out var candidate) || candidate is null) return;
        if (candidate.Kind is "GetCourse" or "HLS") return;
        QueueMediaCandidate(candidate, generation);
    }

    private async Task OnDevToolsLoadingFinishedAsync(CoreWebView2 core, string json, string? sessionId)
    {
        if (!DevToolsGetCourseResponseParser.TryParseLoadingFinished(json, out var requestId) || requestId is null) return;
        var key = RequestKey(sessionId, requestId);
        var generation = Volatile.Read(ref _browserDiscoveryGeneration);
        try
        {
            if (_pendingGetCoursePlayers.TryRemove(key, out var player))
                await ResolveGetCoursePlayerAsync(core, player, requestId, sessionId, generation);
            if (_pendingHlsManifests.TryRemove(key, out var pending))
                await ResolveHlsManifestAsync(core, pending, requestId, sessionId, generation);
        }
        finally
        {
            _networkRequestContexts.TryRemove(key, out _);
        }
    }

    private void OnDevToolsLoadingFailed(string json, string? sessionId)
    {
        if (!DevToolsGetCourseResponseParser.TryParseLoadingFailed(json, out var requestId) || requestId is null) return;
        var key = RequestKey(sessionId, requestId);
        _pendingGetCoursePlayers.TryRemove(key, out _);
        _pendingHlsManifests.TryRemove(key, out _);
        _networkRequestContexts.TryRemove(key, out _);
    }

    private void TrimPendingStateIfNeeded()
    {
        if (_pendingGetCoursePlayers.Count <= 2048 && _pendingHlsManifests.Count <= 2048 && _networkRequestContexts.Count <= 4096) return;
        _pendingGetCoursePlayers.Clear(); _pendingHlsManifests.Clear(); _networkRequestContexts.Clear();
        DiagnosticHub.Log.Write("browser.devtools", "observed", "Pending network state reset after safety limit");
    }

    private async Task ResolveGetCoursePlayerAsync(CoreWebView2 core, DevToolsGetCourseResponse player, string requestId, string? sessionId, long generation)
    {
        try
        {
            var bodyJson = await GetResponseBodyAsync(core, requestId, sessionId);
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
            if (DevToolsGetCourseResponseParser.TryDecodeBody(bodyJson, out var body)
                && body is not null
                && GetCoursePlayerConfigParser.TryExtractMasterPlaylist(body, player.PlayerUri, out var playlist)
                && playlist is not null)
            {
                QueueMediaCandidate(new MediaCandidate(playlist, player.Referer, "HLS", "master [GetCourse fallback]",
                    FrameId: player.FrameId), generation);
                DiagnosticHub.Log.Write("browser.player", "succeeded", "master HLS announced on " + playlist.IdnHost + "; waiting for HLS network response");
            }
            else
                DiagnosticHub.Log.Write("browser.player", "observed", player.SafeDisplay + " without master playlist");
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("browser.player", "failed", player.SafeDisplay + " " + ex.GetType().Name);
        }
    }

    private async Task ResolveHlsManifestAsync(CoreWebView2 core, PendingHlsManifest pending, string requestId, string? sessionId, long generation)
    {
        var fallback = new MediaCandidate(pending.Response.Source, pending.Referer, "HLS", FrameId: pending.Response.FrameId);
        try
        {
            var bodyJson = await GetResponseBodyAsync(core, requestId, sessionId);
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
            if (!DevToolsGetCourseResponseParser.TryDecodeBody(bodyJson, out var body) || body is null
                || !HlsManifestParser.TryParse(body, pending.Response.Source, out var info) || info is null)
            {
                QueueMediaCandidate(fallback, generation);
                return;
            }
            if (!HlsDownloadPolicy.IsAllowed(info))
            {
                _verifiedClearHls.TryRemove(pending.Response.Source.AbsoluteUri, out _);
                RemoveMediaCandidatesReferencing(pending.Response.Source, generation);
                DiagnosticHub.Log.Write("browser.hls", "blocked", pending.Response.SafeDisplay + " " + info.SafeSummary);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (generation == Volatile.Read(ref _browserDiscoveryGeneration))
                        _browserHint.Text = "Обнаружен зашифрованный HLS (EXT-X-KEY). Получение ключей не поддерживается.";
                });
                return;
            }
            _verifiedClearHls[pending.Response.Source.AbsoluteUri] = 0;
            var details = info.SafeSummary;
            QueueMediaCandidate(new MediaCandidate(
                pending.Response.Source, pending.Referer, "HLS", details,
                HlsManifest: info, FrameId: pending.Response.FrameId), generation);
            DiagnosticHub.Log.Write("browser.hls", "succeeded", pending.Response.SafeDisplay + " " + details);
        }
        catch (Exception ex)
        {
            _verifiedClearHls.TryRemove(pending.Response.Source.AbsoluteUri, out _);
            DiagnosticHub.Log.Write("browser.hls", "failed", pending.Response.SafeDisplay + " " + ex.GetType().Name);
            if (generation == Volatile.Read(ref _browserDiscoveryGeneration)) QueueMediaCandidate(fallback, generation);
        }
    }

    private static Task<string> GetResponseBodyAsync(CoreWebView2 core, string requestId, string? sessionId)
    {
        var parameters = JsonSerializer.Serialize(new { requestId });
        return string.IsNullOrEmpty(sessionId)
            ? core.CallDevToolsProtocolMethodAsync("Network.getResponseBody", parameters).AsTask()
            : core.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Network.getResponseBody", parameters).AsTask();
    }

    private async Task OnDevToolsTargetAttachedAsync(CoreWebView2 core, string json)
    {
        if (!DevToolsTargetEventParser.TryParseSessionId(json, out var sessionId) || sessionId is null) return;
        if (!_devToolsSessions.TryAdd(sessionId, 0))
        {
            try { await core.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Runtime.runIfWaitingForDebugger", "{}"); } catch { }
            return;
        }
        DevToolsTargetSession? target = null;
        try
        {
            if (DevToolsTargetEventParser.TryParseAttached(json, out target) && target is not null)
            {
                await core.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Network.enable", "{}");
                await core.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Target.setAutoAttach", AutoAttachJson);
                DiagnosticHub.Log.Write("browser.devtools.target", "succeeded", target.SafeDisplay);
            }
            else
                DiagnosticHub.Log.Write("browser.devtools.target", "observed", "unhandled target session");
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("browser.devtools.target", "failed", (target?.SafeDisplay ?? "unhandled target") + " " + ex.GetType().Name);
        }
        finally
        {
            try { await core.CallDevToolsProtocolMethodForSessionAsync(sessionId, "Runtime.runIfWaitingForDebugger", "{}"); } catch { }
        }
        ScheduleBrowserBindingRefresh(core);
    }

    private void OnDevToolsTargetDetached(string json)
    {
        if (!DevToolsTargetEventParser.TryParseSessionId(json, out var sessionId) || sessionId is null) return;
        _devToolsSessions.TryRemove(sessionId, out _);
        RemoveSessionScopedPending(sessionId);
        DiagnosticHub.Log.Write("browser.devtools.target", "observed", "Detached target state cleared");
    }

    private void RemoveSessionScopedPending(string sessionId)
    {
        var prefix = sessionId + "|";
        foreach (var key in _pendingGetCoursePlayers.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            _pendingGetCoursePlayers.TryRemove(key, out _);
        foreach (var key in _pendingHlsManifests.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            _pendingHlsManifests.TryRemove(key, out _);
        foreach (var key in _networkRequestContexts.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            _networkRequestContexts.TryRemove(key, out _);
    }

    private void QueueMediaCandidate(MediaCandidate candidate, long? expectedGeneration = null)
    {
        if (!MediaCandidatePolicy.CanQueue(candidate)) return;
        var generation = expectedGeneration ?? Volatile.Read(ref _browserDiscoveryGeneration);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration)
                || _mediaBrowser?.CoreWebView2 is null || _browserPageUri is null || _seenMedia.Count >= 200) return;

            var key = candidate.Source.AbsoluteUri;
            if (_mediaCandidateItems.TryGetValue(key, out var previousItem) && previousItem.Tag is MediaCandidate previous)
                candidate = MediaCandidateMerge.Merge(previous, candidate);
            candidate = BrowserFrameBindingResolver.Bind(candidate, _browserFrames, _browserMetadata);
            if (candidate.HlsManifest is { IsMaster: true })
                DiagnosticHub.Log.Write("browser.binding.candidate", "observed",
                    $"source={candidate.Source.IdnHost} referer={BrowserBindingFingerprint.Describe(candidate.Referer)} ordinal={candidate.PageOrdinal?.ToString() ?? "none"}");
            var known = _mediaCandidateItems.Values
                .Select(item => item.Tag as MediaCandidate)
                .Where(item => item is not null)
                .Cast<MediaCandidate>()
                .ToArray();
            if (MediaCandidatePresentation.IsTechnicalChild(candidate, known)) return;

            if (candidate.HlsManifest is { IsMaster: true })
            {
                var technical = _mediaCandidateItems
                    .Where(pair => pair.Value.Tag is MediaCandidate existingCandidate
                        && MediaCandidatePresentation.IsTechnicalChild(existingCandidate, [candidate]))
                    .Select(pair => pair.Key).ToArray();
                foreach (var technicalKey in technical)
                {
                    if (!_mediaCandidateItems.Remove(technicalKey, out var technicalItem)) continue;
                    _mediaCandidatesBox.Items.Remove(technicalItem);
                    _seenMedia.Remove(technicalKey);
                }
            }

            if (_mediaCandidateItems.TryGetValue(key, out var existing))
            {
                var index = Math.Max(1, _mediaCandidatesBox.Items.IndexOf(existing) + 1);
                existing.Content = MediaCandidatePresentation.DisplayName(candidate, index, _browserMetadata);
                existing.Tag = candidate;
                SyncQueuedCandidate(candidate);
                RebindAndReorderMediaCandidates();
                _ = EnrichMediaDurationAsync(candidate, generation);
                return;
            }
            if (!_seenMedia.Add(key)) return;
            var currentCandidates = _mediaCandidatesBox.Items.OfType<Microsoft.UI.Xaml.Controls.ComboBoxItem>()
                .Select(item => item.Tag as MediaCandidate).Where(item => item is not null).Cast<MediaCandidate>().ToArray();
            var insertIndex = MediaCandidatePresentation.GetInsertIndex(candidate, currentCandidates);
            var ordinal = candidate.PageOrdinal ?? insertIndex + 1;
            var displayName = MediaCandidatePresentation.DisplayName(candidate, ordinal, _browserMetadata);
            var item = new Microsoft.UI.Xaml.Controls.ComboBoxItem { Content = displayName, Tag = candidate };
            _mediaCandidateItems[key] = item;
            _mediaCandidatesBox.Items.Insert(insertIndex, item);
            if (_mediaCandidatesBox.SelectedIndex < 0) _mediaCandidatesBox.SelectedIndex = 0;
            _browserHint.Text = "Найдено видео: " + _mediaCandidatesBox.Items.Count + ". Выберите видео и качество; запускать его вручную не требуется.";
            DiagnosticHub.Log.Write("browser.discovery", "succeeded", displayName);
            _ = EnrichMediaDurationAsync(candidate, generation);
        });
    }

    private void RemoveMediaCandidatesReferencing(Uri source, long generation)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
            var remove = _mediaCandidateItems
                .Where(pair => pair.Value.Tag is MediaCandidate candidate
                    && (candidate.Source == source
                        || candidate.HlsManifest?.Variants.Any(v => v.Uri == source) == true
                        || candidate.HlsManifest?.AudioRenditions.Any(a => a.Uri == source) == true))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var key in remove)
            {
                if (!_mediaCandidateItems.Remove(key, out var item)) continue;
                _mediaCandidatesBox.Items.Remove(item);
                _seenMedia.Remove(key);
            }
        });
    }

    private async Task RefreshBrowserFrameTreeAsync(CoreWebView2 core)
    {
        try
        {
            var json = await core.CallDevToolsProtocolMethodAsync("Page.getFrameTree", "{}");
            if (DevToolsFrameTreeParser.TryParse(json, out var frames) && frames is not null)
                _browserFrames = frames;
            RebindAndReorderMediaCandidates();
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("browser.frames", "failed", ex.GetType().Name);
        }
    }

    private void RebindAndReorderMediaCandidates()
    {
        var generation = Volatile.Read(ref _browserDiscoveryGeneration);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != Volatile.Read(ref _browserDiscoveryGeneration)) return;
            var rawEntries = _mediaCandidatesBox.Items.OfType<Microsoft.UI.Xaml.Controls.ComboBoxItem>()
                .Select((item, index) => (Item: item, Index: index, Candidate: item.Tag as MediaCandidate))
                .Where(entry => entry.Candidate is not null)
                .ToArray();
            var boundCandidates = BrowserFrameBindingResolver.BindAll(
                rawEntries.Select(entry => entry.Candidate!).ToArray(), _browserFrames, _browserMetadata);
            var entries = rawEntries
                .Select((entry, index) => (entry.Item, entry.Index, Candidate: boundCandidates[index]))
                .OrderBy(entry => entry.Candidate.PageOrdinal ?? int.MaxValue)
                .ThenBy(entry => entry.Index)
                .ToArray();
            var selectedSource = (_mediaCandidatesBox.SelectedItem as Microsoft.UI.Xaml.Controls.ComboBoxItem)?.Tag is MediaCandidate selected
                ? selected.Source : null;
            var selection = MediaCandidateSelectionReducer.Reduce(
                entries.Select(entry => entry.Candidate).ToArray(), selectedSource, _mediaQualitySelections);
            var wasUpdating = _updatingMediaQuality;
            _updatingMediaQuality = true;
            try
            {
                _mediaCandidatesBox.Items.Clear();
                foreach (var candidate in selection.Candidates)
                {
                    var item = _mediaCandidateItems[candidate.Source.AbsoluteUri];
                    item.Tag = candidate;
                    item.Content = MediaCandidatePresentation.DisplayName(candidate, _mediaCandidatesBox.Items.Count + 1, _browserMetadata);
                    _mediaCandidatesBox.Items.Add(item);
                    SyncQueuedCandidate(candidate);
                }
                _mediaCandidatesBox.SelectedItem = _mediaCandidatesBox.Items.OfType<Microsoft.UI.Xaml.Controls.ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag is MediaCandidate value
                        && string.Equals(value.Source.AbsoluteUri, selection.SelectedSource?.AbsoluteUri, StringComparison.Ordinal));
            }
            finally { _updatingMediaQuality = wasUpdating; }
            SyncMediaQualityChoices(selection.SelectedQuality);
            RefreshDownloadQueueList();
        });
    }

    private void ResetDevToolsDiscoveryForNavigation(bool clearUi = true)
    {
        Interlocked.Increment(ref _browserDiscoveryGeneration);
        _pendingGetCoursePlayers.Clear();
        _pendingHlsManifests.Clear();
        _networkRequestContexts.Clear();
        _verifiedClearHls.Clear();
        _browserFrames = [];
        _seenMedia.Clear();
        _mediaCandidateItems.Clear();
        _mediaQualitySelections.Clear();
        _browserDownloadQueue.Clear();
        _playerMasterBindings.Clear();
        _browserMetadata = BrowserPageMetadata.Empty;
        if (clearUi) _mediaCandidatesBox.Items.Clear();
        RefreshDownloadQueueList();
    }

    private static string RequestKey(string? sessionId, string requestId)
        => (sessionId ?? string.Empty) + "|" + requestId;

    private void DisableDevToolsMediaDiscovery(bool clearUi = true)
    {
        try
        {
            if (_networkResponseReceiver is not null && _networkResponseHandler is not null)
                _networkResponseReceiver.DevToolsProtocolEventReceived -= _networkResponseHandler;
            if (_networkRequestReceiver is not null && _networkRequestHandler is not null)
                _networkRequestReceiver.DevToolsProtocolEventReceived -= _networkRequestHandler;
            if (_networkLoadingFinishedReceiver is not null && _networkLoadingFinishedHandler is not null)
                _networkLoadingFinishedReceiver.DevToolsProtocolEventReceived -= _networkLoadingFinishedHandler;
            if (_networkLoadingFailedReceiver is not null && _networkLoadingFailedHandler is not null)
                _networkLoadingFailedReceiver.DevToolsProtocolEventReceived -= _networkLoadingFailedHandler;
            if (_targetAttachedReceiver is not null && _targetAttachedHandler is not null)
                _targetAttachedReceiver.DevToolsProtocolEventReceived -= _targetAttachedHandler;
            if (_targetDetachedReceiver is not null && _targetDetachedHandler is not null)
                _targetDetachedReceiver.DevToolsProtocolEventReceived -= _targetDetachedHandler;
        }
        catch (Exception ex) { DiagnosticHub.Log.Write("browser.devtools", "failed", ex.GetType().Name); }
        finally
        {
            _networkResponseReceiver = null;
            _networkRequestReceiver = null;
            _networkResponseHandler = null;
            _networkRequestHandler = null;
            _networkLoadingFinishedReceiver = null; _networkLoadingFinishedHandler = null;
            _networkLoadingFailedReceiver = null; _networkLoadingFailedHandler = null;
            _targetAttachedReceiver = null;
            _targetAttachedHandler = null;
            _targetDetachedReceiver = null;
            _targetDetachedHandler = null;
            _devToolsSessions.Clear();
            ResetDevToolsDiscoveryForNavigation(clearUi);
        }
    }
}
