using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class IdentityBrowserLinkTests
{
    [Fact]
    public async Task Browser_link_is_challenge_bound_listed_and_unlinkable()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "browser-link-owner");
        var challengeResponse = await account.Client.PostAsync("/v1/identities/link", null);
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<LinkChallenge>())!;

        const string verifier = "browser-link-verifier-0123456789";
        var startResponse = await account.Client.PostAsJsonAsync(
            "/v1/identities/link/browser/start",
            new BeginIdentityLink(challenge.ChallengeId, "telegram",
                new Uri("https://client.example.test/link/complete"), Pkce(verifier)));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = (await startResponse.Content.ReadFromJsonAsync<SignInStart>())!;
        var state = Query(started.AuthorizationUri, "state");
        var nonce = Query(started.AuthorizationUri, "nonce");
        Assert.Equal(challenge.ChallengeId.ToString("D"), nonce);
        var code = f.Broker.RegisterCode(f.Partition("telegram"), "linked-telegram-subject",
            nonce, f.Clock.GetUtcNow().AddMinutes(5));

        var completed = await account.Client.PostAsJsonAsync(
            "/v1/identities/link/browser/complete",
            new CompleteIdentityLink(challenge.ChallengeId, started.FlowId, code, state, verifier));
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        var identities = (await account.Client.GetFromJsonAsync<LinkedIdentity[]>("/v1/identities"))!;
        Assert.Equal(2, identities.Length);
        Assert.Contains(identities, x => x.Provider == "google");
        var telegram = Assert.Single(identities, x => x.Provider == "telegram");

        var unlink = await account.Client.DeleteAsync($"/v1/identities/{telegram.IdentityId:D}");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        identities = (await account.Client.GetFromJsonAsync<LinkedIdentity[]>("/v1/identities"))!;
        var only = Assert.Single(identities);
        Assert.Equal("google", only.Provider);

        var last = await account.Client.DeleteAsync($"/v1/identities/{only.IdentityId:D}");
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
    }

    [Fact]
    public async Task Browser_link_cannot_take_identity_owned_by_another_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AccountAsync("telegram", "already-owned-telegram");
        var target = await f.AccountAsync("google", "browser-link-target");
        var begun = await target.Client.PostAsync("/v1/identities/link", null);
        var challenge = (await begun.Content.ReadFromJsonAsync<LinkChallenge>())!;
        const string verifier = "owned-identity-verifier-0123456789";
        var start = await target.Client.PostAsJsonAsync(
            "/v1/identities/link/browser/start",
            new BeginIdentityLink(challenge.ChallengeId, "telegram",
                new Uri("https://client.example.test/link/complete"), Pkce(verifier)));
        var started = (await start.Content.ReadFromJsonAsync<SignInStart>())!;
        var state = Query(started.AuthorizationUri, "state");
        var nonce = Query(started.AuthorizationUri, "nonce");
        var code = f.Broker.RegisterCode(f.Partition("telegram"), "already-owned-telegram",
            nonce, f.Clock.GetUtcNow().AddMinutes(5));

        var complete = await target.Client.PostAsJsonAsync(
            "/v1/identities/link/browser/complete",
            new CompleteIdentityLink(challenge.ChallengeId, started.FlowId, code, state, verifier));

        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
        var targetIdentities = (await target.Client.GetFromJsonAsync<LinkedIdentity[]>("/v1/identities"))!;
        Assert.Single(targetIdentities);
        var ownerIdentities = (await owner.Client.GetFromJsonAsync<LinkedIdentity[]>("/v1/identities"))!;
        Assert.Contains(ownerIdentities, x => x.Provider == "telegram");
    }
    [Fact]
    public async Task Browser_link_start_cannot_use_another_accounts_challenge()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "browser-link-a");
        var b = await f.AccountAsync("google", "browser-link-b");
        var begun = await a.Client.PostAsync("/v1/identities/link", null);
        var challenge = (await begun.Content.ReadFromJsonAsync<LinkChallenge>())!;

        var response = await b.Client.PostAsJsonAsync(
            "/v1/identities/link/browser/start",
            new BeginIdentityLink(challenge.ChallengeId, "telegram",
                new Uri("https://client.example.test/link/complete"), Pkce("foreign-verifier-0123456789")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string Pkce(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Query(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name)
                return Uri.UnescapeDataString(parts.Length == 2 ? parts[1] : string.Empty);
        }
        throw new KeyNotFoundException(name);
    }
}