using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class IdentityLinkTests
{
    [Fact]
    public async Task Link_challenge_cannot_be_consumed_by_another_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("telegram", "1001");
        var b = await f.AccountAsync("telegram", "1002");
        var begun = await a.Client.PostAsJsonAsync("/v1/identities/link", new { });
        var challenge = await begun.Content.ReadFromJsonAsync<LinkChallenge>();
        Assert.NotNull(challenge);

        var response = await b.Client.PostAsJsonAsync("/v1/identities/link/complete",
            new LinkProof(challenge!.ChallengeId, "synthetic-invalid-proof"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class IdentityLinkBehaviorTests
{
    [Fact]
    public async Task Fresh_assertion_links_provider_to_same_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "link-owner");
        var challenge = await BeginAsync(account.Client);
        var token = f.Broker.Issue(
            f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience,
            "apple",
            "linked-subject",
            challenge.ChallengeId.ToString("D"),
            f.Clock.GetUtcNow().AddMinutes(5),
            "linked@example.test");

        var response = await account.Client.PostAsJsonAsync(
            "/v1/identities/link/complete", new LinkProof(challenge.ChallengeId, token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var profile = await account.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.Contains("google", profile!.LinkedProviders);
        Assert.Contains("apple", profile.LinkedProviders);
    }

    [Fact]
    public async Task Expired_or_consumed_challenge_returns_conflict()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "expiry-owner");
        var expired = await BeginAsync(account.Client);
        await ExpireChallengeAsync(f, expired.ChallengeId);
        var expiredResponse = await account.Client.PostAsJsonAsync(
            "/v1/identities/link/complete", new LinkProof(expired.ChallengeId, "invalid"));
        Assert.Equal(HttpStatusCode.Conflict, expiredResponse.StatusCode);

        var replay = await BeginAsync(account.Client);
        var token = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", "replay-subject",
            replay.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5));
        var proof = new LinkProof(replay.ChallengeId, token);
        var first = await account.Client.PostAsJsonAsync("/v1/identities/link/complete", proof);
        var second = await account.Client.PostAsJsonAsync("/v1/identities/link/complete", proof);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Identity_owned_by_another_account_returns_conflict()
    {
        await using var f = await ApiFixture.StartAsync();
        var a = await f.AccountAsync("google", "collision-a");
        var b = await f.AccountAsync("apple", "collision-b");
        await MarkPurchasedAsync(f, b.Id);
        var challenge = await BeginAsync(a.Client);
        var token = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", "collision-b",
            challenge.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5));

        var response = await a.Client.PostAsJsonAsync("/v1/identities/link/complete",
            new LinkProof(challenge.ChallengeId, token));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Last_identity_cannot_be_unlinked()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "last-provider");
        var identityId = await IdentityIdAsync(f, account.Id, "google");
        var response = await account.Client.DeleteAsync($"/v1/identities/{identityId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_unlinks_preserve_one_identity()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "concurrent-owner");
        await LinkAsync(f, account, "apple", "concurrent-apple");
        var google = await IdentityIdAsync(f, account.Id, "google");
        var apple = await IdentityIdAsync(f, account.Id, "apple");

        var responses = await Task.WhenAll(
            account.Client.DeleteAsync($"/v1/identities/{google}"),
            account.Client.DeleteAsync($"/v1/identities/{apple}"));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1L, await IdentityCountAsync(f, account.Id));
    }

    private static async Task<LinkChallenge> BeginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/v1/identities/link", new { });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkChallenge>())!;
    }

    private static async Task LinkAsync(
        ApiFixture f,
        TestAccount account,
        string provider,
        string subject)
    {
        var challenge = await BeginAsync(account.Client);
        var token = f.Broker.Issue(f.Partition(provider).Issuer.AbsoluteUri,
            f.Partition(provider).Audience, provider, subject,
            challenge.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5));
        var response = await account.Client.PostAsJsonAsync("/v1/identities/link/complete",
            new LinkProof(challenge.ChallengeId, token));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<Guid> IdentityIdAsync(
        ApiFixture f,
        Guid accountId,
        string provider)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "select identity_id from licensing.identities where account_id=@account and provider=@provider",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("provider", provider);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> IdentityCountAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.identities where account_id=@account", connection);
        command.Parameters.AddWithValue("account", accountId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExpireChallengeAsync(ApiFixture f, Guid challengeId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "update licensing.account_links set expires_at=now()-interval '1 minute' where challenge_id=@id",
            connection);
        command.Parameters.AddWithValue("id", challengeId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ProviderSubjectAsync(ApiFixture f, Guid accountId, string provider)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "select provider_subject from licensing.identities where account_id=@account and provider=@provider",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("provider", provider);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task MarkPurchasedAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "update licensing.accounts set first_purchase_at=now() where account_id=@account",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class IdentityFreshnessTests
{
    [Fact]
    public async Task Stale_session_cannot_begin_link()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "stale-session");
        f.Clock.Advance(TimeSpan.FromMinutes(6));

        var response = await account.Client.PostAsJsonAsync("/v1/identities/link", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class IdentityLinkRaceTests
{
    [Fact]
    public async Task Simultaneous_challenge_consumes_succeed_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "challenge-race");
        var begun = await account.Client.PostAsJsonAsync("/v1/identities/link", new { });
        var challenge = await begun.Content.ReadFromJsonAsync<LinkChallenge>();
        Assert.NotNull(challenge);
        var token = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", "race-apple",
            challenge!.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5));
        var proof = new LinkProof(challenge.ChallengeId, token);

        var responses = await Task.WhenAll(
            account.Client.PostAsJsonAsync("/v1/identities/link/complete", proof),
            account.Client.PostAsJsonAsync("/v1/identities/link/complete", proof));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }
}

