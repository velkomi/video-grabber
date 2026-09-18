using System.Collections.Concurrent;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class BrowserWiringRegressionTests
{
    // Supplemental wiring check; production reducer/merge behavior is tested directly.
    [Fact]
    public void Candidate_rebuild_uses_production_selection_and_merge_with_event_suppression()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        Assert.Contains("MediaCandidateSelectionReducer.Reduce(", devtools);
        Assert.Contains("MediaCandidateMerge.Merge(previous, candidate)", devtools);
        Assert.Contains("foreach (var candidate in selection.Candidates)", devtools);
        Assert.Contains("finally { _updatingMediaQuality = wasUpdating; }", devtools);
        Assert.Contains("if (_updatingMediaQuality) return;", browser);
        Assert.Contains("SyncMediaQualityChoices(selection.SelectedQuality)", devtools);
        Assert.DoesNotContain("return (_mediaQualityBox.SelectedItem", batch);
    }

    [Fact]
    public void Browser_download_does_not_force_private_session_and_resolves_hls_plan_at_click_time()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var preparation = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "BrowserDownloadPreparation.cs"));
        Assert.DoesNotContain("forceEmbeddedSession: true", browser);
        Assert.DoesNotContain("forceEmbeddedSession", preparation);
        Assert.Contains("MediaDownloadPlanResolver.Resolve(candidate", preparation);
        Assert.Contains("BrowserDownloadSessionPolicy.UseEmbeddedSession(intent.CookieSelection)", preparation);
    }

    [Fact]
    public void Navigation_and_target_lifecycle_have_full_reset_and_guaranteed_resume()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("ResetDevToolsDiscoveryForNavigation();", browser);
        Assert.Contains("DevToolsTargetEventParser.TryParseSessionId", devtools);
        Assert.Contains("finally", devtools);
        Assert.Contains("Runtime.runIfWaitingForDebugger", devtools);
    }

    [Fact]
    public void GetCourse_master_fallback_is_queued_with_lesson_referer()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.DoesNotContain("new MediaCandidate(playlist, candidate.Source", browser);
        Assert.Contains("new MediaCandidate(playlist, player.Referer", devtools);
        Assert.Contains("QueueMediaCandidate", devtools);
    }

    [Fact]
    public void DevTools_loading_failed_cleans_all_pending_request_state()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("Network.loadingFailed", devtools);
        Assert.Contains("OnDevToolsLoadingFailed", devtools);
        Assert.Contains("_pendingGetCoursePlayers.TryRemove(key", devtools);
        Assert.Contains("_pendingHlsManifests.TryRemove(key", devtools);
        Assert.Contains("_networkRequestContexts.TryRemove(key", devtools);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.App"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Detached_target_and_direct_download_route_session_are_cleaned_up()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var preparation = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "BrowserDownloadPreparation.cs"));
        Assert.Contains("Target.detachedFromTarget", devtools);
        Assert.Contains("OnDevToolsTargetDetached", devtools);
        Assert.Contains("RemoveSessionScopedPending", devtools);
        Assert.Contains("new DownloadRouteScope(window._routePolicy", preparation);
        Assert.Contains("routeScope.Dispose();", preparation);
        Assert.DoesNotContain("WouldConfigureSession", preparation);
        Assert.Contains("EnsureDownloadEgress(selected.Source, selected.Referer, routeScope)", preparation);
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        Assert.Contains("EnsureDownloadEgress(source, candidate.Referer, routeScope)", preflight);
        Assert.Contains("EnsureDownloadEgress(variant.Uri, candidate.Referer, durationRouteScope)", preflight);
        Assert.DoesNotContain("_routePolicy.ClearSession();", preparation);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Browser_blocks_encrypted_hls_before_queueing_candidate()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("HlsDownloadPolicy.IsAllowed", devtools);
        Assert.Contains("Target.detachedFromTarget", devtools);
        Assert.Contains("RemoveSessionScopedPending", devtools);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Route_mode_switch_destroys_browser_before_reconfiguring_session()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var marker = "private async void OpenBrowser_Click";
        var start = browser.IndexOf(marker, StringComparison.Ordinal);
        var end = browser.IndexOf("private async void Browser_WebResourceResponseReceived", start, StringComparison.Ordinal);
        var method = browser[start..end];
        var destroy = method.IndexOf("DestroyBrowser();", StringComparison.Ordinal);
        var ensure = method.IndexOf("EnsureRoutingProxy(uri)", StringComparison.Ordinal);
        Assert.True(destroy >= 0 && ensure >= 0 && destroy < ensure,
            "When route mode changes, DestroyBrowser must run before EnsureRoutingProxy configures the new session.");
    }

    [Fact]
    public void Media_candidate_queue_uses_central_HLS_safety_policy()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("MediaCandidatePolicy.CanQueue(candidate)", devtools);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Direct_manifest_and_routed_browser_ownership_are_wired_through_UI()
    {
        var root = FindRepoRoot();
        var preparation = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "BrowserDownloadPreparation.cs"));
        Assert.Contains("plan.DirectManifest", preparation);
        Assert.Contains("new PreparedDownload(plan.Source", preparation);
        Assert.Contains("new PreparedBrowserDownload(selected with", preparation);
        var service = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.Infrastructure", "Browser", "BrowserDownloadOperation.cs"));
        Assert.Contains("DownloadRequestFactory.Create(intent, prepared.Values)", service);
        var ui = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.Contains("service.RunAsync(intent, lease, operation.Token)", ui);
        Assert.Contains("_browserUsesSiteRoutes", preparation);
    }

    [Fact]
    public void Browser_route_mode_uses_exact_proxy_requirement_not_only_provider_session()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        Assert.Contains("DownloadRouteResolver.RequiresProxy", browser);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Selected_child_HLS_tracks_require_verified_clear_manifests_before_download()
    {
        var root = FindRepoRoot();
        var preparation = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "BrowserDownloadPreparation.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("EnsureSelectedHlsVerifiedAsync", preparation);
        Assert.Contains("HlsDownloadPolicy.AreSelectedTracksVerified", preflight);
        Assert.Contains("_verifiedClearHls", devtools);
        Assert.Contains("pending.Response.Source.AbsoluteUri", devtools);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void WebResource_fallback_parses_HLS_body_before_queueing()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        Assert.Contains("HlsResponseCandidateResolver.TryResolve", browser);
        Assert.Contains("_verifiedClearHls", browser);
    }

    [Fact]
    public void Window_close_uses_shutdown_safe_browser_cleanup()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        Assert.Contains("DestroyBrowser(forWindowClose: true)", shell);
        Assert.Contains("forWindowClose", browser);
    }
}
public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Navigation_captures_frame_tree_and_rebinds_candidates_to_page_order()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));
        Assert.Contains("RefreshBrowserBindingsAsync", browser);
        Assert.Contains("BrowserFrameBindingResolver.Bind", devtools);
        Assert.Contains("RebindAndReorderMediaCandidates", devtools);
        Assert.Contains("playerSlots", metadata);
        Assert.Contains("source: frame.src", metadata);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public async Task Delayed_page_reads_and_dispatched_merge_cannot_change_new_page_models()
    {
        using var pages = new BrowserPageLifetime();
        var old = pages.Capture();
        var browser = new object();
        var capturedBrowser = browser;
        var metadataRead = new TaskCompletionSource<BrowserPageMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
        var frameRead = new TaskCompletionSource<IReadOnlyList<DevToolsFrameInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeRead = new TaskCompletionSource<HlsPreflightFetchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Uri("https://fixture.invalid/master.m3u8");
        var leaf = new Uri("https://fixture.invalid/video.m3u8");
        var oldMetadata = new BrowserPageMetadata("A", ["Part A"]);
        var metadata = oldMetadata;
        IReadOnlyList<DevToolsFrameInfo> frames = [new("A", source, 1)];
        var candidate = new MediaCandidate(source, new Uri("https://fixture.invalid/page-b"), "HLS",
            PageOrdinal: 2, PageSectionTitle: "Part B");
        var queue = new BrowserDownloadQueue();
        var verified = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var selected = MediaCandidateSelectionReducer.Reduce([candidate], source,
            new Dictionary<string, string> { [source.AbsoluteUri] = "720p" });
        Action oldDispatch = () => pages.TryApply(old, () => ReferenceEquals(capturedBrowser, browser), () =>
        {
            var rebound = candidate with { PageOrdinal = 1, PageSectionTitle = "Part A" };
            queue.AddOrUpdate(rebound, 1, "360p");
            selected = MediaCandidateSelectionReducer.Reduce([rebound], source,
                new Dictionary<string, string> { [source.AbsoluteUri] = "360p" });
        });
        var metadataTask = pages.ReadAndApplyAsync(old, () => metadataRead.Task,
            () => ReferenceEquals(capturedBrowser, browser), value => metadata = value);
        var frameTask = pages.ReadAndApplyAsync(old, () => frameRead.Task,
            () => ReferenceEquals(capturedBrowser, browser), value => frames = value);
        var probeTask = pages.ReadAndApplyAsync(old, () => probeRead.Task,
            () => ReferenceEquals(capturedBrowser, browser), value => HlsDownloadPolicy.RecordDurationProbeResult(verified, leaf, value));
        verified[leaf.AbsoluteUri] = 0;
        pages.Reset();
        verified.Clear();
        var newMetadata = new BrowserPageMetadata("B", ["Part B"]);
        metadata = newMetadata;
        IReadOnlyList<DevToolsFrameInfo> newFrames = [new("B", source, 2)];
        frames = newFrames;
        queue.AddOrUpdate(candidate, 2, "720p");
        Assert.True(HlsManifestParser.TryParse("#EXTM3U\n#EXTINF:12,\nsegment.ts\n#EXT-X-ENDLIST", leaf, out var info));
        metadataRead.SetResult(oldMetadata);
        frameRead.SetResult([new("A", source, 1)]);
        probeRead.SetResult(new(true, false, info));
        Assert.False(await metadataTask);
        Assert.False(await frameTask);
        Assert.False(await probeTask);
        oldDispatch();
        Assert.Same(newMetadata, metadata);
        Assert.Same(newFrames, frames);
        Assert.Equal(source, selected.SelectedSource);
        Assert.Equal("720p", selected.SelectedQuality);
        Assert.Equal("Part B", Assert.Single(selected.Candidates).PageSectionTitle);
        var queued = Assert.Single(queue.Items);
        Assert.Equal(candidate, queued.Candidate);
        Assert.Equal(2, queued.Ordinal);
        Assert.Equal("720p", queued.Quality);
        Assert.Empty(verified);
        Assert.True(pages.TryApply(pages.Capture(), () => true,
            () => HlsDownloadPolicy.RecordDurationProbeResult(verified, leaf, new(true, false, info))));
        Assert.Single(verified);
    }

    [Fact]
    public void Browser_async_wiring_uses_tested_lifetime_seams_and_generation_scoped_probe_keys()
    {
        // Supplemental only: the lifetime, delayed reads and actual model behavior are tested above.
        var root = FindRepoRoot();
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        Assert.Contains("_browserPages.ReadAndApplyAsync(lease", metadata);
        Assert.Contains("_browserPages.ReadAndApplyAsync(lease", devtools);
        Assert.Contains("_browserPages.TryApply(lease", metadata);
        Assert.Contains("Task.Delay(350, lease.Token)", metadata);
        Assert.Contains("_browserPages.Reset();", devtools);
        Assert.Contains("_verifiedClearHls.Clear();", devtools);
        Assert.Contains("_browserPages.Dispose();", browser);
        Assert.Contains("args.NavigationId != _browserNavigationId", browser);
        Assert.Contains("_browserPages.RunProbeAsync(lease", preflight);
        Assert.Contains("var probeKey = (lease.Generation, key);", preflight);
        Assert.Contains("_durationProbeInFlight.TryRemove(probeKey", preflight);
        Assert.Contains("CookieManager.GetCookiesAsync(source.AbsoluteUri)", preflight);
        Assert.Contains("BuildBrowserCookieHeaderAsync(uri, lease, core", preflight);
        Assert.Contains("ReferenceEquals(browser, _mediaBrowser?.CoreWebView2)", preflight);
        Assert.Contains("CreateLinkedTokenSource(cancellationToken, lease.Token)", preflight);
        Assert.DoesNotContain("RefreshBrowserFrameTreeAsync(core);", metadata);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Network_response_handlers_resolve_recorded_start_identity()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var response = devtools[devtools.IndexOf("private void OnDevToolsResponse", StringComparison.Ordinal)..
            devtools.IndexOf("private void OnDevToolsRequest", StringComparison.Ordinal)];
        var webResponse = browser[browser.IndexOf("private async void Browser_WebResourceResponseReceived", StringComparison.Ordinal)..
            browser.IndexOf("private static async Task<string> ReadLimitedTextAsync", StringComparison.Ordinal)];
        Assert.DoesNotContain("_browserPages.Capture()", response);
        Assert.DoesNotContain("_browserPages.Capture()", webResponse);
        Assert.Contains("TryGetRequestLease(RequestKey(sessionId, requestId), loaderId", response);
        Assert.Contains("RememberRequest(RequestKey(sessionId, requestId), loaderId, lease)", devtools);
        Assert.Contains("core.WebResourceRequested += Browser_WebResourceRequested", browser);
        Assert.Contains("RememberRequest(request, string.Empty, _browserPages.Capture())", browser);
        Assert.Contains("TryGetRequestLease(request, string.Empty, out var lease)", webResponse);
        Assert.Contains("_browserPages.ForgetRequest(key, lease)", devtools);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Whole_course_download_is_a_separate_browser_action()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));

        Assert.Contains("SectionHeading(\"Весь курс GetCourse\")", browser);
        Assert.Contains("PrimaryButton(\"Скачать весь курс\")", browser);
        Assert.Contains("DownloadWholeGetCourseAsync()", browser);
        Assert.DoesNotContain("DownloadAllVisibleCandidatesAsync()", course);
        Assert.Contains("BuildCoursePlanAsync", course);
        Assert.Contains("DownloadCoursePlanAsync", course);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Current_page_video_workflow_remains_available_beside_whole_course_action()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var batch = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));

        Assert.Contains("Найденные видео и потоки", browser);
        Assert.Contains("Качество выбранного видео", browser);
        Assert.Contains("Скачать выбранное видео", browser);
        Assert.Contains("Добавить в очередь", browser);
        Assert.Contains("Скачать все найденные", browser);
        Assert.Contains("QueueSelectedCandidate()", browser);
        Assert.Contains("DownloadAllVisibleCandidatesAsync()", browser);
        Assert.Contains("DownloadQueuedCandidatesAsync()", browser);
        Assert.Contains("SelectedBrowserQuality(candidate)", browser);
        Assert.Contains("QueueAllVisibleCandidates()", batch);
        Assert.Contains("RunDownloadOperationAsync", batch);
        Assert.Contains("Весь курс GetCourse", browser);
        Assert.Contains("Скачать весь курс", browser);
    }
}
