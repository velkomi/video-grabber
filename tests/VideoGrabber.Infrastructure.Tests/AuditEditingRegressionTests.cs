using System.Security.Cryptography;
using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Editing;
using VideoGrabber.Infrastructure.Media;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class AuditEditingRegressionTests
{
    [MediaToolsFact]
    public async Task TrimBeyondEndMustReject()
    {
        var context = await Fixture.CreateAsync();
        try
        {
            var sourceHash = Hash(context.H264);
            var output = Path.Combine(context.Root, "trim-beyond.mp4");
            var error = await Record.ExceptionAsync(() => context.Editor.EditAsync(
                new VideoEditRequest(VideoEditMode.FastTrim, [context.H264], output,
                    TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)), context.Token));

            Assert.IsAssignableFrom<ArgumentException>(error);
            Assert.False(File.Exists(output));
            Assert.Equal(sourceHash, Hash(context.H264));
        }
        finally { context.Dispose(); }
    }
    [MediaToolsFact]
    public async Task JoinDifferentCodecMustRejectOrProduceFullyDecodableExpectedDuration()
    {
        var context = await Fixture.CreateAsync();
        try
        {
            var h264Hash = Hash(context.H264);
            var mpeg4Hash = Hash(context.Mpeg4);
            var output = Path.Combine(context.Root, "mixed-join.mp4");
            var error = await Record.ExceptionAsync(() => context.Editor.EditAsync(
                new VideoEditRequest(VideoEditMode.Join, [context.H264, context.Mpeg4], output), context.Token));

            if (error is null)
            {
                var probe = await new FfprobeMediaProbe(context.Runner, context.Tools)
                    .ProbeAsync(output, context.Token);
                var decode = await context.Runner.RunAsync(new ProcessSpec(context.Tools.Ffmpeg,
                    ["-v", "error", "-nostdin", "-i", output, "-f", "null", "-"]), null, context.Token);
                Assert.True(decode.IsSuccess, decode.StandardError);
                Assert.True(probe.IsValid && probe.HasVideo && probe.HasAudio, probe.Error);
                Assert.InRange(probe.DurationSeconds, 7d, 9d);
                Assert.NotEmpty(await FrameHash(context, output, "0.5"));
                Assert.NotEmpty(await FrameHash(context, output, "7"));
            }
            else Assert.IsAssignableFrom<ArgumentException>(error);

            Assert.Equal(h264Hash, Hash(context.H264));
            Assert.Equal(mpeg4Hash, Hash(context.Mpeg4));
        }
        finally { context.Dispose(); }
    }

    private static string Hash(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static async Task<string> FrameHash(Fixture context, string path, string seek)
    {
        var result = await context.Runner.RunAsync(new ProcessSpec(context.Tools.Ffmpeg,
            ["-v", "error", "-nostdin", "-ss", seek, "-i", path,
             "-map", "0:v:0", "-frames:v", "1", "-f", "hash", "-hash", "sha256", "-"]),
            null, context.Token);
        Assert.True(result.IsSuccess, result.StandardError);
        return result.StandardOutput.Trim();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(2));
        private Fixture(string root, ToolLocator tools, ProcessRunner runner)
        {
            Root = root;
            Tools = tools;
            Runner = runner;
            H264 = Path.Combine(root, "edit-h264.mp4");
            Mpeg4 = Path.Combine(root, "edit-mpeg4.mp4");
            Editor = new FfmpegVideoEditor(runner, tools);
        }
        public string Root { get; }
        public string H264 { get; }
        public string Mpeg4 { get; }
        public ToolLocator Tools { get; }
        public ProcessRunner Runner { get; }
        public FfmpegVideoEditor Editor { get; }
        public CancellationToken Token => _timeout.Token;
        public static async Task<Fixture> CreateAsync()
        {
            var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
            var root = Path.Combine(evidence, "editing-024-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
            var runner = new ProcessRunner();
            var fixture = new Fixture(root, tools, runner);
            await fixture.GenerateAsync(fixture.H264, "testsrc2=size=320x180:rate=25", "440", "libx264");
            await fixture.GenerateAsync(fixture.Mpeg4, "testsrc2=size=640x360:rate=25", "880", "mpeg4");
            return fixture;
        }

        private async Task GenerateAsync(string path, string video, string frequency, string codec)
        {
            var args = new List<string>
            {
                "-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i", video,
                "-f", "lavfi", "-i", $"sine=frequency={frequency}:sample_rate=48000",
                "-t", "4", "-c:v", codec
            };
            if (codec == "libx264") args.AddRange(["-g", "25"]);
            args.AddRange(["-c:a", "aac", path]);
            var result = await Runner.RunAsync(new ProcessSpec(Tools.Ffmpeg, args), null, Token);
            Assert.True(result.IsSuccess, result.StandardError);
            var probe = await new FfprobeMediaProbe(Runner, Tools).ProbeAsync(path, Token);
            Assert.True(probe.IsValid && probe.HasVideo && probe.HasAudio, probe.Error);
            Assert.InRange(probe.DurationSeconds, 3.8, 4.2);
        }

        public void Dispose() => _timeout.Dispose();
    }
}

