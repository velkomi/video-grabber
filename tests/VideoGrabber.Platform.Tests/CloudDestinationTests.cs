using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoGrabber.Platform.Api.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class CloudDestinationTests
{
    [Theory]
    [InlineData(null, "true", "true", true, HttpStatusCode.Unauthorized)]
    [InlineData("customer", "true", "true", false, HttpStatusCode.OK)]
    [InlineData("owner_admin", "true", "true", true, HttpStatusCode.OK)]
    [InlineData("customer", "true", "false", true, HttpStatusCode.OK)]
    [InlineData("owner_admin", null, "true", false, HttpStatusCode.OK)]
    public async Task Cloud_config_enforces_session_feature_flag_and_owner_preview(
        string? role, string? enabled, string ownerOnly, bool exposed, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VG_CLOUD_GOOGLE_ENABLED"] = enabled,
            ["VG_CLOUD_GOOGLE_OWNER_ONLY"] = ownerOnly,
            ["VG_CLOUD_GOOGLE_CLIENT_ID"] = "123456-test.apps.googleusercontent.com"
        });
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCloudDestinationEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/v1/cloud/config");
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) return;
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(exposed, body.Contains("123456-test.apps.googleusercontent.com", StringComparison.Ordinal));
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", role)], "Test")), "Test")));
        }
    }
}
