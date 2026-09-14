using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserBindingDiagnosticsTests
{
    [Fact]
    public void Fingerprint_hides_query_but_distinguishes_player_urls()
    {
        var one = new Uri("https://api2.gcvh.ru/sign-player/?token=secret-one&id=1");
        var two = new Uri("https://api2.gcvh.ru/sign-player/?token=secret-two&id=2");

        var first = BrowserBindingFingerprint.Describe(one);
        var second = BrowserBindingFingerprint.Describe(two);

        Assert.Contains("api2.gcvh.ru/sign-player/", first);
        Assert.Contains("q=", first);
        Assert.DoesNotContain("secret-one", first);
        Assert.DoesNotContain("token=", first);
        Assert.NotEqual(first, second);
    }
}

public sealed partial class BrowserBindingDiagnosticsWiringTests
{
    [Fact]
    public void App_logs_safe_slot_and_candidate_binding_fingerprints()
    {
        var root = FindRepoRoot();
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));

        Assert.Contains("browser.binding.slot", metadata);
        Assert.Contains("BrowserBindingFingerprint.Describe", metadata);
        Assert.Contains("browser.binding.candidate", devtools);
        Assert.Contains("BrowserBindingFingerprint.Describe", devtools);
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
