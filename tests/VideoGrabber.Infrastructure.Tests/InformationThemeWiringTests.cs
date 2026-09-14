namespace VideoGrabber.Infrastructure.Tests;

public sealed class InformationThemeWiringTests
{
    [Fact]
    public void Browser_has_context_help_and_theme_uses_dynamic_author_brushes()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var window = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var info = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Information.cs"));

        Assert.Contains("\\u24D8", browser);
        Assert.Contains("ShowPage(\"info\")", browser);
        Assert.Contains("Background = AuthorBackgroundBrush", window);
        Assert.Contains("BorderBrush = AuthorBorderBrush", window);
        Assert.Contains("AppThemeMode.Dark", info);
        Assert.Contains("Как в Windows", info);
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
