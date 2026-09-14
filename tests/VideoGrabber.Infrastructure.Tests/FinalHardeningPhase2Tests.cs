using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class FinalHardeningPhase2Tests
{
    [Fact]
    public void Any_encrypted_hls_is_blocked_by_download_policy()
    {
        const string encrypted = "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"https://keys.example/key\"\n#EXTINF:5,\nseg.ts\n";
        Assert.True(HlsManifestParser.TryParse(encrypted, new Uri("https://cdn.example/media.m3u8"), out var info));
        Assert.True(info!.IsEncrypted);
        Assert.False(HlsDownloadPolicy.IsAllowed(info));

        const string clear = "#EXTM3U\n#EXT-X-TARGETDURATION:5\n#EXTINF:5,\nseg.ts\n";
        Assert.True(HlsManifestParser.TryParse(clear, new Uri("https://cdn.example/media.m3u8"), out var clearInfo));
        Assert.True(HlsDownloadPolicy.IsAllowed(clearInfo!));
    }

    [Fact]
    public void Quality_cap_never_selects_variant_above_requested_limit()
    {
        var info = new HlsManifestInfo(true,
            [new HlsVariant(new Uri("https://cdn.test/1080.m3u8"), 1920, 1080, 3_000_000, 30, null)],
            [], false, null, false);
        Assert.Null(HlsTrackSelector.Select(info, "720p"));
    }
}
