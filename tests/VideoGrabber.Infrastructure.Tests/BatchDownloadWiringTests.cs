namespace VideoGrabber.Infrastructure.Tests;

public sealed class BatchDownloadWiringTests
{
    [Fact]
    public void Batch_keeps_explicit_cookie_mode_until_queue_finishes()
    {
        var root = FindRepoRoot();
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));

        Assert.Contains("resetCookieSelectionAfterUse: false", batch);
        Assert.Contains("_cookiesBox.SelectedIndex = 0", batch);
        Assert.Contains("resetCookieSelectionAfterUse", download);
    }

    [Fact]
    public void Batch_cancel_stops_entire_queue_instead_of_starting_next_item()
    {
        var root = FindRepoRoot();
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.Contains("OperationOutcome.Cancelled", batch);
        Assert.Contains("return;", batch);
        Assert.Contains("service.RunAsync(intent, lease, operation.Token)", download);
        Assert.Contains("installCompletion = _operations.Complete(installOutcome)", download);
        Assert.DoesNotContain("CompleteOperation(_operations.Complete(", download);
    }

    [Fact]
    public void Browser_has_separate_per_video_quality_selector_and_editable_queue()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        Assert.Contains("_mediaQualityBox", browser);
        Assert.Contains("_downloadQueueList", browser);
        Assert.Contains("MoveQueuedCandidate", batch);
        Assert.Contains("RemoveQueuedCandidate", batch);
        Assert.Contains("DownloadQueuedCandidatesAsync", batch);
    }
    [Fact]
    public void Editor_audio_and_transcription_share_completion_and_close_dispatch()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var media = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.MediaActions.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        foreach (var source in new[] { shell, media })
        {
            Assert.Contains("_operations.TryBegin()", source);
            Assert.Contains("CompleteOperation(_operations.Complete(outcome))", source);
            Assert.Contains("CreateLinkedTokenSource(_windowLifetime.Token)", source);
        }
        Assert.Contains("VideoEditMode.FastTrim", shell);
        Assert.Contains("VideoEditMode.Join", shell);
        Assert.Contains("RunLocalMediaAsync(false)", media);
        Assert.Contains("RunLocalMediaAsync(true)", media);
        Assert.Contains("_operations.RequestClose();", shell);
        Assert.Contains("_browserOperation?.RequestClose();", shell);
        Assert.Contains("case OperationCompletion.CloseWindow:", download);
        Assert.Contains("case OperationCompletion.StartQueue:", download);
        Assert.Contains("ReferenceEquals(_progressOwner, owner)", download);
        Assert.Contains("_progressOwner = null", download);
        Assert.DoesNotContain("TryStartPendingQueue", download);
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
