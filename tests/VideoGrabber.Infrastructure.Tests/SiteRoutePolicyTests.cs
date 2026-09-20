using System.Net;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class SiteRoutePolicyTests
{
    [Fact]
    public void Transport_failure_on_auto_physical_route_prefers_system_route_next()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([
            new("iglyrazuma.ru", RouteConnector.AutoPhysicalAdapterId)
        ]));
        Assert.True(policy.ConfigureSession(
            "iglyrazuma.ru",
            RouteConnector.AutoPhysicalAdapterId));

        policy.ReportRouteConnected(
            "vh-79-integros.kinescopecdn.net",
            usedSystemRoute: false);

        Assert.True(policy.ReportTransportFailure(
            "vh-79-integros.kinescopecdn.net"));
        Assert.True(policy.ShouldPreferSystemRoute(
            "vh-79-integros.kinescopecdn.net"));
    }

    [Fact]
    public void Transport_failure_on_system_route_returns_auto_mode_to_physical_first()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([
            new("iglyrazuma.ru", RouteConnector.AutoPhysicalAdapterId)
        ]));
        Assert.True(policy.ConfigureSession(
            "iglyrazuma.ru",
            RouteConnector.AutoPhysicalAdapterId));

        policy.ReportRouteConnected(
            "vh-79-integros.kinescopecdn.net",
            usedSystemRoute: false);
        Assert.True(policy.ReportTransportFailure(
            "vh-79-integros.kinescopecdn.net"));
        Assert.True(policy.ShouldPreferSystemRoute(
            "vh-79-integros.kinescopecdn.net"));

        policy.ReportRouteConnected(
            "vh-79-integros.kinescopecdn.net",
            usedSystemRoute: true);
        Assert.False(policy.ReportTransportFailure(
            "vh-79-integros.kinescopecdn.net"));
        Assert.False(policy.ShouldPreferSystemRoute(
            "vh-79-integros.kinescopecdn.net"));
    }

    [Fact]
    public void Manual_adapter_transport_failure_never_enables_system_fallback_preference()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([
            new("iglyrazuma.ru", "ethernet")
        ]));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet"));
        policy.ReportRouteConnected(
            "vh-79-integros.kinescopecdn.net",
            usedSystemRoute: false);

        Assert.False(policy.ReportTransportFailure(
            "vh-79-integros.kinescopecdn.net"));
        Assert.False(policy.ShouldPreferSystemRoute(
            "vh-79-integros.kinescopecdn.net"));
    }

    [Fact]
    public void Existing_policy_reference_observes_updated_rules()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([]));
        Assert.Null(policy.Find("api2.gcvh.ru"));
        policy.Update(new SiteRouteSettings([new("api2.gcvh.ru", "ethernet")]));
        Assert.Equal("ethernet", policy.Find("api2.gcvh.ru")!.AdapterId);
    }

    [Theory]
    [InlineData("api3.gcvh.ru")]
    [InlineData("v02.getcourse.ru")]
    [InlineData("vh-79-integros.kinescopecdn.net")]
    [InlineData("vh-22.servicecdn.ru")]
    [InlineData("vh-25.servicecdn.ru")]
    [InlineData("gc79.vhcdn.com")]
    [InlineData("hcndwxefvi.a.trbcdn.net")]
    [InlineData("vhapi02.gcfiles.net")]
    [InlineData("kinescope.io")]
    public void Iglyrazuma_session_routes_known_media_families(string host)
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet")]));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet"));
        Assert.Equal("ethernet", policy.ResolveAdapterId(host));
        Assert.Null(policy.ResolveAdapterId("example.com"));
        Assert.Null(policy.ResolveAdapterId("evilgetcourse.ru"));
    }

    [Fact]
    public void Auto_physical_route_is_inherited_by_GetCourse_servicecdn()
    {
        var policy = new SiteRoutePolicy(
            new SiteRouteSettings(
            [
                new(
                    "iglyrazuma.ru",
                    RouteConnector.AutoPhysicalAdapterId)
            ]));

        Assert.True(
            policy.ConfigureSession(
                "iglyrazuma.ru",
                RouteConnector.AutoPhysicalAdapterId));
        Assert.Equal(
            RouteConnector.AutoPhysicalAdapterId,
            policy.ResolveAdapterId("vh-23.servicecdn.ru"));
    }

    [Fact]
    public void Session_allows_only_IPs_resolved_from_routed_media_hosts()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet")]));
        policy.ConfigureSession("iglyrazuma.ru", "ethernet");
        policy.RememberResolvedAddresses("gc79.vhcdn.com", [IPAddress.Parse("95.181.182.182")]);
        Assert.Equal("ethernet", policy.ResolveAdapterId("95.181.182.182"));
        Assert.Null(policy.ResolveAdapterId("8.8.8.8"));
        policy.ClearSession();
        Assert.Null(policy.ResolveAdapterId("95.181.182.182"));
    }
}

