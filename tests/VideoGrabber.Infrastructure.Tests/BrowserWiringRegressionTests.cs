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
        Assert.Contains("player.Referer", devtools);
        Assert.Contains("QueueMediaCandidate(new MediaCandidate(", devtools);
        Assert.Contains("QueueMediaCandidate", devtools);
    }

    [Fact]
    public void DevTools_loading_failed_retains_GetCourse_player_retry_but_cleans_other_pending_state()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("Network.loadingFailed", devtools);
        Assert.Contains("OnDevToolsLoadingFailed", devtools);
        Assert.Contains("_pendingGetCoursePlayers.ContainsKey(key)", devtools);
        Assert.Contains("response-body retry retained", devtools);
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

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Whole_course_navigation_waits_through_GetCourse_redirect_abort()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App",
            "MainWindow.CourseDownload.cs"));

        Assert.Contains(
            "CoreWebView2WebErrorStatus.ConnectionAborted",
            course);
        Assert.Contains(
            "Transient redirect navigation",
            course);
        Assert.Contains(
            "completion.TrySetResult(args);",
            course);
    }
}
public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void GetCourse_player_response_starts_body_retry_without_waiting_for_loading_finished()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var responseStart = devtools.IndexOf("private void OnDevToolsResponse", StringComparison.Ordinal);
        var requestStart = devtools.IndexOf("private void OnDevToolsRequest", responseStart, StringComparison.Ordinal);
        var response = devtools[responseStart..requestStart];
        Assert.Contains("ResolveGetCoursePlayerWithRetryAsync", response);
        Assert.Contains("_playerBodyResolvers.TryAdd", devtools);
        Assert.Contains("attempt < 40", devtools);
        Assert.Contains("Network.getResponseBody", devtools);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Whole_course_archives_page_before_media_and_no_video_is_not_a_lesson_failure()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var archive = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.CourseArchive.cs"));
        var save = course.IndexOf("SaveCourseLessonArchiveAsync", StringComparison.Ordinal);
        var media = course.IndexOf("WaitForCourseMediaAsync", save, StringComparison.Ordinal);
        Assert.True(save >= 0 && media > save);
        Assert.Contains("Lesson archived without downloadable video", course);
        Assert.Contains("CourseLessonArchive.WriteDocx", archive);
        Assert.Contains("CookieManager", archive);
        Assert.Contains("Страница.html", archive);
        Assert.Contains("Изображения", archive);
        Assert.Contains("Вложения", archive);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Whole_course_prefers_dom_player_resolver_and_uses_direct_routed_assets()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var media = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseMedia.cs"));
        var archive = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseArchive.cs"));

        var domResolve = course.IndexOf(
            "DiscoverCourseMediaFromDomAsync", StringComparison.Ordinal);
        var fallback = course.IndexOf(
            "WaitForCourseMediaAsync", domResolve, StringComparison.Ordinal);
        Assert.True(domResolve >= 0 && fallback > domResolve);

        Assert.Contains("data-iframe-src", media);
        Assert.Contains("GetCoursePlayerConfigParser.TryExtractMasterPlaylist", media);
        Assert.Contains("HlsManifestParser.TryParse", media);
        Assert.Contains("new RouteConnector(_routePolicy)", media);
        Assert.Contains("ConnectCallback", media);

        Assert.Contains(".lite-page.block-set", archive);
        Assert.Contains(".o-lt-lesson-comment-block", archive);
        Assert.Contains("style,link,script", archive);
        Assert.Contains("new RouteConnector(_routePolicy)", archive);
        Assert.DoesNotContain("new WebProxy(", archive);
    }
}


public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Course_ui_exposes_overall_progress_resume_cache_and_global_cancel()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var main = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));

        Assert.Contains("Продолжить", browser);
        Assert.Contains("Очистить кэш / временные файлы", browser);
        Assert.Contains("_courseProgressTrack = new Grid", browser);
        Assert.Contains("_courseProgressFill = new Border", browser);
        Assert.DoesNotContain("new ProgressBar", browser);
        Assert.Contains("Этап 1 из 2", course);
        Assert.Contains("Этап 2 из 2", course);
        Assert.Contains("ориентировочно", course);
        Assert.Contains("Прошло с начала", course);
        Assert.Contains("StartCourseElapsedTimer", course);
        Assert.Contains("RecoverCompletedCourseJobsAsync", course);
        Assert.Contains("DangerButton(\"Отменить всё\")", main);
        Assert.Contains("_courseCancellation?.Cancel()", File.ReadAllText(
            Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs")));
    }
}


