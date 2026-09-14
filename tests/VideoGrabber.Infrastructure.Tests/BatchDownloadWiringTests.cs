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
        Assert.Contains("DownloadAttemptOutcome.Cancelled", batch);
        Assert.Contains("Очередь остановлена", batch);
        Assert.Contains("return;", batch);
        Assert.Contains("enum DownloadAttemptOutcome", download);
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
