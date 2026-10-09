using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoGrabber.Platform.Api.Security;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class AuthRateLimitIsolationTests
{
    [Fact]
    public async Task Untrusted_forwarded_header_is_ignored()
    {
        var options = new ForwardedHeadersOptions();
        ClientRateLimitPolicies.ConfigureProxyOptions(options,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["VG_TRUSTED_PROXY_IPS"] = "192.0.2.10;192.0.2.11" }).Build());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(options));

        await middleware.Invoke(context);

        Assert.Equal("ip:198.51.100.10", ClientRateLimitPolicies.CallerKey(context));
    }

    [Fact]
    public async Task Trusted_proxy_chain_recovers_client_without_trusting_injected_prefix()
    {
        var options = new ForwardedHeadersOptions();
        ClientRateLimitPolicies.ConfigureProxyOptions(options,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["VG_TRUSTED_PROXY_IPS"] = "192.0.2.10;192.0.2.11" }).Build());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99, 198.51.100.10, 192.0.2.11";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(options));

        await middleware.Invoke(context);

        Assert.Equal("ip:198.51.100.10", ClientRateLimitPolicies.CallerKey(context));
    }

    [Fact]
    public async Task Client_A_exhaustion_does_not_block_B()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "rate-client-a");
        var b = await f.AccountAsync("google", "rate-client-b");
        for (var i = 0; i < 40; i++)
        {
            using var ignored = await a.Client.GetAsync("/v1/auth/supabase-config");
        }
        using var response = await b.Client.GetAsync("/v1/auth/supabase-config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Public_config_does_not_consume_login_budget()
    {
        await using var f = await ApiFixture.StartAsync();
        for (var i = 0; i < 25; i++)
        {
            using var ignored = await f.Anonymous.GetAsync("/v1/auth/supabase-config");
        }
        using var response = await f.Anonymous.PostAsync("/v1/auth/refresh",
            new StringContent("{\"refreshToken\":\"invalid-test-token\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
