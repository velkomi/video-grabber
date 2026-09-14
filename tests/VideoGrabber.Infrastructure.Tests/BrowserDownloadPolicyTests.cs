using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadPolicyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("chrome", false)]
    [InlineData("embedded", true)]
    public void Embedded_session_is_used_only_after_explicit_selection(string? selection, bool expected)
        => Assert.Equal(expected, BrowserDownloadSessionPolicy.UseEmbeddedSession(selection));

    [Fact]
    public void Hls_download_plan_uses_current_quality_and_audio_choice()
    {
        var info = new HlsManifestInfo(true,
        [
            new HlsVariant(new Uri("https://cdn.example/360.m3u8"), 640, 360, 800000, 30, "aud"),
            new HlsVariant(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1500000, 30, "aud"),
            new HlsVariant(new Uri("https://cdn.example/1080.m3u8"), 1920, 1080, 3000000, 30, "aud")
        ],
        [new HlsAudioRendition(new Uri("https://cdn.example/ru.m3u8"), "ru", "Russian", "aud", true)],
        false, null, false);
        var candidate = new MediaCandidate(new Uri("https://cdn.example/master.m3u8"), new Uri("https://school.example/lesson"), "HLS", "master", HlsManifest: info);

        var video = MediaDownloadPlanResolver.Resolve(candidate, "720p", audioOnly: false);
        Assert.Equal("https://cdn.example/720.m3u8", video.HlsVideoSource!.AbsoluteUri);
        Assert.Equal("https://cdn.example/ru.m3u8", video.HlsAudioSource!.AbsoluteUri);

        var audio = MediaDownloadPlanResolver.Resolve(candidate, "1080p", audioOnly: true);
        Assert.Equal("https://cdn.example/ru.m3u8", audio.Source.AbsoluteUri);
        Assert.Null(audio.HlsVideoSource);
        Assert.Null(audio.HlsAudioSource);
    }
}
