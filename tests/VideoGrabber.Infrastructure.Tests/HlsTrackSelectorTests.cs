using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class HlsTrackSelectorTests
{
    private static HlsManifestInfo Sample() => new(true,
    [
        new HlsVariant(new Uri("https://cdn.test/360.m3u8"), 640, 360, 800_000, 30, "aud"),
        new HlsVariant(new Uri("https://cdn.test/720.m3u8"), 1280, 720, 1_400_000, 60, "aud"),
        new HlsVariant(new Uri("https://cdn.test/1080.m3u8"), 1920, 1080, 3_000_000, 30, "aud")
    ],
    [
        new HlsAudioRendition(new Uri("https://cdn.test/ru.m3u8"), "ru", "Russian", "aud", true),
        new HlsAudioRendition(new Uri("https://cdn.test/en.m3u8"), "en", "English", "aud", false)
    ], false, null, false);

    [Theory]
    [InlineData("best", 1080)]
    [InlineData("1080p", 1080)]
    [InlineData("720p", 720)]
    public void Selects_video_for_requested_quality_and_default_audio(string quality, int expectedHeight)
    {
        var plan = HlsTrackSelector.Select(Sample(), quality);
        Assert.Equal(expectedHeight, plan!.Video.Height);
        Assert.Equal("ru", plan.Audio!.Language);
    }

    [Theory]
    [InlineData("1440p", 1440)]
    [InlineData("2160p", 2160)]
    [InlineData("4K", 2160)]
    [InlineData("4320p", 4320)]
    [InlineData("best", 4320)]
    public void Audit_A003_Dynamic_heights_remain_distinct_from_best(string quality, int expectedHeight)
    {
        var manifest = Sample() with { Variants = new[] { 720, 1440, 2160, 4320 }
            .Select(height => new HlsVariant(new Uri($"https://cdn.test/{height}.m3u8"), null, height, height * 1000, null, null)).ToArray() };
        Assert.Equal(expectedHeight, HlsTrackSelector.Select(manifest, quality)!.Video.Height);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("0p")]
    [InlineData("2147483648p")]
    [InlineData("７２０p")]
    public void Audit_A003_Invalid_quality_is_rejected(string quality)
    {
        Assert.Null(HlsTrackSelector.Select(Sample(), quality));
        var queue = new BrowserDownloadQueue();
        Assert.Throws<ArgumentException>(() => queue.AddOrUpdate(new(new Uri("https://cdn.test/master.m3u8"), new Uri("https://site.test/lesson"), "HLS"), 1, quality));
        Assert.Empty(queue.Items);
    }
}
