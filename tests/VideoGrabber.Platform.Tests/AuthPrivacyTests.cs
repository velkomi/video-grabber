using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class AuthPrivacyTests
{
    [Fact]
    public async Task Browser_cookie_mutation_without_csrf_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = "not-a-real-refresh" })
        };
        request.Headers.Add("Cookie", "vg_session=browser-cookie");

        var response = await f.Anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_origin_preflight_is_denied()
    {
        await using var f = await ApiFixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/start");
        request.Headers.Add("Origin", "https://evil.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await f.Anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Auth_start_is_rate_limited()
    {
        await using var f = await ApiFixture.StartAsync();
        var body = new BeginSignIn("google",
            new Uri("https://client.example.test/auth/complete"), "rate-limit-challenge");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 30; i++)
            statuses.Add((await f.Anonymous.PostAsJsonAsync("/v1/auth/start", body)).StatusCode);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Unsupported_api_version_is_rejected_before_mutation()
    {
        await using var f = await ApiFixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/start")
        {
            Content = JsonContent.Create(new BeginSignIn("google",
                new Uri("https://client.example.test/auth/complete"), "version-challenge"))
        };
        request.Headers.Add("X-VideoGrabber-Api-Version", "999");
        var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }
    [Fact]
    public async Task Allowed_origin_preflight_echoes_only_exact_origin()
    {
        await using var f = await ApiFixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/auth/start");
        request.Headers.Add("Origin", "https://miniapp.example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://miniapp.example.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

}
