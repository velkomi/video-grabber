using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadJobIsolationTests
{
    [Fact]
    public async Task Download_runs_inside_job_directory_then_promotes_verified_file_to_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-job-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var job = Path.Combine(root, "job");
        Directory.CreateDirectory(output);
        try
        {
            var runner = new JobRunner(job);
            var probe = new StubProbe(new MediaProbeResult(true, true, true, "aac", DurationSeconds: 3723));
            var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), output, "720p", DirectManifest: true,
                    SuggestedBaseName: "01 - Часть 1 - 720p", JobDirectory: job), null, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.OutputPath);
            Assert.StartsWith(output, result.OutputPath!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("01h02m03s", Path.GetFileName(result.OutputPath!), StringComparison.Ordinal);
            Assert.Equal(job, runner.ObservedWorkingDirectory);
            Assert.False(Directory.Exists(job));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class JobRunner(string job) : IProcessRunner
    {
        public string? ObservedWorkingDirectory { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            ObservedWorkingDirectory = spec.WorkingDirectory;
            Directory.CreateDirectory(job);
            var path = Path.Combine(job, "01 - Часть 1 - 720p.mp4");
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            onOutput?.Invoke("filepath:" + path);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}