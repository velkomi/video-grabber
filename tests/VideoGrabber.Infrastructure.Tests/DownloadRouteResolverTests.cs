using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadRouteResolverTests
{
    [Fact]
    public void Detected_HLS_inherits_active_course_adapter_but_unrelated_host_does_not()
    {
        var saved = new SiteRouteSettings([new("iglyrazuma.ru", "ethernet")]);
        var policy = new SiteRoutePolicy(saved);
        var lesson = new Uri("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1");
        Assert.Equal("ethernet", DownloadRouteResolver.ResolveAdapterId(policy, saved, lesson, null));

        var hls = new Uri("https://gc77.vhcdn.com/master.m3u8?token=private");
        var player = new Uri("https://api2.gcvh.ru/sign-player/?json=private");
        Assert.Equal("ethernet", DownloadRouteResolver.ResolveAdapterId(policy, saved, hls, player));
        Assert.Null(DownloadRouteResolver.ResolveAdapterId(policy, saved, new Uri("https://example.com/video.m3u8"), null));
    }
}

public sealed partial class DownloadRouteResolverOwnershipTests
{
    [Fact]
    public void Direct_course_or_lesson_referer_can_own_session_but_child_cdn_alone_cannot()
    {
        var saved = new SiteRouteSettings([new("iglyrazuma.ru", "ethernet")]);
        var lesson = new Uri("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1");
        var hls = new Uri("https://gc77.vhcdn.com/master.m3u8");

        Assert.True(DownloadRouteResolver.WouldConfigureSession(saved, lesson, null));
        Assert.True(DownloadRouteResolver.WouldConfigureSession(saved, hls, lesson));
        Assert.False(DownloadRouteResolver.WouldConfigureSession(saved, hls, null));
        Assert.False(DownloadRouteResolver.WouldConfigureSession(saved, new Uri("https://example.com/video"), null));
    }
}
