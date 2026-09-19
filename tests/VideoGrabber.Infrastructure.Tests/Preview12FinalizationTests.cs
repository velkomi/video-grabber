using System.Security.Cryptography;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class Preview12FinalizationTests
{
    [Theory]
    [InlineData(false, true, 2d, true)]
    [InlineData(true, true, 2d, true)]
    [InlineData(false, true, 60d, false)]
    [InlineData(true, null, 60d, false)]
    [InlineData(true, false, 60d, true)]
    public async Task Completed_output_must_meet_contract_before_promotion(bool split, bool? expectedAudio, double duration, bool hasAudio)
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-p12-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runner = new CompletedOutputRunner();
        var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), new ValidVideoProbe(duration, hasAudio))
            .DownloadAsync(new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), root, "best",
                HlsVideoSource: split ? new Uri("https://cdn.example/video.m3u8") : null,
                HlsAudioSource: split ? new Uri("https://cdn.example/audio.m3u8") : null,
                SuggestedBaseName: "Lesson", ExpectedDurationSeconds: 60, ExpectedAudio: expectedAudio), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Null(result.OutputPath);
        Assert.Empty(Directory.GetFiles(root, "*.mp4"));
        Assert.All(runner.WorkPaths, path => Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(path)));
    }

    private sealed class CompletedOutputRunner : IProcessRunner
    {
        public List<string> WorkPaths { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            var args = spec.Arguments.ToList();
            var index = args.IndexOf("-o");
            var path = index >= 0 ? args[index + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal) : args[^1];
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            WorkPaths.Add(path);
            onOutput?.Invoke("filepath:" + path);
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    [Fact]
    public async Task Failed_finalization_preserves_readable_owned_output_without_success()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-p12-failed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runner = new FailedFinalizationRunner();
        var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), new ValidVideoProbe(60));
        var result = await downloader.DownloadAsync(new DownloadRequest(new Uri("https://cdn.example/video"), root, "best",
            SuggestedBaseName: "Lesson", ExpectedDurationSeconds: 60, ExpectedAudio: true), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Null(result.OutputPath);
        Assert.True(File.Exists(runner.WorkPath));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(runner.WorkPath));
        Assert.Contains(Path.GetDirectoryName(runner.WorkPath)!, result.Details);
        Assert.Empty(Directory.GetFiles(root, "*.mp4"));
    }

    private sealed class FailedFinalizationRunner : IProcessRunner
    {
        public string WorkPath { get; private set; } = string.Empty;
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            var args = spec.Arguments.ToList();
            WorkPath = args[args.IndexOf("-o") + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal);
            File.WriteAllBytes(WorkPath, [1, 2, 3, 4]);
            onOutput?.Invoke("filepath:" + WorkPath);
            return Task.FromResult(new ProcessResult(1, "", "ERROR: finalization failed"));
        }
    }

    [Fact]
    public async Task Cancel_after_100_percent_preserves_work_and_temp_without_promotion()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-p12-finalize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new CompletedThenCancelledRunner();
            var probe = new ValidVideoProbe(3842.4);
            var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), probe);
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), root, "720p",
                    DirectManifest: true, SuggestedBaseName: "MODULE 1 - Part 2 - Day 1 - 720p"),
                null, CancellationToken.None));
            Assert.Equal(Path.GetDirectoryName(runner.WorkPath), error.Data["JobDirectory"]);
            Assert.True(File.Exists(runner.TempPath));
            Assert.True(File.Exists(runner.WorkPath));
            Assert.Equal(SHA256.HashData(new byte[] { 5, 6, 7 }), SHA256.HashData(File.ReadAllBytes(runner.TempPath)));
            Assert.Equal(SHA256.HashData(new byte[] { 1, 2, 3, 4 }), SHA256.HashData(File.ReadAllBytes(runner.WorkPath)));
            Assert.Empty(Directory.GetFiles(root, "*.mp4"));
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

    private sealed class ValidVideoProbe(double durationSeconds, bool hasAudio = true) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(new MediaProbeResult(true, hasAudio, true, hasAudio ? "aac" : null, DurationSeconds: durationSeconds));
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
        Assert.Contains("_operations.RequestQueue();", batch);
        Assert.Contains("case OperationCompletion.StartQueue:", download);
        Assert.Contains("CompleteOperation(completion);", download);
        Assert.DoesNotContain("InstallComponentsAsync(forceUpdate: false", download);
    }

    [Fact]
    public void Window_close_defers_native_cleanup_until_active_download_stops()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        Assert.Contains("AppWindow.Closing +=", shell);
        Assert.Contains("_operations.RequestClose();", shell);
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
