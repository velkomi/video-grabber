namespace VideoGrabber.Infrastructure.Tests;

public sealed class SourcePrivacyRegressionTests
{
    [Fact]
    public void Browser_diagnostics_do_not_log_media_absolute_paths()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var lines = browser.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("DiagnosticHub.Log.Write", StringComparison.Ordinal)
                && line.Contains("AbsolutePath", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(lines);
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
