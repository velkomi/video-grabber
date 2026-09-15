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

public sealed partial class DownloadRouteResolverOwnershipTests
{
    [Theory]
    [InlineData("complete")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task Late_family_session_and_proxy_are_released_on_operation_exit(string outcome)
    {
        var saved = new SiteRouteSettings([new("master.example", "exact-adapter"), new("iglyrazuma.ru", "family-adapter")]);
        var policy = new SiteRoutePolicy(saved);
        var master = new Uri("https://master.example/master.m3u8");
        var oldReferer = new Uri("https://page.example/lesson");
        var leaf = new Uri("https://gc77.vhcdn.com/720.m3u8");
        var refreshedReferer = new Uri("https://iglyrazuma.ru/lesson");
        Assert.False(DownloadRouteResolver.WouldConfigureSession(saved, master, oldReferer));
        Assert.Null(policy.ResolveAdapterId(leaf.Host));
        SiteRouteProxy? proxy = null;
        var scope = new DownloadRouteScope(policy);
        try
        {
            var masterProxy = scope.ResolveProxy(saved, master, oldReferer);
            Assert.NotNull(masterProxy);
            Assert.Null(policy.ResolveAdapterId(leaf.Host));
            // Binding refresh/preflight occurs after the operation's initial identity was captured.
            await Task.Yield();
            proxy = scope.ResolveProxy(saved, leaf, refreshedReferer);
            Assert.NotNull(proxy);
            Assert.Same(masterProxy, proxy);
            Assert.Equal("family-adapter", policy.ResolveAdapterId(leaf.Host));
            using (var occupied = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, proxy.Port))
            {
                occupied.ExclusiveAddressUse = true;
                Assert.Throws<System.Net.Sockets.SocketException>(() => occupied.Start());
            }
            Assert.Same(proxy, scope.ResolveProxy(saved, leaf, refreshedReferer));
            if (outcome == "failure") throw new IOException("fixture failure");
            if (outcome == "cancel") throw new OperationCanceledException();
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        finally { scope.Dispose(); }
        try
        {
            Assert.Null(policy.ResolveAdapterId(leaf.Host));
            using var released = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, proxy!.Port);
            released.ExclusiveAddressUse = true;
            released.Start();
        }
        finally { proxy?.Dispose(); }
    }

    [Fact]
    public async Task Existing_browser_session_and_proxy_survive_download_scope()
    {
        var saved = new SiteRouteSettings([new("iglyrazuma.ru", "family-adapter")]);
        var policy = new SiteRoutePolicy(saved);
        policy.ConfigureSession("iglyrazuma.ru", "browser-adapter");
        var leaf = new Uri("https://gc77.vhcdn.com/720.m3u8");
        var lease = policy.CaptureLease(leaf.Host);
        using var browserProxy = new SiteRouteProxy((_, _, _) => throw new InvalidOperationException("No external connections in fixture."));
        using (var scope = new DownloadRouteScope(policy, browserProxy))
            Assert.Same(browserProxy, scope.ResolveProxy(saved, leaf, new Uri("https://iglyrazuma.ru/lesson")));
        Assert.Equal("browser-adapter", policy.ResolveAdapterId(leaf.Host));
        Assert.True(policy.IsLeaseCurrent(lease, leaf.Host));
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(System.Net.IPAddress.Loopback, browserProxy.Port).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(client.Connected);
    }

    [Fact]
    public void Newer_session_replacing_owned_session_survives_scope_cleanup()
    {
        var saved = new SiteRouteSettings([new("iglyrazuma.ru", "operation-adapter")]);
        var policy = new SiteRoutePolicy(saved);
        var lesson = new Uri("https://iglyrazuma.ru/lesson");
        const string mediaHost = "gc77.vhcdn.com";
        using var scope = new DownloadRouteScope(policy);
        using var proxy = scope.ResolveProxy(saved, lesson, null);
        Assert.Equal("operation-adapter", policy.ResolveAdapterId(mediaHost));
        policy.ConfigureSession("iglyrazuma.ru", "newer-browser-adapter");
        var newer = policy.CaptureLease(mediaHost);
        Assert.Same(proxy, scope.ResolveProxy(saved, lesson, null));
        Assert.True(policy.IsLeaseCurrent(newer, mediaHost));
        scope.Dispose();
        Assert.Equal("newer-browser-adapter", policy.ResolveAdapterId(mediaHost));
        Assert.True(policy.IsLeaseCurrent(newer, mediaHost));
    }
}
