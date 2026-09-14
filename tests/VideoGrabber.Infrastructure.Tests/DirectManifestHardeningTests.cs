using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DirectManifestHardeningTests
{
    [Fact]
    public void Browser_discovered_extensionless_HLS_is_marked_direct_manifest()
    {
        var candidate = new MediaCandidate(
            new Uri("https://vhapi02.getcourse.ru/api/playlist/master/abc?sig=secret"),
            new Uri("https://iglyrazuma.ru/lesson"), "HLS", "master",
            HlsManifest: ClearMaster());
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        Assert.True(plan.DirectManifest);
    }

    [Fact]
    public void Raw_or_encrypted_HLS_is_not_queueable_but_clear_manifest_is()
    {
        var source = new Uri("https://cdn.example/master");
        var referer = new Uri("https://school.example/lesson");
        Assert.False(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS")));
        Assert.True(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS", HlsManifest: ClearMaster())));
        Assert.False(MediaCandidatePolicy.CanQueue(new(source, referer, "HLS", HlsManifest: EncryptedMaster())));
        Assert.True(MediaCandidatePolicy.CanQueue(new(new Uri("https://cdn.example/video.mp4"), referer, "MP4")));
    }

    [Fact]
    public async Task Direct_manifest_download_forces_generic_extractor()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new RecordingRunner(root);
            var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), new ValidProbe());
            var result = await downloader.DownloadAsync(new DownloadRequest(
                new Uri("https://vhapi02.getcourse.ru/api/playlist/master/abc"), root, "best",
                DirectManifest: true), null, CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.Contains("--use-extractors", runner.Arguments);
            var index = runner.Arguments.IndexOf("--use-extractors");
            Assert.Equal("generic", runner.Arguments[index + 1]);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static HlsManifestInfo ClearMaster() => new(true,
        [new HlsVariant(new Uri("https://cdn.example/v.m3u8"), 1280, 720, 1_000_000, 30, null)],
        [], false, null, false);

    private static HlsManifestInfo EncryptedMaster() => new(true,
        [new HlsVariant(new Uri("https://cdn.example/v.m3u8"), 1280, 720, 1_000_000, 30, null)],
        [], true, "AES-128", false);

    private sealed class RecordingRunner(string root) : IProcessRunner
    {
        public List<string> Arguments { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Arguments.AddRange(spec.Arguments);
            var output = Path.Combine(root, "direct.mp4");
            File.WriteAllBytes(output, [1, 2, 3]);
            onOutput?.Invoke("filepath:" + output);
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class ValidProbe : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(new MediaProbeResult(true, true, true, "aac"));
    }
}
