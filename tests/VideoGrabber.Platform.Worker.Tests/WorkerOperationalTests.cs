using VideoGrabber.Platform.Worker;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class WorkerOperationalTests
{
    [Fact]
    public void Capture_counts_only_worker_job_root_and_qualified_tool_paths()
    {
        var root = TempRoot("jobs");
        var toolsRoot = TempRoot("tools");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(toolsRoot);
        try
        {
            var job = Path.Combine(root, "job.bin");
            File.WriteAllBytes(job, new byte[123]);
            var yt = CreateTool(toolsRoot, "yt-dlp");
            var ffmpeg = CreateTool(toolsRoot, "ffmpeg");
            var ffprobe = CreateTool(toolsRoot, "ffprobe");
            File.WriteAllBytes(
                Path.Combine(toolsRoot, "not-job.bin"),
                new byte[999]);

            var snapshot = WorkerMetrics.Capture(
                root,
                new WorkerToolLocator(
                    yt,
                    ffmpeg,
                    ffprobe,
                    Path.Combine(toolsRoot, "whisper")));

            Assert.Equal(123, snapshot.JobRootBytes);
            Assert.Equal(1, snapshot.JobFileCount);
            Assert.True(snapshot.YtDlpAvailable);
            Assert.True(snapshot.FfmpegAvailable);
            Assert.True(snapshot.FfprobeAvailable);
            Assert.InRange(snapshot.FreeDiskPercent, 0, 100);
        }
        finally
        {
            SafeDelete(root);
            SafeDelete(toolsRoot);
        }
    }

    [Theory]
    [InlineData(19.9, true)]
    [InlineData(20.0, false)]
    public void Worker_disk_threshold_matches_platform_policy(
        double freePercent,
        bool expected)
        => Assert.Equal(
            expected,
            WorkerMetrics.DiskTooLow(freePercent));

    [Theory]
    [InlineData(59, false)]
    [InlineData(61, true)]
    public void Worker_heartbeat_threshold_matches_platform_policy(
        int seconds,
        bool expected)
        => Assert.Equal(
            expected,
            WorkerMetrics.HeartbeatStale(
                TimeSpan.FromSeconds(seconds)));

    private static string CreateTool(
        string root,
        string name)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, "qualified test tool");
        return path;
    }

    private static string TempRoot(string name)
        => Path.Combine(
            Path.GetTempPath(),
            "vg-worker-ops-" + name + "-"
            + Guid.NewGuid().ToString("N"));

    private static void SafeDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
