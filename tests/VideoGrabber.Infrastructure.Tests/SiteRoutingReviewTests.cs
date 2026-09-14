using VideoGrabber.Infrastructure.Networking;
namespace VideoGrabber.Infrastructure.Tests;
public sealed partial class SiteRoutingReviewTests
{
    [Theory]
    [InlineData("co.uk")][InlineData("github.io")][InlineData("example.com")]
    public void Recursive_rules_are_rejected_in_this_exact_host_release(string host)
        => Assert.Throws<ArgumentException>(() => new SiteRouteSettings([new(host, "adapter", true)]));
    [Theory]
    [InlineData("8.8.8.8")][InlineData("2606:4700:4700::1111")]
    public void Proxy_refuses_literal_IP_that_cannot_be_matched_to_domain_policy(string host)
        => Assert.Throws<ArgumentException>(() => RouteConnector.ValidateTarget(host, 443));
    [Theory]
    [InlineData(8080)][InlineData(8443)]
    public void Alternate_web_ports_are_rejected(int port)
        => Assert.Throws<ArgumentException>(() => RouteConnector.ValidateTarget("example.com", port));
}

public sealed partial class SiteRoutingReviewTests
{
    [Fact]
    public void Rejected_proxy_port_message_contains_the_actual_port()
    {
        var ex = Assert.Throws<ArgumentException>(() => RouteConnector.ValidateProxyTarget("example.com", 9443));
        Assert.Contains("9443", ex.Message, StringComparison.Ordinal);
    }
}


public sealed partial class SiteRoutingReviewTests
{
    [Fact]
    public void GetCourse_realtime_port_is_accepted_only_for_GetCourse_hosts()
    {
        RouteConnector.ValidateProxyTarget("v01.getcourse.ru", 3001);
        Assert.Throws<ArgumentException>(() => RouteConnector.ValidateProxyTarget("example.com", 3001));
        Assert.Throws<ArgumentException>(() => RouteConnector.ValidateProxyTarget("evilgetcourse.ru", 3001));
    }

    [Fact]
    public async Task GetCourse_realtime_port_requires_active_routed_session()
    {
        var connector = new RouteConnector(new SiteRouteSettings([]));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenAsync("v01.getcourse.ru", 3001, CancellationToken.None));
    }
}

public sealed partial class SiteRoutingReviewTests
{
    [Fact]
    public async Task Persisted_exact_rule_does_not_unlock_GetCourse_realtime_port_without_session()
    {
        var connector = new RouteConnector(new SiteRouteSettings([new("v01.getcourse.ru", "fake-adapter")]));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenAsync("v01.getcourse.ru", 3001, CancellationToken.None));
    }
}