public sealed partial class SiteRoutePolicyTests
{
    [Fact]
    public void Child_media_host_does_not_clear_existing_course_session()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet")]));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet"));
        Assert.Equal("ethernet", policy.ResolveAdapterId("v02.getcourse.ru"));

        Assert.False(policy.ConfigureSession("api2.gcvh.ru", "ethernet"));

        Assert.Equal("ethernet", policy.ResolveAdapterId("v02.getcourse.ru"));
        Assert.Equal("ethernet", policy.ResolveAdapterId("vh-79-integros.kinescopecdn.net"));
    }
}

public sealed partial class SiteRoutePolicyTests
{
[Fact]
        public void Updating_rules_clears_active_session_until_reconfigured()
        {
            var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet-a")]));
            Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet-a"));
            Assert.Equal("ethernet-a", policy.ResolveAdapterId("gc77.vhcdn.com"));
            policy.Update(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet-b")]));
            Assert.Null(policy.ResolveAdapterId("gc77.vhcdn.com"));
            Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet-b"));
            Assert.Equal("ethernet-b", policy.ResolveAdapterId("gc77.vhcdn.com"));
        }
}

public sealed partial class SiteRoutePolicyTests
{
    [Fact]
    public void Exact_rule_is_not_an_active_course_session()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("v01.getcourse.ru", "ethernet")]));
        Assert.False(policy.IsActiveSessionHost("v01.getcourse.ru"));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet"));
        Assert.True(policy.IsActiveSessionHost("v01.getcourse.ru"));
    }
}

public sealed partial class SiteRoutePolicyTests
{
    [Fact]
    public void Stale_dns_result_cannot_write_old_adapter_into_new_session()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("iglyrazuma.ru", "ethernet-a")]));
        Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet-a"));
        var ip = IPAddress.Parse("95.181.182.182");

        IEnumerable<IPAddress> StaleDnsResult()
        {
            Assert.True(policy.ConfigureSession("iglyrazuma.ru", "ethernet-b"));
            yield return ip;
        }

        policy.RememberResolvedAddresses("gc79.vhcdn.com", StaleDnsResult());
        Assert.Null(policy.ResolveAdapterId(ip.ToString()));
        Assert.Equal("ethernet-b", policy.ResolveAdapterId("gc79.vhcdn.com"));
    }
}

public sealed partial class SiteRoutePolicyTests
{
    [Fact]
    public void Custom_domain_GetCourse_session_routes_known_media_families()
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("academy.example", "ethernet")]));
        Assert.True(policy.ConfigureSession("academy.example", "ethernet", allowCustomRoot: true));
        Assert.Equal("ethernet", policy.ResolveAdapterId("api3.gcvh.ru"));
        Assert.Equal("ethernet", policy.ResolveAdapterId("vh-23.servicecdn.ru"));
        Assert.Equal("ethernet", policy.ResolveAdapterId("vh-79-integros.kinescopecdn.net"));
    }

    [Theory]
    [InlineData("api2.gcvh.ru")]
    [InlineData("vhapi02.gcfiles.net")]
    [InlineData("vh-23.servicecdn.ru")]
    [InlineData("kinescope.io")]
    public void Provider_hosts_cannot_replace_custom_course_root_session(string host)
    {
        var policy = new SiteRoutePolicy(new SiteRouteSettings([new("academy.example", "ethernet")]));
        Assert.True(policy.ConfigureSession("academy.example", "ethernet", allowCustomRoot: true));
        Assert.False(policy.ConfigureSession(host, "vpn"));
        Assert.Equal("ethernet", policy.ResolveAdapterId("vh-23.servicecdn.ru"));
    }
}