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
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.DoesNotContain("forceEmbeddedSession: true", browser);
        Assert.DoesNotContain("forceEmbeddedSession", download);
        Assert.Contains("MediaDownloadPlanResolver.Resolve(candidate", batch);
        Assert.Contains("BrowserDownloadSessionPolicy.UseEmbeddedSession(selectedCookies)", download);
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
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.Contains("Target.detachedFromTarget", devtools);
        Assert.Contains("OnDevToolsTargetDetached", devtools);
        Assert.Contains("RemoveSessionScopedPending", devtools);
        Assert.Contains("new DownloadRouteScope(_routePolicy", download);
        Assert.Contains("routeScope.Dispose();", download);
        Assert.DoesNotContain("WouldConfigureSession", download);
        Assert.Contains("EnsureRoutingProxy(selected.Source, selected.Referer, routeScope)", download);
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        Assert.Contains("EnsureRoutingProxy(source, candidate.Referer, routeScope)", preflight);
        Assert.Contains("EnsureRoutingProxy(variant.Uri, candidate.Referer)", preflight);
        Assert.DoesNotContain("_routePolicy.ClearSession();", download);
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
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.Contains("plan.DirectManifest", batch);
        Assert.Contains("new PreparedDownload(plan.Source", batch);
        Assert.Contains("DownloadPreparedSourceAsync(intent, selected, operationToken, routeScope)", batch);
        Assert.Contains("DownloadRequestFactory.PrepareAsync(intent", download);
        Assert.Contains("DownloadSourceAsync(UserDownloadIntent intent, CancellationToken operationToken)", download);
        Assert.Contains("_browserUsesSiteRoutes", download);
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
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.HlsPreflight.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        Assert.Contains("EnsureSelectedHlsVerifiedAsync", batch);
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
