using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PaymentProjectionTests
{
    [Fact]
    public async Task One_verified_charge_creates_one_purchase_grant()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93001");
        using var checkoutResponse = await user.Client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest("test.credits3", "stars", Guid.NewGuid(), false));
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<PaymentCheckout>())!;
        var confirmed = new VerifiedPayment(
            "stars", "test", "charge-1", checkout.PaymentId,
            user.Id, new Money(30, "XTR"), "succeeded",
            f.Clock.GetUtcNow(), null, null);

        await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(_ => f.Service<PaymentStore>()
                    .ApplyAsync(confirmed, CancellationToken.None)));

        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
        Assert.Equal(1L, await PurchaseGrantCountAsync(f, checkout.PaymentId));
        Assert.Equal(1L, await PaymentEventCountAsync(f, checkout.PaymentId, "succeeded"));
        Assert.Equal(1L, await IssueCountAsync(f, user.Id));
        var profile = await user.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.Equal("user", profile!.Role);
        Assert.NotNull(profile.FirstPurchaseAt);
    }

    [Fact]
    public async Task Pending_payment_does_not_grant_access()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93002");
        var checkout = await CheckoutAsync(user.Client, "test.credits3", "stars");
        var pending = new VerifiedPayment(
            "stars", "test", "pending-1", checkout.PaymentId,
            user.Id, new Money(30, "XTR"), "pending",
            f.Clock.GetUtcNow(), null, null);

        await f.Service<PaymentStore>().ApplyAsync(pending, CancellationToken.None);

        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
        Assert.Equal(0L, await PurchaseGrantCountAsync(f, checkout.PaymentId));
    }

    [Theory]
    [InlineData(31, "XTR", "test")]
    [InlineData(30, "USD", "test")]
    [InlineData(30, "XTR", "live")]
    public async Task Verified_payment_must_match_catalog_and_environment(
        long minor,
        string currency,
        string environment)
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93006");
        var checkout = await CheckoutAsync(user.Client, "test.credits3", "stars");
        var payment = new VerifiedPayment(
            "stars", environment, "mismatch-charge-" + Guid.NewGuid().ToString("N"),
            checkout.PaymentId, user.Id,
            new Money(minor, currency), "succeeded",
            f.Clock.GetUtcNow(), null, null);

        await Assert.ThrowsAsync<PaymentConflictException>(
            () => f.Service<PaymentStore>().ApplyAsync(payment, CancellationToken.None));

        Assert.Equal(0L, await PurchaseGrantCountAsync(f, checkout.PaymentId));
    }

    [Fact]
    public async Task Altered_body_with_same_payment_idempotency_key_conflicts()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93003");
        var key = Guid.NewGuid();
        using var first = await user.Client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest("test.credits3", "stars", key, false));
        first.EnsureSuccessStatusCode();
        using var altered = await user.Client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest("test.time30", "stars", key, true));
        Assert.Equal(HttpStatusCode.Conflict, altered.StatusCode);
    }

    [Fact]
    public async Task Transaction_fault_between_event_and_grant_rolls_back_everything()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93004");
        var checkout = await CheckoutAsync(user.Client, "test.credits3", "stars");
        var catalog = PaymentCatalog.Load(CatalogPath());
        var faulted = PaymentStore.CreateForTesting(
            f.Service<CreditLedger>(),
            f.Clock,
            catalog,
            point =>
            {
                if (point == "after_payment_event")
                    throw new InvalidOperationException("synthetic payment crash");
            });
        var confirmed = new VerifiedPayment(
            "stars", "test", "crash-charge", checkout.PaymentId,
            user.Id, new Money(30, "XTR"), "succeeded",
            f.Clock.GetUtcNow(), null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => faulted.ApplyAsync(confirmed, CancellationToken.None));

        Assert.Equal(0L, await PaymentEventCountAsync(f, checkout.PaymentId, "succeeded"));
        Assert.Equal(0L, await PurchaseGrantCountAsync(f, checkout.PaymentId));

        await f.Service<PaymentStore>().ApplyAsync(confirmed, CancellationToken.None);
        Assert.Equal(1L, await PurchaseGrantCountAsync(f, checkout.PaymentId));
    }

    [Fact]
    public async Task Refunded_payment_cannot_be_regranted_by_late_success_replay()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "93005");
        var checkout = await CheckoutAsync(user.Client, "test.credits3", "stars");
        var succeeded = new VerifiedPayment(
            "stars", "test", "refund-charge", checkout.PaymentId,
            user.Id, new Money(30, "XTR"), "succeeded",
            f.Clock.GetUtcNow(), null, null);
        var store = f.Service<PaymentStore>();
        await store.ApplyAsync(succeeded, CancellationToken.None);
        await store.ApplyAsync(
            succeeded with
            {
                Status = "refunded",
                OccurredAt = f.Clock.GetUtcNow().AddMinutes(1)
            },
            CancellationToken.None);
        await store.ApplyAsync(succeeded, CancellationToken.None);

        var payment = await store.ReadAsync(
            user.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("refunded", payment!.Status);
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
        Assert.Equal(1L, await PurchaseGrantCountAsync(f, checkout.PaymentId));
    }

    [Fact]
    public void Money_validation_rejects_negative_zero_and_bad_currency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PaymentProjection.ValidateMoney(new Money(0, "XTR")));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PaymentProjection.ValidateMoney(new Money(-1, "XTR")));
        Assert.Throws<ArgumentException>(
            () => PaymentProjection.ValidateMoney(new Money(1, "12")));
    }

    private static async Task<PaymentCheckout> CheckoutAsync(
        HttpClient client,
        string sku,
        string provider)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(sku, provider, Guid.NewGuid(), false));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentCheckout>())!;
    }

    private static async Task<long> PurchaseGrantCountAsync(
        ApiFixture f,
        Guid paymentId)
        => await ScalarAsync(
            f,
            """
            select count(*) from licensing.entitlement_grants
            where source='purchase' and source_reference=@reference
            """,
            "reference",
            "payment:" + paymentId.ToString("D"));

    private static async Task<long> PaymentEventCountAsync(
        ApiFixture f,
        Guid paymentId,
        string status)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            """
            select count(*) from licensing.payment_events
            where payment_id=@payment and status=@status
            """,
            connection);
        command.Parameters.AddWithValue("payment", paymentId);
        command.Parameters.AddWithValue("status", status);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> IssueCountAsync(ApiFixture f, Guid accountId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            """
            select count(*) from licensing.credit_ledger
            where account_id=@account and event_kind='issue'
            """,
            connection);
        command.Parameters.AddWithValue("account", accountId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarAsync(
        ApiFixture f,
        string sql,
        string parameter,
        object value)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(parameter, value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static string CatalogPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solution = Path.Combine(current.FullName, "VideoGrabber.slnx");
            if (File.Exists(solution))
                return Path.Combine(
                    current.FullName,
                    "tests",
                    "VideoGrabber.Platform.Tests",
                    "Fixtures",
                    "payment-catalog.test.json");
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
