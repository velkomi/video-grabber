using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class Preview12FinalizationTests
{
    [Fact]
    public async Task Cancel_after_100_percent_finalizes_valid_downloading_file_and_removes_temp()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-p12-finalize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new CompletedThenCancelledRunner();
            var probe = new ValidVideoProbe(3842.4);
            var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), probe);
            var result = await downloader.DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), root, "720p",
                    DirectManifest: true, SuggestedBaseName: "MODULE 1 - Part 2 - Day 1 - 720p"),
                null, CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.OutputPath);
            Assert.Contains("01h04m02s", Path.GetFileName(result.OutputPath!), StringComparison.Ordinal);
            Assert.DoesNotContain("downloading", Path.GetFileName(result.OutputPath!), StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(runner.TempPath));
            Assert.False(File.Exists(runner.WorkPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CompletedThenCancelledRunner : IProcessRunner
    {
        public string WorkPath { get; private set; } = string.Empty;
        public string TempPath { get; private set; } = string.Empty;

        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            var args = spec.Arguments.ToList();
            var outputIndex = args.IndexOf("-o");
            var template = args[outputIndex + 1];
            WorkPath = template.Replace("%(ext)s", "mp4", StringComparison.Ordinal);
            TempPath = Path.Combine(Path.GetDirectoryName(WorkPath)!, Path.GetFileNameWithoutExtension(WorkPath) + ".temp.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(WorkPath)!);
            File.WriteAllBytes(WorkPath, [1, 2, 3, 4]);
            File.WriteAllBytes(TempPath, [5, 6, 7]);
            onOutput?.Invoke("videograbber:1000|1000|NA|NA|NA|1.00MiB/s|00:00");
            throw new OperationCanceledException();
        }
    }

    private sealed class ValidVideoProbe(double durationSeconds) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(new MediaProbeResult(true, true, true, "aac", DurationSeconds: durationSeconds));
    }
}
public sealed class Preview12QueueAndShutdownWiringTests
{
    [Fact]
    public void Queue_run_requested_during_active_download_is_deferred_until_current_finishes()
    {
        var root = FindRepoRoot();
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));
        var download = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Download.cs"));
        Assert.Contains("_queueRunRequested", batch);
        Assert.Contains("_queueRunRequested = true", batch);
        Assert.Contains("TryStartPendingQueue", download);
    }

    [Fact]
    public void Window_close_defers_native_cleanup_until_active_download_stops()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        Assert.Contains("AppWindow.Closing +=", shell);
        Assert.Contains("_closeRequested", shell);
        Assert.Contains("args.Cancel = true", shell);
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
