using System.Net;
using VideoGrabber.Infrastructure.Networking;
namespace VideoGrabber.Infrastructure.Tests;
public sealed class SiteRouteTests
{
    [Theory]
    [InlineData("https://IGLYRAZUMA.ru/pl/teach/view?id=123", "iglyrazuma.ru")]
    [InlineData("sub.example.com.", "sub.example.com")]
    public void Normalizes_host_without_storing_path(string input, string host)
        => Assert.Equal(host, SiteRouteSettings.NormalizeHost(input));
    [Theory]
    [InlineData("*.ru")][InlineData("ru")][InlineData("localhost")]
    [InlineData("127.0.0.1")][InlineData("192.168.1.1")][InlineData("https://user:password@example.com")]
    [InlineData("http://example.com:8080")][InlineData("evil.com\r\nHost:x")]
    public void Rejects_broad_or_unsafe_rules(string input)
        => Assert.Throws<ArgumentException>(() => SiteRouteSettings.NormalizeHost(input));
    [Fact]
    public void Exact_rules_never_match_suffix_lookalikes_or_other_subdomains()
    {
        var settings = new SiteRouteSettings([new("example.com", "parent"), new("a.example.com", "child")]);
        Assert.Equal("child", settings.Find("a.example.com")!.AdapterId);
        Assert.Null(settings.Find("b.example.com"));
        Assert.Null(settings.Find("example.com.evil.net")); Assert.Null(settings.Find("evil-example.com"));
        Assert.Null(new SiteRouteSettings([new("example.com", "x")]).Find("a.example.com"));
    }
    [Fact]
    public void Stores_rules_atomically_and_rejects_corrupt_settings()
    {
        var dir=Path.Combine(Path.GetTempPath(),"vg-route-"+Guid.NewGuid().ToString("N"));
        var path=Path.Combine(dir,"routes.json");
        try {
            Assert.Empty(SiteRouteSettings.Load(path).Rules);
            new SiteRouteSettings([new("example.com","adapter")]).Save(path);
            Assert.Equal("adapter", Assert.Single(SiteRouteSettings.Load(path).Rules).AdapterId);
            new SiteRouteSettings([]).Save(path); Assert.Empty(SiteRouteSettings.Load(path).Rules);
            File.WriteAllText(path,"{broken"); Assert.ThrowsAny<Exception>(()=>SiteRouteSettings.Load(path));
        } finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }
    [Theory]
    [InlineData("0.1.2.3")][InlineData("10.0.0.1")][InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")][InlineData("172.16.0.1")][InlineData("192.168.0.1")]
    [InlineData("100.64.0.1")][InlineData("224.0.0.1")][InlineData("255.255.255.255")]
    [InlineData("::1")][InlineData("::")][InlineData("fc00::1")][InlineData("fe80::1")]
    [InlineData("::ffff:127.0.0.1")][InlineData("2001:db8::1")]
    public void Blocks_nonpublic_proxy_targets(string ip) => Assert.False(RouteConnector.IsPublic(IPAddress.Parse(ip)));
    [Theory]
    [InlineData("185.65.148.19")][InlineData("8.8.8.8")][InlineData("2606:4700:4700::1111")]
    public void Accepts_public_targets(string ip) => Assert.True(RouteConnector.IsPublic(IPAddress.Parse(ip)));
    [Fact]
    public async Task Missing_selected_adapter_does_not_fall_back_to_default_route()
    {
        var connector=new RouteConnector(new SiteRouteSettings([new("example.com","nonexistent-adapter")]));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>connector.OpenAsync("example.com",443,CancellationToken.None));
    }
}
