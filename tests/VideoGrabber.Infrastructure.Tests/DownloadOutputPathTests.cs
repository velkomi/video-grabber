using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadOutputPathTests
{
    [Fact]
    public async Task Successful_process_recovers_new_media_file_when_filepath_line_is_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new CreatesFileWithoutPathRunner(root, "01 - Часть 1 - 720p.mp4");
            var probe = new StubProbe(new MediaProbeResult(true, true, true, "aac"));
            var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master"), root, "720p", DirectManifest: true,
                    SuggestedBaseName: "01 - Часть 1 - 720p"), null, CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.Equal(runner.CreatedPath, result.OutputPath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Filename_sanitizer_removes_signed_query_and_windows_invalid_characters()
    {
        var value = DownloadFileName.SanitizeBaseName("02 - Часть: 2 ?consumer=vod&jwt=eyJ.private/token*bad");
        Assert.StartsWith("02 - Часть 2", value, StringComparison.Ordinal);
        Assert.DoesNotContain("consumer", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('?', value);
        Assert.DoesNotContain(':', value);
        Assert.DoesNotContain('*', value);
    }

    private sealed class CreatesFileWithoutPathRunner(string root, string fileName) : IProcessRunner
    {
        public string CreatedPath { get; } = Path.Combine(root, fileName);
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            File.WriteAllBytes(CreatedPath, [1, 2, 3, 4]);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }
}
