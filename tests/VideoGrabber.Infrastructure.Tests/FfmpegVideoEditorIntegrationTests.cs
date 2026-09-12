using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Editing;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class FfmpegVideoEditorIntegrationTests
{
    [MediaToolsFact]
    public async Task Editor_trims_and_joins_a_real_synthetic_video()
    {
        var toolsRoot = Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS");
        if (string.IsNullOrWhiteSpace(toolsRoot))
        {
            return;
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"VideoGrabber-Integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var tools = new ToolLocator(toolsRoot);
            var runner = new ProcessRunner();
            var source = Path.Combine(temporaryDirectory, "source.mp4");
            var fragment = Path.Combine(temporaryDirectory, "fragment.mp4");
            var joined = Path.Combine(temporaryDirectory, "joined.mp4");

            var generated = await runner.RunAsync(
                new ProcessSpec(tools.Ffmpeg, new[]
                {
                    "-hide_banner", "-y",
                    "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25",
                    "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=48000",
                    "-t", "3", "-c:v", "mpeg4", "-c:a", "aac", source
                }),
                null,
                CancellationToken.None);
            Assert.True(generated.IsSuccess, generated.StandardError);

            var editor = new FfmpegVideoEditor(runner, tools);
            await editor.EditAsync(
                new VideoEditRequest(
                    VideoEditMode.FastTrim,
                    new[] { source },
                    fragment,
                    TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(1)),
                CancellationToken.None);
            await editor.EditAsync(
                new VideoEditRequest(VideoEditMode.Join, new[] { fragment, fragment }, joined),
                CancellationToken.None);

            Assert.True(new FileInfo(fragment).Length > 0);
            Assert.True(new FileInfo(joined).Length > new FileInfo(fragment).Length);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
