using System.Net;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class FinalHardeningTests
{
    [Fact]
    public async Task RouteConnector_rejects_stale_session_lease_after_dns()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet-a")]));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet-a"));
        var connector = new RouteConnector(policy, async (_, _) =>
        {
            policy.ConfigureSession("iglyrazuma.ru", "ethernet-b");
            await Task.Yield();
            return [IPAddress.Parse("95.181.182.182")];
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.OpenAsync("gc79.vhcdn.com", 443, CancellationToken.None));
        Assert.Contains("сесс", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Hls_selector_never_borrows_audio_from_a_different_group()
    {
        var info = new HlsManifestInfo(true,
            [new HlsVariant(new Uri("https://cdn.test/v.m3u8"), 1280, 720, 1_000_000, 30, "ru-group")],
            [new HlsAudioRendition(new Uri("https://cdn.test/en.m3u8"), "en", "English", "en-group", true)],
            false, null, false);
        var plan = HlsTrackSelector.Select(info, "best");
        Assert.NotNull(plan);
        Assert.Null(plan!.Audio);
    }

    [Theory]
    [InlineData(8080)]
    [InlineData(8443)]
    public void Alternate_web_ports_are_rejected(int port)
        => Assert.Throws<ArgumentException>(() => RouteConnector.ValidateProxyTarget("example.com", port));
    [Theory]
    [InlineData("chrome")]
    [InlineData("edge")]
    [InlineData("firefox")]
    [InlineData("embedded")]
    public void Every_nonempty_cookie_mode_is_one_shot(string mode)
        => Assert.True(BrowserDownloadSessionPolicy.ShouldResetAfterUse(mode));

    [Fact]
    public void Legacy_migration_preserves_partial_or_custom_dependency_rules()
    {
        var settings = new SiteRouteSettings([
            new("iglyrazuma.ru", "ethernet"),
            new("api2.gcvh.ru", "custom-adapter")
        ]);
        var normalized = SiteRouteProfiles.NormalizePersisted(settings);
        Assert.Equal("custom-adapter", normalized.Find("api2.gcvh.ru")!.AdapterId);
    }
}
