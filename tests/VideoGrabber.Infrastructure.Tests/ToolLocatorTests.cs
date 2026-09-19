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

    [Fact]
    public void Complete_bundled_runtime_is_authoritative_and_exposes_whisper()
    {
        var root = Path.Combine(Path.GetTempPath(), $"VideoGrabber-Bundled-{Guid.NewGuid():N}");
        var app = Path.Combine(root, "app");
        var bundled = Path.Combine(app, "tools");
        var whisper = Path.Combine(bundled, "whisper");
        var userTools = Path.Combine(root, "user-tools");
        Directory.CreateDirectory(whisper);
        Directory.CreateDirectory(userTools);
        foreach (var name in new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" })
        {
            File.WriteAllText(Path.Combine(bundled, name), "bundled");
            File.WriteAllText(Path.Combine(userTools, name), "user");
        }
        File.WriteAllText(Path.Combine(whisper, "whisper-cli.exe"), "exe");
        File.WriteAllText(Path.Combine(whisper, "ggml-base.bin"), "model");

        try
        {
            var locator = new ToolLocator(app, userTools);
            Assert.True(locator.UsesBundledRuntime);
            Assert.True(locator.WhisperAvailable);
            Assert.Equal(Path.Combine(bundled, "yt-dlp.exe"), locator.YtDlp);
            Assert.Equal(Path.Combine(whisper, "whisper-cli.exe"), locator.WhisperCli);
            Assert.Equal(Path.Combine(whisper, "ggml-base.bin"), locator.WhisperModel);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }}
