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
}
