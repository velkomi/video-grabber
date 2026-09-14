using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserPlayerMasterBindingsTests
{
    [Fact]
    public void Master_playlist_resolves_back_to_exact_player_iframe()
    {
        var bindings = new BrowserPlayerMasterBindings();
        var master = new Uri("https://api2.gcvh.ru/master.m3u8?jwt=secret-master");
        var player = new Uri("https://api2.gcvh.ru/sign-player/?id=part-3&token=secret-player");

        bindings.Remember(master, player);

        Assert.True(bindings.TryResolve(master, out var resolved));
        Assert.Equal(player, resolved);
    }

    [Fact]
    public void Unknown_master_has_no_player_binding()
    {
        var bindings = new BrowserPlayerMasterBindings();
        Assert.False(bindings.TryResolve(new Uri("https://api1.gcvh.ru/unknown.m3u8"), out _));
    }
}

public sealed class BrowserPlayerMasterBindingsWiringTests
{
    [Fact]
    public void WebResource_master_uses_exact_player_binding_and_metadata_scans_part_labels()
    {
        var root = FindRepoRoot();
        var browser = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Browser.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));

        Assert.Contains("_playerMasterBindings.Remember(playlist, candidate.Source)", browser);
        Assert.Contains("_playerMasterBindings.TryResolve(responseUri", browser);
        Assert.Contains("partPattern", metadata);
        Assert.Contains("nearestPartTitle", metadata);
        Assert.Contains("_playerMasterBindings.Clear()", devtools);
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
