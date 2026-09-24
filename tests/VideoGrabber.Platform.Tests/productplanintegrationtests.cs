using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class ProductPlanIntegrationTests
{
    [Fact]
    public async Task Free_same_logical_intent_is_reserved_once_and_release_restores_allowance()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "email", "free-idempotent", includeStarter: true);
        var ledger = f.Service<CreditLedger>();

        var intent = Guid.NewGuid();
        var request = new ReservationRequest(
            intent, "free-logical-item", "download", "server_worker", null);

        var first = await ledger.ReserveAsync(account.Id, request, CancellationToken.None);
        var second = await ledger.ReserveAsync(account.Id, request, CancellationToken.None);

        Assert.Equal(first.ReservationId, second.ReservationId);
        Assert.True(first.UsesCredit);

        var state = await ReadFreeStateAsync(f, account.Id);
        Assert.Equal((9L, 1L), state);

        Assert.True(await ledger.ReleaseUnstartedAsync(
            first.ReservationId, CancellationToken.None));
        Assert.Equal((10L, 0L), await ReadFreeStateAsync(f, account.Id));
    }

    [Fact]
    public async Task Free_allows_ten_basic_downloads_but_rejects_premium_media()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "free-premium-gate", includeStarter: true);
        var ledger = f.Service<CreditLedger>();

        await Assert.ThrowsAsync<ReservationUnavailableException>(() =>
            ledger.ReserveAsync(
                account.Id,
                new ReservationRequest(
                    Guid.NewGuid(), "free-premium", "premium_media", "server_worker", null),
                CancellationToken.None));

        Assert.Equal((10L, 0L), await ReadFreeStateAsync(f, account.Id));

        var basic = await ledger.ReserveAsync(
            account.Id,
            new ReservationRequest(
                Guid.NewGuid(), "free-basic", "download", "server_worker", null),
            CancellationToken.None);
        Assert.True(basic.UsesCredit);
        Assert.Equal((9L, 1L), await ReadFreeStateAsync(f, account.Id));
    }

    [Fact]
    public async Task Start_plan_allows_premium_media_and_counts_it_in_daily_quota()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "start-premium-media");
        await AddTimedPlanAsync(f, account.Id, "start");
        var ledger = f.Service<CreditLedger>();

        var receipt = await ledger.ReserveAsync(
            account.Id,
            new ReservationRequest(
                Guid.NewGuid(), "start-premium", "premium_media", "server_worker", null),
            CancellationToken.None);

        Assert.False(receipt.UsesCredit);
        var date = DateOnly.FromDateTime(f.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal((1L, 0L), await ReadStartUsageAsync(f, account.Id, date));
    }

    [Fact]
    public async Task Start_concurrent_reservations_cannot_exceed_ten_per_utc_day()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "start-concurrency");
        await AddTimedPlanAsync(f, account.Id, "start");

        var ledger = f.Service<CreditLedger>();
        var tasks = Enumerable.Range(0, 20)
            .Select(index => TryReserveAsync(
                ledger,
                account.Id,
                new ReservationRequest(
                    Guid.NewGuid(),
                    "start-" + index,
                    "download",
                    "server_worker",
                    null)))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(10, results.Count(x => x is not null));
        Assert.Equal(10, results.Count(x => x is null));

        var usage = await ReadStartUsageAsync(
            f, account.Id, DateOnly.FromDateTime(f.Clock.GetUtcNow().UtcDateTime));
        Assert.Equal((10L, 0L), usage);
    }

    [Fact]
    public async Task Start_quota_resets_on_next_server_utc_date()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "start-reset");
        await AddTimedPlanAsync(f, account.Id, "start");
        var ledger = f.Service<CreditLedger>();

        var dayOne = DateOnly.FromDateTime(f.Clock.GetUtcNow().UtcDateTime);
        for (var i = 0; i < 10; i++)
            Assert.NotNull(await TryReserveAsync(
                ledger,
                account.Id,
                new ReservationRequest(
                    Guid.NewGuid(), "day1-" + i, "download", "server_worker", null)));

        Assert.Null(await TryReserveAsync(
            ledger,
            account.Id,
            new ReservationRequest(
                Guid.NewGuid(), "day1-over", "download", "server_worker", null)));

        f.Clock.Advance(TimeSpan.FromDays(1));
        var dayTwo = DateOnly.FromDateTime(f.Clock.GetUtcNow().UtcDateTime);
        Assert.NotEqual(dayOne, dayTwo);

        for (var i = 0; i < 10; i++)
            Assert.NotNull(await TryReserveAsync(
                ledger,
                account.Id,
                new ReservationRequest(
                    Guid.NewGuid(), "day2-" + i, "download", "server_worker", null)));

        Assert.Equal((10L, 0L), await ReadStartUsageAsync(f, account.Id, dayOne));
        Assert.Equal((10L, 0L), await ReadStartUsageAsync(f, account.Id, dayTwo));
    }

    [Fact]
    public async Task Unlimited_video_allows_individual_downloads_but_rejects_course_job()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("email", "unlimited-video");
        await AddTimedPlanAsync(f, account.Id, "unlimited_video");

        var ledger = f.Service<CreditLedger>();
        for (var i = 0; i < 15; i++)
        {
            var reservation = await ledger.ReserveAsync(
                account.Id,
                new ReservationRequest(
                    Guid.NewGuid(), "unlimited-" + i, "download", "server_worker", null),
                CancellationToken.None);
            Assert.False(reservation.UsesCredit);
        }

        var source = await f.SourceAsync(account.Id, "best");
        var course = DesktopJob("course_download", source, "best");
        await Assert.ThrowsAsync<ReservationUnavailableException>(
            () => f.Service<JobStore>().CreateAsync(
                account.Id, course, CancellationToken.None));
    }

    [Fact]
    public async Task Full_course_allows_course_job_without_credit_depletion()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "email", "full-course", includeStarter: true);
        await AddTimedPlanAsync(f, account.Id, "full_course");

        var source = await f.SourceAsync(account.Id, "best");
        var course = DesktopJob("course_download", source, "best");
        var created = await f.Service<JobStore>().CreateAsync(
            account.Id, course, CancellationToken.None);

        Assert.Equal("waiting_for_worker", created.State);
        Assert.Equal("course_download", created.Kind);

        var access = await f.Service<GrantStore>().EvaluateAsync(
            account.Id, CancellationToken.None);
        Assert.True(access.Unlimited);
        Assert.True(access.CanDownloadCourse);
        Assert.Equal("full_course", access.PlanId);

        Assert.Equal((10L, 0L), await ReadFreeStateAsync(f, account.Id));
    }

    [Fact]
    public async Task Owner_admin_allows_course_without_depleting_free_allowance()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "email", "owner-course", includeStarter: true);
        await f.PromoteAdminAsync(account.Id);

        var source = await f.SourceAsync(account.Id, "best");
        var course = DesktopJob("course_download", source, "best");
        var created = await f.Service<JobStore>().CreateAsync(
            account.Id, course, CancellationToken.None);

        Assert.Equal("waiting_for_worker", created.State);
        var access = await f.Service<GrantStore>().EvaluateAsync(
            account.Id, CancellationToken.None);
        Assert.True(access.Unlimited);
        Assert.True(access.CanDownloadCourse);
        Assert.Equal((10L, 0L), await ReadFreeStateAsync(f, account.Id));
    }

    private static async Task AddTimedPlanAsync(
        ApiFixture f,
        Guid accountId,
        string planId)
    {
        var now = f.Clock.GetUtcNow();
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,plan_id,
              valid_from,valid_until,available,reserved,original_amount,
              reason,created_at)
            values(@grant,@account,'time','adjustment',@plan,
              @from,@until,0,0,0,@reason,@from)
            """, connection);
        command.Parameters.AddWithValue("grant", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("plan", planId);
        command.Parameters.AddWithValue("from", now);
        command.Parameters.AddWithValue("until", now.AddDays(30));
        command.Parameters.AddWithValue("reason", "test plan " + planId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ReservationReceipt?> TryReserveAsync(
        CreditLedger ledger,
        Guid accountId,
        ReservationRequest request)
    {
        try
        {
            return await ledger.ReserveAsync(
                accountId, request, CancellationToken.None);
        }
        catch (ReservationUnavailableException)
        {
            return null;
        }
    }

    private static CreateJob DesktopJob(
        string kind,
        string sourceId,
        string quality)
    {
        var request = new CreateJob(
            Guid.NewGuid(),
            string.Empty,
            kind,
            "desktop_worker",
            Guid.NewGuid(),
            sourceId,
            quality,
            [],
            null,
            null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static async Task<(long Available, long Reserved)> ReadFreeStateAsync(
        ApiFixture f,
        Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            select available,reserved
            from licensing.entitlement_grants
            where account_id=@account
              and source='system_starter'
              and plan_id='free'
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<(long Reserved, long Spent)> ReadStartUsageAsync(
        ApiFixture f,
        Guid accountId,
        DateOnly date)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            select reserved,spent
            from licensing.daily_plan_usage
            where account_id=@account and usage_date=@date and plan_id='start'
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("date", date);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}
