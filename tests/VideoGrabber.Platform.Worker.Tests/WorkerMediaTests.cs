using VideoGrabber.Platform.Worker;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class WorkerMediaTests
{
    [Fact]
    public void Tool_locator_has_cross_platform_names_not_windows_only_assumptions()
    {
        var tools = new WorkerToolLocator("yt-dlp", "ffmpeg", "ffprobe", "whisper-cli");
        Assert.Equal("yt-dlp", tools.YtDlp);
        Assert.Equal("ffmpeg", tools.Ffmpeg);
        Assert.Equal("ffprobe", tools.Ffprobe);
        Assert.Equal("whisper-cli", tools.Whisper);
        Assert.DoesNotContain(".exe", tools.YtDlp, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Actual_synthetic_video_is_full_decoded_and_sha_verified()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var runner = new BoundedProcessRunner();
            var tools = new WorkerToolLocator();
            var output = Path.Combine(root, "result.mp4");
            var generated = await runner.RunAsync(new WorkerProcessSpec(
                tools.Ffmpeg,
                ["-v", "error", "-f", "lavfi", "-i",
                 "testsrc=size=320x180:rate=25", "-t", "1",
                 "-pix_fmt", "yuv420p", "-y", output],
                root, TimeSpan.FromMinutes(1)), CancellationToken.None);
            Assert.True(generated.Success, generated.StandardError);

            var receipt = await new ArtifactVerifier(runner, tools).VerifyAsync(
                root, output, 320, 180, "video/mp4",
                "synthetic-verify", CancellationToken.None);

            Assert.Equal(64, receipt.Sha256.Length);
            Assert.True(receipt.Bytes > 0);
            Assert.Equal("video/mp4", receipt.MediaType);
            Assert.Equal("synthetic-verify", receipt.VerificationEvidenceId);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Truncated_media_is_rejected_by_full_decode()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var runner = new BoundedProcessRunner();
            var tools = new WorkerToolLocator();
            var good = Path.Combine(root, "good.mp4");
            var generated = await runner.RunAsync(new WorkerProcessSpec(
                tools.Ffmpeg,
                ["-v", "error", "-f", "lavfi", "-i",
                 "testsrc=size=320x180:rate=25", "-t", "2",
                 "-pix_fmt", "yuv420p", "-y", good],
                root, TimeSpan.FromMinutes(1)), CancellationToken.None);
            Assert.True(generated.Success, generated.StandardError);
            var bytes = await File.ReadAllBytesAsync(good);
            var broken = Path.Combine(root, "broken.mp4");
            await File.WriteAllBytesAsync(broken, bytes[..Math.Max(1, bytes.Length / 3)]);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new ArtifactVerifier(runner, tools).VerifyAsync(
                    root, broken, 320, 180, "video/mp4",
                    "broken", CancellationToken.None));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Artifact_outside_attempt_root_is_rejected_before_tools_run()
    {
        var root = TempRoot();
        var outsideRoot = TempRoot();
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outsideRoot);
        var outside = Path.Combine(outsideRoot, "foreign.mp4");
        await File.WriteAllBytesAsync(outside, [1, 2, 3]);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new ArtifactVerifier(new BoundedProcessRunner(), new WorkerToolLocator())
                    .VerifyAsync(root, outside, null, null, "video/mp4",
                        "foreign", CancellationToken.None));
        }
        finally
        {
            SafeDelete(root);
            SafeDelete(outsideRoot);
        }
    }

    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "vg-worker-" + Guid.NewGuid().ToString("N"));

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
