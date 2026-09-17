using System.Net.Http.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class LedgerConservationTests
{
    [Fact]
    public async Task Successful_finalize_is_idempotent_and_moves_reserved_to_spent()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "finalize-once");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "finalize", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('c', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var reserved = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        var ledger = f.Service<CreditLedger>();
        var finalize = new FinalizeReservation(reserved.ReservationId, Guid.NewGuid(), 1,
            "success", "evidence-success-1");
        var first = await ledger.FinalizeAsync(user.Id, finalize, default);
        var replay = await ledger.FinalizeAsync(user.Id, finalize, default);
        Assert.Equal(first, replay);
        Assert.Equal("completed", first.State);
    }

    [Fact]
    public async Task Release_returns_credit_only_while_original_grant_is_active()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "release-active");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "release", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('d', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var reservation = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        var ledger = f.Service<CreditLedger>();
        Assert.True(await ledger.ReleaseUnstartedAsync(reservation.ReservationId, default));
        Assert.False(await ledger.ReleaseUnstartedAsync(reservation.ReservationId, default));
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(1, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Revoked_grant_release_goes_to_void_not_available()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "release-revoked");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "void", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('e', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var reservation = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        await RevokeOnlyGrantAsync(f, user.Id);
        Assert.True(await f.Service<CreditLedger>().ReleaseUnstartedAsync(reservation.ReservationId, default));
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
        Assert.Equal(1L, await EventCountAsync(f, reservation.ReservationId, "release_void"));
    }

    [Fact]
    public async Task Owner_reservation_uses_no_credit_and_creates_no_debit_event()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AdminAsync();
        var profile = await owner.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.NotNull(profile);
        var response = await owner.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('f', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        Assert.False(receipt.UsesCredit);
        Assert.Equal(0L, await EventCountAsync(f, receipt.ReservationId, "reserve"));
    }

    private static async Task RevokeOnlyGrantAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "update licensing.entitlement_grants set revoked_at=now() where account_id=@account", connection);
        command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> EventCountAsync(ApiFixture f, Guid reservationId, string kind)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.credit_ledger where reservation_id=@reservation and event_kind=@kind", connection);
        command.Parameters.AddWithValue("reservation", reservationId);
        command.Parameters.AddWithValue("kind", kind);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

public sealed class LedgerAdditionalConservationTests
{
    [Fact]
    public async Task Expired_grant_release_goes_to_void()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "release-expired");
        var admin = await f.AdminAsync();
        var expiry = f.Clock.GetUtcNow().AddMinutes(5);
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "hybrid", 0, 1, expiry, "expiry", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('1', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        f.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await f.Service<CreditLedger>().ReleaseUnstartedAsync(receipt.ReservationId, default));
        var access = await f.Service<GrantStore>().EvaluateAsync(user.Id, default);
        Assert.Equal(0, access.RemainingDownloads);
        Assert.Equal(1L, await CountEventAsync(f, receipt.ReservationId, "release_void"));
    }

    [Fact]
    public async Task Active_time_access_does_not_spend_available_credit()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "time-no-credit");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 2, null, "credits", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "time", 1, 0, null, "time", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('2', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        Assert.False(receipt.UsesCredit);
        var access = await f.Service<GrantStore>().EvaluateAsync(user.Id, default);
        Assert.Equal(2, access.RemainingDownloads);
        Assert.Equal(0L, await CountEventAsync(f, receipt.ReservationId, "reserve"));
    }

    [Fact]
    public async Task Issue_reserve_commit_deltas_conserve_one_credit()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "ledger-conserve");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "conserve", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var response = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('3', 64), "download", "server_worker", null));
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        await f.Service<CreditLedger>().FinalizeAsync(user.Id,
            new FinalizeReservation(receipt.ReservationId, Guid.NewGuid(), 1, "success", "ledger-proof"), default);
        var totals = await LedgerTotalsAsync(f, user.Id);
        Assert.Equal((0L, 0L, 1L, 0L), totals);
    }
    private static async Task<long> CountEventAsync(
        ApiFixture f,
        Guid reservationId,
        string kind)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.credit_ledger where reservation_id=@reservation and event_kind=@kind",
            connection);
        command.Parameters.AddWithValue("reservation", reservationId);
        command.Parameters.AddWithValue("kind", kind);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<(long Available, long Reserved, long Spent, long Void)> LedgerTotalsAsync(
        ApiFixture f,
        Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "select coalesce(sum(available_delta),0),coalesce(sum(reserved_delta),0),coalesce(sum(spent_delta),0),coalesce(sum(void_delta),0) from licensing.credit_ledger where account_id=@account",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }
}
public sealed class LedgerPolicyTests
{
    [Fact]
    public async Task Review_required_keeps_reserved_credit_held()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "review-required");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "credits", 0, 1, null, "review", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        var reserve = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('4', 64), "download", "server_worker", null));
        reserve.EnsureSuccessStatusCode();
        var receipt = (await reserve.Content.ReadFromJsonAsync<ReservationReceipt>())!;
        var finalized = await f.Service<CreditLedger>().FinalizeAsync(user.Id,
            new FinalizeReservation(receipt.ReservationId, Guid.NewGuid(), 1,
                "review_required", "ambiguous-desktop-failure"), default);
        Assert.Equal("review_required", finalized.State);
        var access = await f.Service<GrantStore>().EvaluateAsync(user.Id, default);
        Assert.Equal(0, access.RemainingDownloads);
        Assert.Equal((0L, 1L, 0L, 0L), await GrantBucketsAsync(f, user.Id));
    }

    [Fact]
    public async Task Reservation_uses_earliest_expiring_credit_grant()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "earliest-expiry");
        var admin = await f.AdminAsync();
        var later = f.Clock.GetUtcNow().AddDays(10);
        var sooner = f.Clock.GetUtcNow().AddDays(2);
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "hybrid", 0, 1, later, "later", Guid.NewGuid()))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(user.Id, "hybrid", 0, 1, sooner, "sooner", Guid.NewGuid()))).EnsureSuccessStatusCode();
        var reserve = await user.Client.PostAsJsonAsync("/v1/reservations",
            new ReservationRequest(Guid.NewGuid(), new string('5', 64), "download", "server_worker", null));
        reserve.EnsureSuccessStatusCode();
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            select g.valid_until from licensing.reservations r
            join licensing.entitlement_grants g on g.grant_id=r.grant_id
            where r.account_id=@account
            """, connection);
        command.Parameters.AddWithValue("account", user.Id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var selected = reader.GetFieldValue<DateTimeOffset>(0);
        Assert.InRange((selected - sooner).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    private static async Task<(long Available, long Reserved, long Spent, long Void)> GrantBucketsAsync(
        ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            select coalesce(sum(available),0),coalesce(sum(reserved),0),
                   coalesce(sum(original_amount-available-reserved),0),0::bigint
            from licensing.entitlement_grants where account_id=@account
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }
}