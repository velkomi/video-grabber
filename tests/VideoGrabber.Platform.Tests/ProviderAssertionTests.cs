using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Api.Auth;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ProviderAssertionTests
{
    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    [InlineData("yandex")]
    [InlineData("telegram")]
    [InlineData("email")]
    public async Task Provider_flow_starts_for_each_isolated_partition(string provider)
    {
        await using var f = await ApiFixture.StartAsync();
        var request = new BeginSignIn(
            provider,
            new Uri("https://client.example.test/auth/complete"),
            "client-challenge");

        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/start", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        Assert.True(started!.ExpiresAt > f.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Complete_route_rejects_unknown_broker_code_after_valid_state_and_pkce()
    {
        await using var f = await ApiFixture.StartAsync();
        const string verifier = "provider-test-verifier-0123456789";
        var begin = new BeginSignIn("google",
            new Uri("https://client.example.test/auth/complete"), Pkce(verifier));
        var startedResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/start", begin);
        var started = await startedResponse.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var state = QueryValue(started!.AuthorizationUri, "state");

        var completed = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(started.FlowId, "unknown-code", state, verifier));

        Assert.Equal(HttpStatusCode.Unauthorized, completed.StatusCode);
    }

    [Theory]
    [InlineData("bad-state")]
    [InlineData("bad-pkce")]
    public async Task Invalid_state_or_pkce_is_rejected(string mode)
    {
        await using var f = await ApiFixture.StartAsync();
        const string verifier = "provider-test-verifier-0123456789";
        var startedResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        var started = await startedResponse.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var state = QueryValue(started!.AuthorizationUri, "state");
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete", new CompleteSignIn(
            started.FlowId, "unknown-code", mode == "bad-state" ? state + "x" : state,
            mode == "bad-pkce" ? verifier + "x" : verifier));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_flow_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        const string verifier = "provider-test-verifier-0123456789";
        var startedResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        var started = await startedResponse.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var state = QueryValue(started!.AuthorizationUri, "state");
        f.Clock.Advance(TimeSpan.FromMinutes(6));
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(started.FlowId, "unknown-code", state, verifier));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Consumed_flow_cannot_be_replayed()
    {
        await using var f = await ApiFixture.StartAsync();
        const string verifier = "provider-test-verifier-0123456789";
        var startedResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        var started = await startedResponse.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var request = new CompleteSignIn(started!.FlowId, "unknown-code",
            QueryValue(started.AuthorizationUri, "state"), verifier);
        var first = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete", request);
        var second = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete", request);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    [InlineData("yandex")]
    [InlineData("telegram")]
    [InlineData("email")]
    public async Task Repeated_verified_identity_has_one_account(string provider)
    {
        await using var f = await ApiFixture.StartAsync();
        var first = await CompleteAsync(f, provider, "stable-subject");
        var second = await CompleteAsync(f, provider, "stable-subject");
        using var firstClient = f.SessionClient(first);
        using var secondClient = f.SessionClient(second);
        var a = await firstClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        var b = await secondClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.AccountId, b!.AccountId);
    }

    private static async Task<(SignInStart Start,string State,string Nonce,string Verifier)> BeginRawAsync(ApiFixture f, string provider)
    {
        var verifier = "raw-flow-verifier-0123456789";
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn(provider, new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        var start = await response.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(start);
        return (start!, QueryValue(start!.AuthorizationUri,"state"), QueryValue(start.AuthorizationUri,"nonce"), verifier);
    }

    private static async Task<long> CountAccountsAsync(ApiFixture f)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select count(*) from licensing.accounts", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<ApiSession> CompleteAsync(ApiFixture f, string provider, string subject, string? email = null)
    {
        const string verifier = "provider-roundtrip-verifier-0123456789";
        var begin = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn(provider, new Uri("https://client.example.test/auth/complete"), Pkce(verifier)));
        var started = await begin.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var state = QueryValue(started!.AuthorizationUri, "state");
        var nonce = QueryValue(started.AuthorizationUri, "nonce");
        var code = f.Broker.RegisterCode(f.Partition(provider), subject, nonce, f.Clock.GetUtcNow().AddMinutes(5), email);
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(started.FlowId, code, state, verifier));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiSession>())!;
    }

    [Fact]
    public async Task Broker_redirect_uses_registered_server_callback()
    {
        await using var f = await ApiFixture.StartAsync();
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("https://client.example.test/after-login"), Pkce("redirect-verifier-0123456789")));
        var started = await response.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        Assert.Equal(f.Partition("google").CallbackUri.AbsoluteUri, QueryValue(started!.AuthorizationUri, "redirect_uri"));
    }

    [Fact]
    public async Task Same_verified_email_across_partitions_never_merges_accounts()
    {
        await using var f = await ApiFixture.StartAsync();
        var google = await CompleteAsync(f, "google", "subject-a", "same@example.test");
        var apple = await CompleteAsync(f, "apple", "subject-b", "same@example.test");
        using var ga = f.SessionClient(google); using var ap = f.SessionClient(apple);
        var a = await ga.GetFromJsonAsync<AccountProfile>("/v1/me");
        var b = await ap.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotEqual(a!.AccountId, b!.AccountId);
    }

    [Fact]
    public async Task Frozen_broker_mapping_rejects_subject_swap_without_new_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var first = await BeginRawAsync(f, "google");
        var firstCode = f.Broker.RegisterCodeCustom(f.Partition("google"), "broker-fixed",
            "subject-1", first.Nonce, f.Clock.GetUtcNow().AddMinutes(5));
        var firstResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(first.Start.FlowId, firstCode, first.State, first.Verifier));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var before = await CountAccountsAsync(f);

        var second = await BeginRawAsync(f, "google");
        var changedCode = f.Broker.RegisterCodeCustom(f.Partition("google"), "broker-fixed",
            "subject-2", second.Nonce, f.Clock.GetUtcNow().AddMinutes(5));
        var changed = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(second.Start.FlowId, changedCode, second.State, second.Verifier));
        Assert.Equal(HttpStatusCode.Unauthorized, changed.StatusCode);
        Assert.Equal(before, await CountAccountsAsync(f));
    }

    [Fact]
    public async Task Duplicate_broker_identities_create_no_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var flow = await BeginRawAsync(f, "apple");
        var code = f.Broker.RegisterCodeCustom(f.Partition("apple"), "broker-dup",
            "subject-1", flow.Nonce, f.Clock.GetUtcNow().AddMinutes(5), identityCount: 2);
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete",
            new CompleteSignIn(flow.Start.FlowId, code, flow.State, flow.Verifier));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountAccountsAsync(f));
    }

    [Fact]
    public async Task Auth_secrets_are_not_written_to_request_logs()
    {
        await using var f = await ApiFixture.StartAsync();
        const string codeSecret = "SENTINEL_OAUTH_CODE_7f31";
        const string cookieSecret = "SENTINEL_COOKIE_91ac";
        const string bearerSecret = "SENTINEL_BEARER_42de";
        const string signedUrlSecret = "SENTINEL_SIGNED_URL_ae10";
        f.Anonymous.DefaultRequestHeaders.Add("Cookie", "vg=" + cookieSecret);
        f.Anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerSecret);
        const string verifier = "logging-verifier-0123456789";
        var begin = await f.Anonymous.PostAsJsonAsync("/v1/auth/start", new BeginSignIn("google",
            new Uri("https://client.example.test/callback?sig=" + signedUrlSecret), Pkce(verifier)));
        var started = await begin.Content.ReadFromJsonAsync<SignInStart>();
        Assert.NotNull(started);
        var response = await f.Anonymous.PostAsJsonAsync("/v1/auth/complete", new CompleteSignIn(
            started!.FlowId, codeSecret, QueryValue(started.AuthorizationUri,"state"), verifier));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var logs = string.Join("\n", f.Logs);
        Assert.DoesNotContain(codeSecret, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(cookieSecret, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(bearerSecret, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(signedUrlSecret, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_rotates_secret_and_replay_revokes_family()
    {
        await using var f = await ApiFixture.StartAsync();
        var original = await CompleteAsync(f, "google", "refresh-subject");
        var rotatedResponse = await f.Anonymous.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<ApiSession>();
        Assert.NotNull(rotated);
        Assert.NotEqual(original.RefreshToken, rotated!.RefreshToken);

        var replay = await f.Anonymous.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var familyRevoked = await f.Anonymous.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_family()
    {
        await using var f = await ApiFixture.StartAsync();
        var session = await CompleteAsync(f, "apple", "logout-subject");
        var logout = await f.Anonymous.PostAsJsonAsync("/v1/auth/logout",
            new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var refresh = await f.Anonymous.PostAsJsonAsync("/v1/auth/refresh",
            new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Desktop_loopback_return_is_allowed_but_external_http_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        const string verifier = "desktop-loopback-verifier-0123456789";
        var loopback = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("http://127.0.0.1:54321/videograbber-auth/callback"), Pkce(verifier)));
        Assert.Equal(HttpStatusCode.OK, loopback.StatusCode);

        var external = await f.Anonymous.PostAsJsonAsync("/v1/auth/start",
            new BeginSignIn("google", new Uri("http://client.example.test/videograbber-auth/callback"), Pkce(verifier)));
        Assert.Equal(HttpStatusCode.BadRequest, external.StatusCode);
    }
    [Theory]
    [InlineData("https://client.example.test/auth/complete", true)]
    [InlineData("http://127.0.0.1:54321/videograbber-auth/callback", true)]
    [InlineData("http://localhost:54321/videograbber-auth/callback", false)]
    [InlineData("http://127.0.0.1:54321/other", false)]
    [InlineData("http://client.example.test/videograbber-auth/callback", false)]
    public void Desktop_return_uri_policy_allows_only_https_or_exact_ipv4_loopback(string uri, bool allowed)
    {
        var method = typeof(ProviderFlow).GetMethod("IsAllowedClientReturnUri",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.Equal(allowed, (bool)method!.Invoke(null, [new Uri(uri)])!);
    }
    private static string Pkce(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string QueryValue(Uri uri, string name)
    {
        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]) == name)
                return Uri.UnescapeDataString(pair[1]);
        }
        throw new InvalidDataException($"Missing query value {name}.");
    }
}
