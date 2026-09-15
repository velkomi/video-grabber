using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class YtDlpDownloaderTests
{
    [Theory]
    [InlineData("360p", "720p", false)]
    [InlineData("720p", "360p", true)]
    [InlineData("best", "best", false)]
    public async Task Audit_A003_Resolved_leaf_has_no_second_height_filter(string globalQuality, string selectedQuality, bool unknownHeight)
    {
        var source = new Uri("https://cdn.test/360.m3u8");
        var text = unknownHeight ? "#EXTM3U\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST"
            : "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=100000,RESOLUTION=640x360\n360.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=200000,RESOLUTION=1280x720\n720.m3u8";
        Assert.True(HlsManifestParser.TryParse(text, source, out var manifest));
        var candidate = new MediaCandidate(source, new Uri("https://site.test/lesson"), "HLS", HlsManifest: manifest);
        var plan = MediaDownloadPlanResolver.Resolve(candidate, selectedQuality, false);
        Assert.True(plan.IsResolved);
        Assert.True(plan.ResolvedHlsLeaf);
        var output = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        var runner = new RecordingProcessRunner();
        try
        {
            var controls = new UserDownloadIntent(candidate.Source, globalQuality, false, output, null, 1);
            var intent = controls with { Quality = selectedQuality };
            var ready = new TaskCompletionSource<PreparedDownload>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = DownloadRequestFactory.PrepareAsync(intent, (_, _) => ready.Task, CancellationToken.None);
            controls = controls with { Quality = "1080p", AudioOnly = true, OutputDirectory = "later-output", CookieSelection = "chrome", SessionEpoch = 2 };
            ready.SetResult(new(plan.Source, candidate.Referer, null, null, null, null, null, plan.DirectManifest,
                plan.ResolvedHlsLeaf, null, null, null));
            var request = await pending;
            Assert.Equal(selectedQuality, request.Quality);
            Assert.Equal(output, request.OutputDirectory);
            Assert.False(request.AudioOnly);
            Assert.Null(request.CookiesFromBrowser);
            await new YtDlpDownloader(runner, new ToolLocator(output, output)).DownloadAsync(request, null, CancellationToken.None);
            Assert.DoesNotContain(runner.Spec!.Arguments, argument => argument.Contains("height", StringComparison.Ordinal));
            Assert.Contains("best", runner.Spec.Arguments);
            Assert.Equal(unknownHeight ? "360.m3u8" : "720.m3u8", Path.GetFileName(plan.Source.AbsolutePath));
            Assert.Contains(plan.Source.AbsoluteUri, runner.Spec.Arguments);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Theory]
    [InlineData("1440p", "bestvideo*[height<=1440]+bestaudio/best[height<=1440]")]
    [InlineData("2160p", "bestvideo*[height<=2160]+bestaudio/best[height<=2160]")]
    [InlineData("4K", "bestvideo*[height<=2160]+bestaudio/best[height<=2160]")]
    [InlineData("best", "bestvideo*+bestaudio/best")]
    public async Task Audit_A003_Non_leaf_uses_parsed_numeric_quality(string quality, string expectedFormat)
    {
        var output = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        var runner = new RecordingProcessRunner();
        try
        {
            await new YtDlpDownloader(runner, new ToolLocator(output, output)).DownloadAsync(
                new DownloadRequest(new Uri("https://site.test/video"), output, quality), null, CancellationToken.None);
            Assert.Contains(expectedFormat, runner.Spec!.Arguments);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task Audit_A003_Invalid_quality_never_starts_a_process()
    {
        var runner = new RecordingProcessRunner();
        var result = await new YtDlpDownloader(runner, new ToolLocator(@"C:\VideoGrabber", @"C:\VideoGrabber\tools")).DownloadAsync(
            new DownloadRequest(new Uri("https://site.test/video"), "unused", "broken"), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Null(runner.Spec);
    }

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
        var runner = new RecordingProcessRunner("audio.mp3");
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
            if (outputPath is not null)
            {
                var output = Path.Combine(spec.WorkingDirectory!, outputPath);
                File.WriteAllText(output, "not-empty");
                onOutput?.Invoke($"filepath:{output}");
            }
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
