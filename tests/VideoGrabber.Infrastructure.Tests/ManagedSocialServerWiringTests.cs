

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class BrowserWiringRegressionTests
{
    [Fact]
    public void Managed_public_social_links_use_server_worker_without_site_cookies()
    {
        var root = FindRepoRoot();
        var download = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        var social = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.App", "MainWindow.ServerSocialDownload.cs"));

        Assert.Contains("ShouldUseManagedSocialServer(intent)", download);
        Assert.Contains("RunManagedSocialServerDownloadAsync(intent)", download);

        Assert.Contains("youtube.com", social);
        Assert.Contains("youtu.be", social);
        Assert.Contains("instagram.com", social);
        Assert.Contains("tiktok.com", social);
        Assert.Contains("pinterest.com", social);
        Assert.Contains("pin.it", social);
        Assert.Contains("!UsesExplicitSiteSession(intent)", social);

        Assert.Contains("\"server_worker\"", social);
        Assert.Contains("/v1/sources/analyze", social);
        Assert.Contains("/v1/jobs", social);
        Assert.Contains("/download-link", social);
        Assert.Contains("DownloadManagedServerArtifactAsync", social);
        Assert.DoesNotContain("CookiesFromBrowser", social);
    }
}
