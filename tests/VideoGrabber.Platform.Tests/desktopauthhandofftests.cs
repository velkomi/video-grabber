using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DesktopAuthHandoffTests
{
    [Fact]
    public async Task Desktop_handoff_issues_one_time_session_for_same_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "desktop-handoff-user");

        var startResponse = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/start",
            new DesktopSignInStartRequest(
                new Uri("http://127.0.0.1:54321/videograbber-auth/callback")));
        startResponse.EnsureSuccessStatusCode();
        var started = (await startResponse.Content.ReadFromJsonAsync<DesktopSignInStart>())!;

        Assert.Contains(
            "desktop_flow=" + Uri.EscapeDataString(started.FlowId.ToString("D")),
            started.VerificationUri.Query,
            StringComparison.Ordinal);
        Assert.Contains(
            "desktop_state=",
            started.VerificationUri.Query,
            StringComparison.Ordinal);

        var approve = await account.Client.PostAsJsonAsync(
            "/v1/auth/desktop/approve",
            new DesktopSignInApprovalRequest(started.FlowId, started.State));
        approve.EnsureSuccessStatusCode();
        var approved = (await approve.Content.ReadFromJsonAsync<DesktopSignInApproval>())!;

        Assert.Equal(
            "http://127.0.0.1:54321/videograbber-auth/callback",
            approved.ReturnUri.AbsoluteUri);
        Assert.Equal(started.State, approved.State);
        Assert.False(string.IsNullOrWhiteSpace(approved.Code));

        var consume = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/consume",
            new DesktopSignInConsumeRequest(
                started.FlowId,
                approved.Code,
                approved.State));
        consume.EnsureSuccessStatusCode();
        var session = (await consume.Content.ReadFromJsonAsync<ApiSession>())!;

        using var desktop = f.SessionClient(session);
        var profile = await desktop.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(profile);
        Assert.Equal(account.Id, profile!.AccountId);

        using var replay = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/consume",
            new DesktopSignInConsumeRequest(
                started.FlowId,
                approved.Code,
                approved.State));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost:54321/videograbber-auth/callback")]
    [InlineData("http://127.0.0.1:54321/other")]
    [InlineData("https://example.test/videograbber-auth/callback")]
    public async Task Desktop_handoff_rejects_non_exact_loopback_return(string uri)
    {
        await using var f = await ApiFixture.StartAsync();

        using var response = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/start",
            new DesktopSignInStartRequest(new Uri(uri)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Desktop_approval_requires_authenticated_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var startResponse = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/start",
            new DesktopSignInStartRequest(
                new Uri("http://127.0.0.1:54322/videograbber-auth/callback")));
        var started = (await startResponse.Content.ReadFromJsonAsync<DesktopSignInStart>())!;

        using var approve = await f.Anonymous.PostAsJsonAsync(
            "/v1/auth/desktop/approve",
            new DesktopSignInApprovalRequest(started.FlowId, started.State));

        Assert.Equal(HttpStatusCode.Unauthorized, approve.StatusCode);
    }
}
