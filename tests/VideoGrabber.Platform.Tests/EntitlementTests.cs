using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class EntitlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Blocked_owner_has_no_access()
    {
        var account = new AccountProfile(Guid.NewGuid(), "owner_admin", true, [], null);
        var access = AccessEvaluator.Evaluate(account, [], Now);
        Assert.False(access.CanDownload);
        Assert.False(access.CanEdit);
        Assert.Equal("account_blocked", access.Reason);
    }

    [Fact]
    public void Owner_is_unlimited_without_credit_balance()
    {
        var account = new AccountProfile(Guid.NewGuid(), "owner_admin", false, [], null);
        var access = AccessEvaluator.Evaluate(account, [], Now);
        Assert.True(access.Unlimited);
        Assert.Equal(0, access.RemainingDownloads);
        Assert.Equal(Now.AddHours(72), access.OfflineUntil);
    }
    [Fact]
    public void Expiry_is_exclusive_and_future_grant_is_inactive()
    {
        var account = new AccountProfile(Guid.NewGuid(), "guest", false, [], null);
        var expired = new Grant(Guid.NewGuid(), "time", "admin_gift", Now.AddDays(-1), Now, 0, 0, false, Now.AddDays(-1));
        var future = new Grant(Guid.NewGuid(), "time", "admin_gift", Now.AddMinutes(1), Now.AddDays(1), 0, 0, false, Now);
        var access = AccessEvaluator.Evaluate(account, [expired, future], Now);
        Assert.False(access.CanDownload);
        Assert.Equal("no_grant", access.Reason);
    }

    [Fact]
    public void Hybrid_is_credits_before_expiry_not_time_access()
    {
        var account = new AccountProfile(Guid.NewGuid(), "guest", false, [], null);
        var hybrid = new Grant(Guid.NewGuid(), "hybrid", "admin_gift", Now, Now.AddDays(7), 3, 0, false, Now);
        var access = AccessEvaluator.Evaluate(account, [hybrid], Now);
        Assert.True(access.CanDownload);
        Assert.False(access.CanEdit);
        Assert.False(access.Unlimited);
        Assert.Equal(3, access.RemainingDownloads);
        Assert.Equal("online_credit_required", access.Reason);
    }

    [Fact]
    public void Active_time_allows_edit_and_bounds_offline_to_24h()
    {
        var account = new AccountProfile(Guid.NewGuid(), "guest", false, [], null);
        var time = new Grant(Guid.NewGuid(), "time", "admin_gift", Now, Now.AddDays(7), 0, 0, false, Now);
        var access = AccessEvaluator.Evaluate(account, [time], Now);
        Assert.True(access.CanEdit);
        Assert.Equal(Now.AddHours(24), access.OfflineUntil);
    }
    [Fact]
    public async Task Gifted_guest_keeps_role_and_has_exact_credits()
    {
        await using var f = await ApiFixture.StartAsync();
        var guest = await f.AccountAsync("telegram", "gifted-guest");
        var admin = await f.AdminAsync();
        var response = await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(guest.Id, "credits", 0, 3, null, "acceptance gift", Guid.NewGuid()));
        response.EnsureSuccessStatusCode();

        var profile = await guest.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        var access = await guest.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal("guest", profile!.Role);
        Assert.Equal(3, access!.RemainingDownloads);
        Assert.False(access.CanEdit);
    }

    [Fact]
    public async Task Gift_idempotency_same_key_same_body_reuses_receipt_changed_body_conflicts()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "gift-idempotency");
        var admin = await f.AdminAsync();
        var key = Guid.NewGuid();
        var first = new GrantRequest(user.Id, "credits", 0, 2, null, "gift", key);
        var a = await admin.PostAsJsonAsync("/v1/admin/grants", first);
        var b = await admin.PostAsJsonAsync("/v1/admin/grants", first);
        a.EnsureSuccessStatusCode(); b.EnsureSuccessStatusCode();
        Assert.Equal(await a.Content.ReadFromJsonAsync<GrantReceipt>(), await b.Content.ReadFromJsonAsync<GrantReceipt>());
        var changed = await admin.PostAsJsonAsync("/v1/admin/grants", first with { Credits = 3 });
        Assert.Equal(System.Net.HttpStatusCode.Conflict, changed.StatusCode);
    }
}

