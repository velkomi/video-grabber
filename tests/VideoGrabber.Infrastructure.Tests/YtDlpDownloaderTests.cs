using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class YtDlpDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_keeps_progress_enabled_when_file_path_is_printed()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        var runner = new RecordingProcessRunner();
        var tools = new ToolLocator(@"C:\VideoGrabber", @"C:\VideoGrabber\tools");

        try
        {
            await new YtDlpDownloader(runner, tools).DownloadAsync(
                new DownloadRequest(
                    new Uri("https://example.com/video"),
                    outputDirectory,
                    "best",
                    null,
                    false),
                null,
                CancellationToken.None);

            Assert.Contains("--print", runner.Spec!.Arguments);
            Assert.Contains("--progress", runner.Spec.Arguments);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAsync_configures_real_mp3_finite_retries_and_validates_output()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, "audio.mp3");
        await File.WriteAllTextAsync(output, "not-empty");
        var runner = new RecordingProcessRunner(output);
        var probe = new StubProbe(new MediaProbeResult(true, true, false, "mp3"));

        try
        {
            var result = await new YtDlpDownloader(runner, new ToolLocator(outputDirectory, outputDirectory), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://example.com/audio"), outputDirectory, "best", AudioOnly: true),
                null,
                CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Contains("--progress", runner.Spec!.Arguments);
            Assert.Contains("--print", runner.Spec.Arguments);
            Assert.Contains("--extract-audio", runner.Spec.Arguments);
            Assert.Contains("--audio-format", runner.Spec.Arguments);
            Assert.Contains("mp3", runner.Spec.Arguments);
            Assert.Contains("--retries", runner.Spec.Arguments);
            Assert.Contains("--socket-timeout", runner.Spec.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_never_succeeds_without_a_valid_reported_file()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        var runner = new RecordingProcessRunner();
        var probe = new StubProbe(new MediaProbeResult(false, false, false, null, "missing"));
        try
        {
            var result = await new YtDlpDownloader(runner, new ToolLocator(outputDirectory, outputDirectory), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://example.com/video"), outputDirectory, "best"), null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Null(result.OutputPath);
        }
        finally
        {
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);
        }
    }

    private sealed class RecordingProcessRunner(string? outputPath = null) : IProcessRunner
    {
        public ProcessSpec? Spec { get; private set; }

        public Task<ProcessResult> RunAsync(
            ProcessSpec spec,
            Action<string>? onOutput,
            CancellationToken cancellationToken)
        {
            Spec = spec;
            if (outputPath is not null) onOutput?.Invoke($"filepath:{outputPath}");
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
