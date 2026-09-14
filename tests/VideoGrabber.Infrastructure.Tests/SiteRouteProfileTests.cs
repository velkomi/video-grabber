using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class SiteRouteProfileTests
{
    private static readonly string[] Legacy =
    [
        "fs.getcourse.ru", "ws04.getcourse.ru", "api2.gcvh.ru",
        "vh-asset-static2.kinescopecdn.net", "v01.getcourse.ru",
        "vh-75-integros.kinescopecdn.net"
    ];

    [Fact]
    public void Iglyrazuma_profile_persists_only_explicit_root_rule()
    {
        var settings = SiteRouteProfiles.Merge(
            new SiteRouteSettings([new("unrelated.example", "other")]),
            "iglyrazuma.ru", "ethernet");
        Assert.Equal("ethernet", settings.Find("iglyrazuma.ru")!.AdapterId);
        Assert.Equal("other", settings.Find("unrelated.example")!.AdapterId);
        Assert.All(Legacy, host => Assert.Null(settings.Find(host)));
        Assert.Equal(2, settings.Rules.Count);
    }

    [Fact]
    public void Merge_preserves_partial_legacy_rules_because_they_may_be_user_created()
    {
        var current = new SiteRouteSettings([
            new("iglyrazuma.ru", "ethernet"), new("api2.gcvh.ru", "ethernet"),
            new("v01.getcourse.ru", "ethernet"), new("unrelated.example", "other")]);
        var settings = SiteRouteProfiles.Merge(current, "iglyrazuma.ru", "ethernet");
        Assert.NotNull(settings.Find("api2.gcvh.ru"));
        Assert.NotNull(settings.Find("v01.getcourse.ru"));
    }

    [Fact]
    public void Merge_prunes_only_complete_legacy_auto_profile_with_same_adapter()
    {
        var rules = new List<SiteRouteRule> { new("iglyrazuma.ru", "ethernet"), new("unrelated.example", "other") };
        rules.AddRange(Legacy.Select(host => new SiteRouteRule(host, "ethernet")));
        var settings = SiteRouteProfiles.Merge(new SiteRouteSettings(rules), "iglyrazuma.ru", "ethernet");
        Assert.Equal(["iglyrazuma.ru", "unrelated.example"], settings.Rules.Select(r => r.Host).Order().ToArray());
    }
}

public sealed class SiteRouteProfileMigrationTests
{
    [Fact]
    public void NormalizePersisted_removes_complete_legacy_auto_profile()
    {
        string[] legacy =
        [
            "fs.getcourse.ru", "ws04.getcourse.ru", "api2.gcvh.ru",
            "vh-asset-static2.kinescopecdn.net", "v01.getcourse.ru",
            "vh-75-integros.kinescopecdn.net"
        ];
        var rules = new List<SiteRouteRule> { new("iglyrazuma.ru", "ethernet"), new("other.example", "other") };
        rules.AddRange(legacy.Select(host => new SiteRouteRule(host, "ethernet")));
        var normalized = SiteRouteProfiles.NormalizePersisted(new SiteRouteSettings(rules));
        Assert.Equal(["iglyrazuma.ru", "other.example"], normalized.Rules.Select(r => r.Host).Order().ToArray());
    }
}
