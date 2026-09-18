using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SourceAnalysisSecurityTests
{
    [Theory]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("http://10.0.0.1/private")]
    [InlineData("http://192.168.1.1/private")]
    [InlineData("http://[::1]/private")]
    public async Task Public_source_analysis_rejects_private_targets_before_tool_execution(string source)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "source-policy-" + Guid.NewGuid().ToString("N"));
        using var response = await account.Client.PostAsJsonAsync(
            "/v1/sources/analyze", new AnalyzeSourceRequest(new Uri(source)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_source_analysis_rejects_userinfo()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "source-userinfo");
        using var response = await account.Client.PostAsJsonAsync(
            "/v1/sources/analyze",
            new AnalyzeSourceRequest(new Uri("https://user:secret@example.test/video")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Worker_source_handle_requires_private_worker_token()
    {
        await using var f = await ApiFixture.StartAsync();
        using var denied = await f.Anonymous.GetAsync(
            "/v1/worker/sources/src_missing?quality=720p");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/v1/worker/sources/src_missing?quality=720p");
        request.Headers.Add("X-VideoGrabber-Worker-Token", "test-server-worker-token");
        using var missing = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
