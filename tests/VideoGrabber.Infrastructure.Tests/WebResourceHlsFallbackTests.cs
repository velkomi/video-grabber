using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class WebResourceHlsFallbackTests
{
    [Fact]
    public void Clear_extensionless_HLS_body_becomes_verified_queueable_candidate()
    {
        const string body = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION=1280x720\nvideo.m3u8\n";
        var source = new Uri("https://vhapi02.getcourse.ru/api/playlist/master/abc?sig=secret");
        var referer = new Uri("https://iglyrazuma.ru/lesson");

        Assert.True(HlsResponseCandidateResolver.TryResolve(body, source, referer, out var candidate, out var blocked));
        Assert.False(blocked);
        Assert.NotNull(candidate);
        Assert.NotNull(candidate!.HlsManifest);
        Assert.True(MediaCandidatePolicy.CanQueue(candidate));
        Assert.Equal(source, candidate.Source);
        Assert.DoesNotContain("secret", candidate.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void Encrypted_HLS_body_is_detected_but_not_queueable()
    {
        const string body = "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key.bin\"\n#EXTINF:5,\nseg.ts\n";
        var source = new Uri("https://cdn.example/media");
        var referer = new Uri("https://school.example/lesson");

        Assert.True(HlsResponseCandidateResolver.TryResolve(body, source, referer, out var candidate, out var blocked));
        Assert.True(blocked);
        Assert.Null(candidate);
    }
}