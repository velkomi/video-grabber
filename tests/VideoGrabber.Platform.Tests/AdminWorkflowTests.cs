using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class AdminWorkflowTests
{
    [Fact]
    public async Task Normal_user_cannot_gift_even_with_forged_admin_fields()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "ordinary-admin-attack");
        user.Client.DefaultRequestHeaders.Add("X-Test-Mfa", "fresh");
        var response = await user.Client.PostAsJsonAsync("/v1/admin/grants",
            new { accountId = user.Id, role = "owner_admin", kind = "credits",
                credits = 100, days = 0, reason = "forged", idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sensitive_admin_mutation_requires_fresh_mfa()
    {
        await using var f = await ApiFixture.StartAsync();
        var target = await f.AccountAsync("telegram", "admin-mfa-target");
        var admin = await f.AdminAsync(freshMfa: false);
        var response = await admin.PostAsJsonAsync($"/v1/admin/accounts/{target.Id}/block",
            new { reason = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact]
    public async Task Admin_can_block_and_unblock_without_losing_base_role()
    {
        await using var f = await ApiFixture.StartAsync();
        var target = await f.AccountAsync("telegram", "admin-block-target");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync($"/v1/admin/accounts/{target.Id}/block",
            new { reason = "support block" })).EnsureSuccessStatusCode();
        var blocked = await target.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.True(blocked!.Blocked);
        Assert.Equal("guest", blocked.Role);
        (await admin.PostAsJsonAsync($"/v1/admin/accounts/{target.Id}/unblock",
            new { reason = "support unblock" })).EnsureSuccessStatusCode();
        var restored = await target.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.False(restored!.Blocked);
        Assert.Equal("guest", restored.Role);
    }

    [Fact]
    public async Task Revoke_gift_removes_only_unused_gift_access()
    {
        await using var f = await ApiFixture.StartAsync();
        var target = await f.AccountAsync("telegram", "admin-revoke-target");
        var admin = await f.AdminAsync();
        var receipt = await (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(target.Id, "credits", 0, 3, null, "gift", Guid.NewGuid())))
            .Content.ReadFromJsonAsync<GrantReceipt>();
        var revoke = await admin.PostAsJsonAsync($"/v1/admin/grants/{receipt!.GrantId}/revoke",
            new { reason = "gift withdrawn" });
        revoke.EnsureSuccessStatusCode();
        var access = await target.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }
    [Fact]
    public async Task Admin_can_reset_all_active_devices_and_read_audit()
    {
        await using var f = await ApiFixture.StartAsync();
        var target = await f.AccountAsync("telegram", "admin-device-target");
        using var key = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var registration = new DeviceRegistration("PC", "windows",
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), Guid.NewGuid());
        (await target.Client.PostAsJsonAsync("/v1/devices", registration)).EnsureSuccessStatusCode();
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync($"/v1/admin/accounts/{target.Id}/devices/reset",
            new { reason = "lost computer" })).EnsureSuccessStatusCode();
        var deviceResponse = await target.Client.GetAsync("/v1/devices");
        var deviceBody = await deviceResponse.Content.ReadAsStringAsync();
        Assert.True(deviceResponse.IsSuccessStatusCode, deviceBody + "\n" + string.Join("\n", f.Logs));
        var devices = await deviceResponse.Content.ReadFromJsonAsync<DeviceReceipt[]>();
        Assert.All(devices!, device => Assert.True(device.Revoked));

        var audit = await admin.GetAsync($"/v1/admin/accounts/{target.Id}/audit");
        audit.EnsureSuccessStatusCode();
        var text = await audit.Content.ReadAsStringAsync();
        Assert.Contains("devices_reset", text, StringComparison.Ordinal);
    }
}
public sealed class AdminSupportAdjustmentTests
{
    [Fact]
    public async Task Blocked_owner_cannot_perform_admin_mutation()
    {
        await using var f = await ApiFixture.StartAsync();
        var target = await f.AccountAsync("telegram", "blocked-admin-target");
        var admin = await f.AdminAsync();
        var adminProfile = await admin.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(adminProfile);
        await using (var c = await f.Database.OpenConnectionAsync())
        await using (var q = new Npgsql.NpgsqlCommand(
            "update licensing.accounts set blocked_at=now() where account_id=@id", c))
        {
            q.Parameters.AddWithValue("id", adminProfile!.AccountId);
            await q.ExecuteNonQueryAsync();
        }
        var response = await admin.PostAsJsonAsync($"/v1/admin/accounts/{target.Id}/block",
            new { reason = "must fail" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task Review_required_adjustment_releases_original_credit_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "support-adjustment-user");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "support", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var reserve = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), "support-adjustment-hash", "download",
                "server_worker", null));
        reserve.EnsureSuccessStatusCode();
        var receipt = await reserve.Content.ReadFromJsonAsync<ReservationReceipt>();
        Assert.NotNull(receipt);
        await MarkReviewRequiredAsync(f, receipt!.ReservationId);

        var adjusted = await admin.PostAsJsonAsync(
            $"/v1/admin/reservations/{receipt.ReservationId}/adjust",
            new { evidenceId = "support-proof-1", reason = "confirmed pre-start failure" });
        adjusted.EnsureSuccessStatusCode();
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(1, access!.RemainingDownloads);
    }
    private static async Task MarkReviewRequiredAsync(ApiFixture f, Guid reservationId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "update licensing.reservations set state='review_required' where reservation_id=@id", c);
        q.Parameters.AddWithValue("id", reservationId);
        await q.ExecuteNonQueryAsync();
    }
}

