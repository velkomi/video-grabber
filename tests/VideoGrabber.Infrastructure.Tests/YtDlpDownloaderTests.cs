using VideoGrabber.Core.Downloads;
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

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public ProcessSpec? Spec { get; private set; }

        public Task<ProcessResult> RunAsync(
            ProcessSpec spec,
            Action<string>? onOutput,
            CancellationToken cancellationToken)
        {
            Spec = spec;
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