public sealed class AuditEditingRegressionPreflightTests
{
    [Fact]
    public async Task Trim_beyond_end_is_rejected_before_edit_ffmpeg_call()
    {
        var root = MakeRoot(out var tools, out var first, out _);
        try
        {
            var runner = new ProbeOnlyRunner(null);
            await Assert.ThrowsAsync<ArgumentException>(() => new FfmpegVideoEditor(runner, tools).EditAsync(
                new VideoEditRequest(VideoEditMode.FastTrim, [first], Path.Combine(root, "out.mp4"),
                    TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)), CancellationToken.None));
            Assert.Equal(0, runner.EditCalls);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Incompatible_join_is_rejected_before_edit_ffmpeg_call()
    {
        var root = MakeRoot(out var tools, out var first, out var second);
        try
        {
            var runner = new ProbeOnlyRunner(second);
            await Assert.ThrowsAsync<ArgumentException>(() => new FfmpegVideoEditor(runner, tools).EditAsync(
                new VideoEditRequest(VideoEditMode.Join, [first, second], Path.Combine(root, "out.mp4")), CancellationToken.None));
            Assert.Equal(0, runner.EditCalls);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string MakeRoot(out ToolLocator tools, out string first, out string second)
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-edit-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ffmpeg.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "ffprobe.exe"), "fixture");
        first = Path.Combine(root, "first.mp4");
        second = Path.Combine(root, "second.mp4");
        File.WriteAllText(first, "video-a");
        File.WriteAllText(second, "video-b");
        tools = new ToolLocator(root, root);
        return root;
    }

    private sealed class ProbeOnlyRunner(string? second) : IProcessRunner
    {
        public int EditCalls { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            if (Path.GetFileName(spec.FileName).Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
            {
                var input = spec.Arguments[^1];
                var mismatch = second is not null && Path.GetFullPath(input).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
                return Task.FromResult(new ProcessResult(0, ProbeJson(mismatch), ""));
            }
            EditCalls++;
            return Task.FromResult(new ProcessResult(99, "", "edit ffmpeg must not run"));
        }

        private static string ProbeJson(bool mismatch)
        {
            var codec = mismatch ? "mpeg4" : "h264";
            var width = mismatch ? 640 : 320;
            var height = mismatch ? 360 : 180;
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                streams = new object[]
                {
                    new { codec_type = "video", codec_name = codec, width, height, pix_fmt = "yuv420p", time_base = "1/12800" },
                    new { codec_type = "audio", codec_name = "aac", sample_rate = "48000", channels = 1, time_base = "1/48000" }
                },
                format = new { duration = "4.000000" }
            });
        }
    }
}