public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Course_crash_resume_is_persistent_and_failed_video_is_retried()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var preparation = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "BrowserDownloadPreparation.cs"));
        var downloader = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure",
            "Downloads", "YtDlpDownloader.cs"));

        Assert.Contains("VideoGrabber.course.json", File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure",
            "Browser", "CourseDownloadStateStore.cs")));
        Assert.Contains("PersistCourseStateAsync", course);
        Assert.Contains("LoadCourseResumeProjectAsync", course);
        Assert.Contains("Продолжить / открыть папку курса", File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs")));
        Assert.Contains("StableMediaResumeKey", course);
        Assert.Contains("resumeKeyOverride", preparation);
        Assert.Contains("request.ResumeKey", downloader);
        Assert.Contains("\"--continue\"", downloader);

        Assert.Contains("const int maxAttempts = 4", course);
        Assert.Contains("course.video.retry", course);
        Assert.Contains("DiscoverCourseMediaFromDomAsync", course);
        Assert.Contains("Обновляю ссылку", course);
    }
}


public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Course_resume_matches_the_opened_course_and_browser_guidance_is_explicit()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var shell = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));

        Assert.Contains("ResolveCourseResumeStateAsync", course);
        Assert.Contains("RequestedCourseRoot", course);
        Assert.Contains("Выбрана папка другого курса", course);
        Assert.Contains("Directory.EnumerateDirectories", course);

        Assert.Contains("сначала войдите в свой аккаунт", browser);
        Assert.Contains("Прокрутите страницу вниз", browser);
        Assert.Contains("BrowserActionButton", shell);
        Assert.Contains("24, 94, 61", shell);
        Assert.Contains("прокрутите эту страницу ниже", shell);
    }
}


public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Whole_course_automatically_recovers_transient_network_failures()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));

        Assert.Contains("DownloadCoursePlanWithAutomaticRecoveryAsync", course);
        Assert.Contains("course.auto-resume", course);
        Assert.Contains("Автопродолжение через", course);
        Assert.Contains("IsTransientCourseFailure", course);
        Assert.Contains("HttpStatusCode.Unauthorized", course);
        Assert.Contains("HttpStatusCode.Forbidden", course);
        Assert.Contains("5, 10, 20, 30, 60, 120", course);
        Assert.Contains("name.EndsWith(", course);
        Assert.Contains("\".ytdl\"", course);
    }
}