public sealed class AdminMergeWorkflowTests
{
    [Fact]
    public async Task Active_reservation_blocks_account_merge()
    {
        await using var f = await ApiFixture.StartAsync();
        var source = await f.AccountAsync("google", "admin-merge-active-source");
        var target = await f.AccountAsync("apple", "admin-merge-active-target");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(source.Id, "credits", 0, 2, null, "merge credits", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        (await source.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), "merge-active-hash", "download",
                "server_worker", null))).EnsureSuccessStatusCode();
        var request = await MergeRequestAsync(f, source, target);
        var response = await admin.PostAsJsonAsync("/v1/admin/accounts/merge", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    [Fact]
    public async Task Merge_transfers_only_unused_credit_and_preserves_source_ledger_history()
    {
        await using var f = await ApiFixture.StartAsync();
        var source = await f.AccountAsync("google", "admin-merge-source");
        var target = await f.AccountAsync("apple", "admin-merge-target");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(source.Id, "credits", 0, 3, null, "merge credits", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var sourceLedgerBefore = await LedgerCountAsync(f, source.Id);
        var request = await MergeRequestAsync(f, source, target);
        var response = await admin.PostAsJsonAsync("/v1/admin/accounts/merge", request);
        response.EnsureSuccessStatusCode();

        var access = await target.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
        Assert.Equal(sourceLedgerBefore + 1, await LedgerCountAsync(f, source.Id));
        Assert.True(await LedgerCountAsync(f, target.Id) >= 1);
        Assert.Equal(target.Id, await MergedIntoAsync(f, source.Id));
    }
    private static async Task<MergeRequest> MergeRequestAsync(
        ApiFixture f, TestAccount source, TestAccount target)
    {
        var sourceSubject = await SubjectAsync(f, source.Id, "google");
        var targetSubject = await SubjectAsync(f, target.Id, "apple");
        var sourceProof = f.Broker.Issue(f.Partition("google").Issuer.AbsoluteUri,
            f.Partition("google").Audience, "google", sourceSubject,
            $"merge:{source.Id:D}:{target.Id:D}:source", f.Clock.GetUtcNow().AddMinutes(5));
        var targetProof = f.Broker.Issue(f.Partition("apple").Issuer.AbsoluteUri,
            f.Partition("apple").Audience, "apple", targetSubject,
            $"merge:{source.Id:D}:{target.Id:D}:target", f.Clock.GetUtcNow().AddMinutes(5));
        return new MergeRequest(source.Id, target.Id, "admin merge acceptance", sourceProof, targetProof);
    }

    private static async Task<string> SubjectAsync(ApiFixture f, Guid accountId, string provider)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select provider_subject from licensing.identities where account_id=@a and provider=@p", c);
        q.Parameters.AddWithValue("a", accountId);
        q.Parameters.AddWithValue("p", provider);
        return (string)(await q.ExecuteScalarAsync())!;
    }
    private static async Task<long> LedgerCountAsync(ApiFixture f, Guid accountId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select count(*) from licensing.credit_ledger where account_id=@a", c);
        q.Parameters.AddWithValue("a", accountId);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    private static async Task<Guid?> MergedIntoAsync(ApiFixture f, Guid sourceId)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var q = new Npgsql.NpgsqlCommand(
            "select merged_into from licensing.accounts where account_id=@a", c);
        q.Parameters.AddWithValue("a", sourceId);
        var value = await q.ExecuteScalarAsync();
        return value is Guid id ? id : null;
    }
}