public sealed class IdentityMergeTests
{
    [Fact]
    public async Task Purchased_account_merge_requires_reconciliation()
    {
        await using var f = await ApiFixture.StartAsync();
        var source = await f.AccountAsync("google", "merge-paid-source");
        var target = await f.AccountAsync("apple", "merge-paid-target");
        var adminClient = await f.AdminAsync();
        var admin = await adminClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(admin);
        await MarkPurchasedAsync(f, source.Id);
        var request = await MergeRequestForAsync(f, source, target);

        await Assert.ThrowsAsync<FinancialMergeRequiresReconciliationException>(() =>
            f.Service<IdentityLinkService>().MergeAsync(admin!.AccountId, request, default));
    }

    [Fact]
    public async Task Unpaid_accounts_merge_identities_revoke_source_sessions_and_audit()
    {
        await using var f = await ApiFixture.StartAsync();
        var source = await f.AccountAsync("google", "merge-source");
        var target = await f.AccountAsync("apple", "merge-target");
        var adminClient = await f.AdminAsync();
        var admin = await adminClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(admin);

        var request = await MergeRequestForAsync(f, source, target);
        await f.Service<IdentityLinkService>().MergeAsync(admin!.AccountId, request, default);

        var profile = await target.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(profile);
        Assert.Contains("google", profile!.LinkedProviders);
        Assert.Contains("apple", profile.LinkedProviders);

        await using var connection = await f.Database.OpenConnectionAsync();
        await using var merged = new Npgsql.NpgsqlCommand(
            "select merged_into from licensing.accounts where account_id=@source", connection);
        merged.Parameters.AddWithValue("source", source.Id);
        Assert.Equal(target.Id, (Guid)(await merged.ExecuteScalarAsync())!);

        await using var sessions = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.api_sessions where account_id=@source and revoked_at is not null",
            connection);
        sessions.Parameters.AddWithValue("source", source.Id);
        Assert.True((long)(await sessions.ExecuteScalarAsync())! > 0);

