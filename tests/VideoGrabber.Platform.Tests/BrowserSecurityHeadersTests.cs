using System.Net;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class BrowserSecurityHeadersTests
{
    [Theory]
    [InlineData("/info/")]
    [InlineData("/miniapp/")]
    public async Task Static_pages_receive_report_only_policy_and_safe_headers(string path)
    {
        await using var fixture = await ApiFixture.StartAsync();
        fixture.Anonymous.BaseAddress = new Uri("https://localhost");
        using var response = await fixture.Anonymous.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = string.Join("", response.Headers.GetValues("Content-Security-Policy-Report-Only"));
        Assert.Contains("worker-src 'self' blob:", policy);
        Assert.Contains("report-uri /v1/security/csp-report", policy);
        Assert.DoesNotContain("Content-Security-Policy", response.Headers.Select(x => x.Key));
        Assert.Contains(path.StartsWith("/miniapp") ? "https://web.telegram.org" : "frame-ancestors 'self'", policy);
        Assert.Equal("max-age=86400", string.Join("", response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Fact]
    public async Task Report_collector_bounds_body_and_discards_report_content()
    {
        await using var fixture = await ApiFixture.StartAsync();
        using var small = await fixture.Anonymous.PostAsync("/v1/security/csp-report",
            new StringContent("{\"csp-report\":{\"blocked-uri\":\"https://private.test/?secret=never-log-me\"}}"));
        Assert.Equal(HttpStatusCode.NoContent, small.StatusCode);
        using var large = await fixture.Anonymous.PostAsync("/v1/security/csp-report", new StringContent(new string('x', 16385)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        Assert.DoesNotContain(fixture.Logs, x => x.Contains("never-log-me"));
    }
}
