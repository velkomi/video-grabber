using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class MasterHlsDownloadTests
{
    private static HlsManifestInfo Master() => new(true,
        [
            new(new Uri("https://cdn.example/360.m3u8"), 640, 360, 600_000, 30, null),
            new(new Uri("https://cdn.example/480.m3u8"), 854, 480, 900_000, 30, null),
            new(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1_500_000, 30, null)
        ], [], false, null, false);

    [Theory]
    [InlineData("360p", "360.m3u8")]
    [InlineData("480p", "480.m3u8")]
    [InlineData("720p", "720.m3u8")]
    public void Quality_selects_exact_expected_variant(string quality, string expected)
    {
        var plan = HlsTrackSelector.Select(Master(), quality);
        Assert.NotNull(plan);
        Assert.EndsWith(expected, plan!.Video.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_child_manifest_can_complete_master_preflight()
    {
        var body = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXTINF:6,\nseg.ts\n";
        Assert.True(HlsDownloadPreflight.TryVerify(body, new Uri("https://cdn.example/720.m3u8"), out var info));
        Assert.NotNull(info);
    }
}
