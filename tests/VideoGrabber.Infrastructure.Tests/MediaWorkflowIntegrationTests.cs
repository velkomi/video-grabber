using System.Text.Json;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Audio;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Media;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class MediaWorkflowIntegrationTests
{
    [MediaToolsFact]
    public async Task Extractor_creates_real_mp3_with_mp3_audio_no_video_and_full_decode()
    {
        var toolsRoot = Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS");
        if (string.IsNullOrWhiteSpace(toolsRoot)) return;

        var root = Path.Combine(Path.GetTempPath(), $"VideoGrabber-Mp3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var tools = new ToolLocator(toolsRoot);
            var runner = new ProcessRunner();
            var source = Path.Combine(root, "source.mp4");
            var output = Path.Combine(root, "audio.mp3");
            var generated = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg, new[]
            {
                "-hide_banner", "-y",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100",
                "-t", "1", "-c:v", "mpeg4", "-c:a", "aac", source
            }), null, CancellationToken.None);
            Assert.True(generated.IsSuccess, generated.StandardError);

            var result = await new FfmpegAudioExtractor(runner, tools, new FfprobeMediaProbe(runner, tools))
                .ExtractAsync(source, output, CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.NotEqual(Path.GetFullPath(source), Path.GetFullPath(output));

            var probe = await runner.RunAsync(new ProcessSpec(tools.Ffprobe, new[]
            {
                "-v", "error", "-show_entries", "stream=codec_type,codec_name", "-of", "json", output
            }), null, CancellationToken.None);
            Assert.True(probe.IsSuccess, probe.StandardError);
            using var document = JsonDocument.Parse(probe.StandardOutput);
            var streams = document.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            Assert.Contains(streams, stream => stream.GetProperty("codec_type").GetString() == "audio" && stream.GetProperty("codec_name").GetString() == "mp3");
            Assert.DoesNotContain(streams, stream => stream.GetProperty("codec_type").GetString() == "video");

            var decode = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg, new[]
            {
                "-v", "error", "-i", output, "-f", "null", "-"
            }), null, CancellationToken.None);
            Assert.True(decode.IsSuccess, decode.StandardError);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