public sealed class EntitlementGiftWindowTests
{
    [Fact]
    public async Task Time_gifts_extend_one_contiguous_window()
    {
        await using var f = await ApiFixture.StartAsync();
        var guest = await f.AccountAsync("telegram", "time-extension");
        var admin = await f.AdminAsync();
        var now = f.Clock.GetUtcNow();
        foreach (var days in new[] { 2, 3 })
        {
            var response = await admin.PostAsJsonAsync("/v1/admin/grants",
                new GrantRequest(guest.Id, "time", days, 0, null, "time gift", Guid.NewGuid()));
            response.EnsureSuccessStatusCode();
        }
        var access = await guest.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.NotNull(access!.ValidUntil);
        Assert.InRange((access.ValidUntil!.Value - now.AddDays(5)).Duration(),
            TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        Assert.True(access.CanEdit);
    }

    [Fact]
    public async Task Permanent_gift_keeps_guest_role_but_grants_unlimited_access()
    {
        await using var f = await ApiFixture.StartAsync();
        var guest = await f.AccountAsync("telegram", "permanent-gift");
        var admin = await f.AdminAsync();
        var response = await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(guest.Id, "permanent", 0, 0, null, "permanent gift", Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        var profile = await guest.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        var access = await guest.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal("guest", profile!.Role);
        Assert.True(access!.Unlimited);
        Assert.True(access.CanEdit);
    }
}

public sealed class EntitlementExpiryTests
{
    [Fact]
    public async Task Hybrid_grant_stops_download_at_expiry()
    {
        await using var f = await ApiFixture.StartAsync();
        var guest = await f.AccountAsync("telegram", "hybrid-expiry");
        var admin = await f.AdminAsync();
        var expiry = f.Clock.GetUtcNow().AddHours(1);
        var response = await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(guest.Id, "hybrid", 0, 2, expiry, "hybrid gift", Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        var before = await f.Service<VideoGrabber.Platform.Persistence.GrantStore>()
            .EvaluateAsync(guest.Id, default);
        Assert.Equal(2, before.RemainingDownloads);
        f.Clock.Advance(TimeSpan.FromHours(1));
        var after = await f.Service<VideoGrabber.Platform.Persistence.GrantStore>()
            .EvaluateAsync(guest.Id, default);
        Assert.False(after.CanDownload);
        Assert.Equal(0, after.RemainingDownloads);
    }
}

public sealed class EntitlementAdminBoundaryTests
{
    [Fact]
    public async Task Owner_without_fresh_mfa_cannot_gift()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AccountAsync("email", "owner-no-mfa");
        var target = await f.AccountAsync("telegram", "owner-no-mfa-target");
        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var command = new Npgsql.NpgsqlCommand(
            "update licensing.accounts set base_role='owner_admin' where account_id=@id", connection))
        {
            command.Parameters.AddWithValue("id", owner.Id);
            await command.ExecuteNonQueryAsync();
        }
        var response = await owner.Client.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(target.Id, "credits", 0, 1, null, "forbidden gift", Guid.NewGuid()));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gift_persists_admin_source_original_amount_and_audit()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "gift-audit");
        var admin = await f.AdminAsync();
        var response = await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 4, null, "audit gift", Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var grant = new Npgsql.NpgsqlCommand(
            "select source,original_amount from licensing.entitlement_grants where account_id=@account", connection);
        grant.Parameters.AddWithValue("account", user.Id);
        await using var reader = await grant.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("admin_gift", reader.GetString(0));
        Assert.Equal(4L, reader.GetInt64(1));
        await reader.DisposeAsync();
        await using var audit = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.audit_events where account_id=@account and event_type='grant_created'", connection);
        audit.Parameters.AddWithValue("account", user.Id);
        Assert.Equal(1L, (long)(await audit.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Concurrent_same_idempotency_key_creates_one_grant()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "gift-idempotency-race");
        var admin = await f.AdminAsync();
        var request = new GrantRequest(user.Id, "credits", 0, 2, null, "race gift", Guid.NewGuid());
        var responses = await Task.WhenAll(
            admin.PostAsJsonAsync("/v1/admin/grants", request),
            admin.PostAsJsonAsync("/v1/admin/grants", request));
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        Assert.Equal(await responses[0].Content.ReadFromJsonAsync<GrantReceipt>(),
            await responses[1].Content.ReadFromJsonAsync<GrantReceipt>());
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var count = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.entitlement_grants where account_id=@account", connection);
        count.Parameters.AddWithValue("account", user.Id);
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }
}
