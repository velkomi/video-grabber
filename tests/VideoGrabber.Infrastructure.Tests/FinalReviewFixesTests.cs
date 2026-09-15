using System.Security.Cryptography;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class FinalReviewFixesTests
{
    [Fact]
    public void Exact_saved_rule_requires_proxied_browser_even_without_provider_session()
    {
        var saved = new SiteRouteSettings([new("example.com", "ethernet")]);
        Assert.True(DownloadRouteResolver.RequiresProxy(saved, new Uri("https://example.com/page"), null));
        Assert.False(DownloadRouteResolver.RequiresProxy(saved, new Uri("https://unrelated.example/page"), null));
    }

    [Fact]
    public void Key_tag_without_method_is_fail_closed()
    {
        const string text = "#EXTM3U\n#EXT-X-KEY:URI=\"https://keys.example/key\"\n#EXTINF:5,\nseg.ts\n";
        Assert.True(HlsManifestParser.TryParse(text, new Uri("https://cdn.example/media.m3u8"), out var info));
        Assert.True(info!.IsEncrypted);
        Assert.False(HlsDownloadPolicy.IsAllowed(info));
    }
    [Fact]
    public void Master_selected_tracks_must_be_verified_clear_before_download()
    {
        var master = new HlsManifestInfo(true,
            [new HlsVariant(new Uri("https://cdn.example/video.m3u8"), 1280, 720, 1_000_000, 30, "aud")],
            [new HlsAudioRendition(new Uri("https://cdn.example/audio.m3u8"), "ru", "Russian", "aud", true)],
            false, null, false);
        var candidate = new MediaCandidate(new Uri("https://cdn.example/master.m3u8"), new Uri("https://school.example/lesson"), "HLS", HlsManifest: master);
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>()));
        var verified = new HashSet<string>(StringComparer.Ordinal) { plan.HlsVideoSource!.AbsoluteUri, plan.HlsAudioSource!.AbsoluteUri };
        Assert.True(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, verified));
    }

    [Fact]
    public async Task Invalid_audio_only_output_is_preserved_after_probe_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-audio-clean-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new AudioOutputRunner();
            var downloader = new YtDlpDownloader(runner, new ToolLocator(root, root), new InvalidAudioProbe());
            var result = await downloader.DownloadAsync(new DownloadRequest(new Uri("https://cdn.example/audio.m3u8"), root, "best", AudioOnly: true, DirectManifest: true), null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.True(File.Exists(runner.OutputPath));
            Assert.Equal(SHA256.HashData(new byte[] { 1, 2, 3 }), SHA256.HashData(File.ReadAllBytes(runner.OutputPath)));
            Assert.Contains(Path.GetDirectoryName(runner.OutputPath)!, result.Details);
            Assert.Empty(Directory.GetFiles(root, "*.mp3"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class AudioOutputRunner : IProcessRunner
    {
        public string OutputPath { get; private set; } = string.Empty;
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            OutputPath = Path.Combine(spec.WorkingDirectory!, "invalid.mp3");
            File.WriteAllBytes(OutputPath, [1, 2, 3]);
            onOutput?.Invoke("filepath:" + OutputPath);
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class InvalidAudioProbe : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
            => Task.FromResult(new MediaProbeResult(true, true, true, "aac"));
    }
}
