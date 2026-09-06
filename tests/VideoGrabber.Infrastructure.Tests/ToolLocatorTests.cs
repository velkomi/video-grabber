using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ToolLocatorTests
{
    [Fact]
    public void Locator_prefers_portable_tools_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"VideoGrabber-Test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        var expected = Path.Combine(root, "tools", "yt-dlp.exe");
        File.WriteAllText(expected, string.Empty);

        try
        {
            Assert.Equal(expected, new ToolLocator(root).YtDlp);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Locator_finds_tools_in_explicit_user_tools_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"VideoGrabber-Test-{Guid.NewGuid():N}");
        var userTools = Path.Combine(root, "user-tools");
        Directory.CreateDirectory(userTools);
        var expected = Path.Combine(userTools, "deno.exe");
        File.WriteAllText(expected, string.Empty);

        try
        {
            var locator = new ToolLocator(Path.Combine(root, "app"), userTools);

            Assert.Equal(userTools, locator.LocalToolsDirectory);
            Assert.Equal(expected, locator.Deno);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
