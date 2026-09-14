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
}