        await using var audit = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.audit_events where event_type='account_merged' and account_id=@target",
            connection);
        audit.Parameters.AddWithValue("target", target.Id);
        Assert.Equal(1L, (long)(await audit.ExecuteScalarAsync())!);
    }

    private static async Task<MergeRequest> MergeRequestForAsync(
        ApiFixture f,
        TestAccount source,
        TestAccount target)
    {
        var sourceSubject = await MergeProviderSubjectAsync(f, source.Id, "google");
        var targetSubject = await MergeProviderSubjectAsync(f, target.Id, "apple");
        var sourceProof = f.Broker.Issue(f.Partition("google").Issuer.AbsoluteUri,
            f.Partition("google").Audience, "google", sourceSubject,
            $"merge:{source.Id:D}:{target.Id:D}:source", f.Clock.GetUtcNow().AddMinutes(5));
        var targetProof = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", targetSubject,
            $"merge:{source.Id:D}:{target.Id:D}:target", f.Clock.GetUtcNow().AddMinutes(5));
        return new MergeRequest(source.Id, target.Id, "test merge", sourceProof, targetProof);
    }

    private static async Task<string> MergeProviderSubjectAsync(ApiFixture f, Guid accountId, string provider)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand("select provider_subject from licensing.identities where account_id=@a and provider=@p", c);
        q.Parameters.AddWithValue("a", accountId); q.Parameters.AddWithValue("p", provider);
        return (string)(await q.ExecuteScalarAsync())!;
    }

    private static async Task MarkPurchasedAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "update licensing.accounts set first_purchase_at=now() where account_id=@account",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class IdentityRecoveryTests
{
    [Fact]
    public async Task Recovery_requires_admin_mfa_and_records_review()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "recovery-owner");
        using var adminClient = await f.AdminAsync();
        var admin = await adminClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(admin);
        var service = f.Service<IdentityLinkService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BeginRecoveryAsync(
            admin!.AccountId, account.Id, "support_ticket", "verified ownership", false, default));

        var challenge = await service.BeginRecoveryAsync(admin.AccountId, account.Id,
            "support_ticket", "verified ownership", true, default);
        Assert.True(challenge.ExpiresAt > f.Clock.GetUtcNow());
        Assert.Equal(1L, await AuditCountAsync(f, account.Id, "recovery_reviewed"));
        Assert.True(await RevokedSessionCountAsync(f, account.Id) > 0);
    }

    [Fact]
    public async Task Same_email_without_linked_email_identity_does_not_recover()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "email-similarity-owner", "same@example.test");
        var challenge = await AdminRecoveryAsync(f, account.Id);
        var token = f.Broker.Issue(f.Partition("email").Issuer.AbsoluteUri,
            f.Partition("email").Audience, "email", "different-email-identity",
            challenge.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5), "same@example.test");
        var response = await f.Anonymous.PostAsJsonAsync("/v1/recovery/complete",
            new LinkProof(challenge.ChallengeId, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await IdentityCountByProviderAsync(f, account.Id, "email"));
    }

    [Fact]
    public async Task Already_linked_verified_email_can_complete_recovery_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "linked-email-subject", "linked@example.test");
        var challenge = await AdminRecoveryAsync(f, account.Id);
        var token = f.Broker.Issue(f.Partition("email").Issuer.AbsoluteUri,
            f.Partition("email").Audience, "email", "linked-email-subject",
            challenge.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5), "linked@example.test");
        var proof = new LinkProof(challenge.ChallengeId, token);

        var first = await f.Anonymous.PostAsJsonAsync("/v1/recovery/complete", proof);
        var replay = await f.Anonymous.PostAsJsonAsync("/v1/recovery/complete", proof);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(1L, await AuditCountAsync(f, account.Id, "recovery_completed"));
    }

    [Fact]
    public async Task Expired_recovery_challenge_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("google", "expired-recovery-owner");
        var challenge = await AdminRecoveryAsync(f, account.Id);
        await ExpireRecoveryAsync(f, challenge.ChallengeId);
        var token = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", "new-apple-recovery",
            challenge.ChallengeId.ToString("D"), f.Clock.GetUtcNow().AddMinutes(5));
        var response = await f.Anonymous.PostAsJsonAsync("/v1/recovery/complete",
            new LinkProof(challenge.ChallengeId, token));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await IdentityCountByProviderAsync(f, account.Id, "apple"));
    }

    private static async Task<LinkChallenge> AdminRecoveryAsync(ApiFixture f, Guid accountId)
    {
        using var adminClient = await f.AdminAsync();
        var admin = await adminClient.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(admin);
        return await f.Service<IdentityLinkService>().BeginRecoveryAsync(
            admin!.AccountId, accountId, "manual_review", "ownership confirmed", true, default);
    }

    private static async Task<long> AuditCountAsync(ApiFixture f, Guid accountId, string eventType)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.audit_events where account_id=@a and event_type=@e", c);
        q.Parameters.AddWithValue("a", accountId);
        q.Parameters.AddWithValue("e", eventType);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    private static async Task<long> RevokedSessionCountAsync(ApiFixture f, Guid accountId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.api_sessions where account_id=@a and revoked_at is not null", c);
        q.Parameters.AddWithValue("a", accountId);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    private static async Task<long> IdentityCountByProviderAsync(ApiFixture f, Guid accountId, string provider)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.identities where account_id=@a and provider=@p", c);
        q.Parameters.AddWithValue("a", accountId);
        q.Parameters.AddWithValue("p", provider);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    private static async Task ExpireRecoveryAsync(ApiFixture f, Guid challengeId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "update licensing.account_links set expires_at=now()-interval '1 minute' where challenge_id=@id", c);
        q.Parameters.AddWithValue("id", challengeId);
        await q.ExecuteNonQueryAsync();
    }
}