public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Network_ui_prefers_portable_auto_route_and_GetCourse_CDN_family()
    {
        var root = FindRepoRoot();
        var network = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Network.cs"));
        var profiles = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure", "Networking", "SiteRouteProfiles.cs"));
        var connector = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure", "Networking", "RouteConnector.cs"));

        Assert.Contains("Авто — физический интернет", network);
        Assert.Contains("AutoPhysicalAdapterId", network);
        Assert.Contains("servicecdn.ru", profiles);
        Assert.Contains("AutoPhysicalAdapterId = \"auto-physical\"", connector);
        Assert.Contains("system-fallback", connector);
        Assert.Contains("LooksVirtualOrVpn", connector);
    }
}

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Pause_button_has_stable_size_and_running_yellow_state()
    {
        var root = FindRepoRoot();
        var main = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        Assert.Contains("button.Width = 145;", main);
        Assert.Contains("button.MinWidth = 145;", main);
        Assert.Contains("ColorHelper.FromArgb(255, 250, 204, 21)", main);
        Assert.Contains("button.Content = \"▶  Продолжить\"", main);
        Assert.Contains("ColorHelper.FromArgb(255, 22, 163, 74)", main);
    }
}
public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Bundled_runtime_and_transcription_are_wired_without_runtime_installer()
    {
        var root = FindRepoRoot();
        var main = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        var media = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.MediaActions.cs"));
        Assert.Contains("initialTools.UsesBundledRuntime", main);
        Assert.DoesNotContain("var install = PrimaryButton(\"Установить или обновить\")", main);
        Assert.DoesNotContain("InstallComponentsAsync(forceUpdate: false", download);
        Assert.Contains("components.Tools.WhisperCli", media);
        Assert.Contains("components.Tools.WhisperModel", media);
        Assert.Contains("Horizontal(_mp3Button, _textButton, mediaPauseButton, cancel)", media);
    }
}public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Course_completion_is_disk_authoritative_before_progress_reaches_100_percent()
    {
        var root = FindRepoRoot();
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));

        Assert.Contains("out var incompleteReason", course);
        Assert.Contains("course.lesson-verify", course);
        Assert.Contains("diskCompleted = new HashSet<string>", course);
        Assert.Contains("_courseCompletedLessons.Clear();", course);
        Assert.Contains("_courseCompletedLessons.UnionWith(diskCompleted);", course);
    }

    [Fact]
    public void Desktop_shell_uses_compact_navigation_logo_visible_scrollbar_and_wrapped_actions()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var browser = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));

        Assert.Contains("VideoGrabber.png", shell);
        Assert.DoesNotContain("локально на вашем ПК", shell);
        Assert.DoesNotContain("Встроенные инструменты", shell);
        Assert.Contains("\"Настройки\"", shell);
        Assert.Contains("new FontFamily(\"Segoe UI\")", shell);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Visible", shell);
        Assert.DoesNotContain("ScrollBarThumbBackground", shell);
        Assert.Contains("ResponsiveActions", browser);
        Assert.Contains("_courseNetworkWarning = new Border", browser);
        Assert.DoesNotContain("_courseNetworkWarning = new InfoBar", browser);
    }

    [Fact]
    public void Download_page_keeps_brand_pause_persistence_and_completion_actions()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var preferences = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.DownloadPreferences.cs"));
        var media = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.MediaActions.cs"));
        var download = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        var queue = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var course = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.CourseDownload.cs"));

        Assert.Contains("Child = new Image", shell);
        Assert.Contains("BrandLogoAssetPath", shell);
        Assert.Contains("После завершения всех загрузок", shell);
        Assert.Contains("button.IsHitTestVisible = busy", shell);
        Assert.Contains("⏸  Пауза", shell);

        Assert.Contains(@"D:\VideoGrabber", preferences);
        Assert.Contains(@"C:\VideoGrabber", preferences);
        Assert.Contains("SetSuspendState", preferences);
        Assert.Contains("shutdown.exe", preferences);
        Assert.Contains("CompletionAction", media);
        Assert.Contains("DownloadFolder", media);

        Assert.Contains("ScheduleCompletionActionAfterDownloads(\"direct\")", download);
        Assert.Contains("ScheduleCompletionActionAfterDownloads(\"queue\")", queue);
        Assert.Contains("ScheduleCompletionActionAfterDownloads(\"course\")", course);

        var png = new FileInfo(Path.Combine(
            root, "src", "VideoGrabber.App", "Assets", "VideoGrabber.png"));
        var ico = new FileInfo(Path.Combine(
            root, "src", "VideoGrabber.App", "Assets", "VideoGrabber.ico"));
        Assert.True(png.Exists && png.Length > 500_000);
        Assert.True(ico.Exists && ico.Length > 5_000);
    }

    [Fact]
    public void Auto_route_proxy_reports_transport_failures_for_alternate_route_selection()
    {
        var root = FindRepoRoot();
        var proxy = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure", "Networking", "SiteRouteProxy.cs"));
        var routes = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Infrastructure", "Networking", "DownloadRouteResolver.cs"));
        var mainNetwork = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Network.cs"));

        Assert.Contains("_onTransportFailure?.Invoke(targetHost, ex)", proxy);
        Assert.Contains("onTransportFailure: connector.ReportTransportFailure", routes);
        Assert.Contains("onTransportFailure: connector.ReportTransportFailure", mainNetwork);
    }
}
