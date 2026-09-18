using System.Net.Http.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PaymentRaceTests
{
    [Fact]
    public async Task Hundred_verified_replays_converge_to_one_grant_and_one_event()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "race-payer");
        var checkout = await CheckoutAsync(user.Client);
        var verified = new VerifiedPayment(
            "stars", "test", "race-charge-1", checkout.PaymentId,
            user.Id, new Money(30, "XTR"), "succeeded",
            f.Clock.GetUtcNow(), null, null);
        var store = f.Service<PaymentStore>();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => store.ApplyAsync(verified, CancellationToken.None)));

        Assert.All(results, item => Assert.Equal("succeeded", item.Status));
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
        Assert.Equal(1L, await ScalarAsync(
            f,
            "select count(*) from licensing.payment_events where payment_id=@id and status='succeeded'",
            checkout.PaymentId));
        Assert.Equal(1L, await ScalarAsync(
            f,
            "select count(*) from licensing.entitlement_grants where source_reference=@reference",
            "payment:" + checkout.PaymentId.ToString("D")));
    }

    [Fact]
    public async Task Same_provider_charge_cannot_back_two_payment_intents()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "charge-collision");
        var first = await CheckoutAsync(user.Client);
        var second = await CheckoutAsync(user.Client);
        var store = f.Service<PaymentStore>();
        var now = f.Clock.GetUtcNow();

        await store.ApplyAsync(
            new VerifiedPayment(
                "stars", "test", "shared-charge", first.PaymentId,
                user.Id, new Money(30, "XTR"), "succeeded",
                now, null, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<PaymentConflictException>(
            () => store.ApplyAsync(
                new VerifiedPayment(
                    "stars", "test", "shared-charge", second.PaymentId,
                    user.Id, new Money(30, "XTR"), "succeeded",
                    now, null, null),
                CancellationToken.None));

        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Verified_payment_cannot_be_applied_to_foreign_account()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AccountAsync("telegram", "payment-owner");
        var foreign = await f.AccountAsync("telegram", "payment-foreign");
        var checkout = await CheckoutAsync(owner.Client);

        await Assert.ThrowsAsync<PaymentConflictException>(
            () => f.Service<PaymentStore>().ApplyAsync(
                new VerifiedPayment(
                    "stars", "test", "foreign-charge", checkout.PaymentId,
                    foreign.Id, new Money(30, "XTR"), "succeeded",
                    f.Clock.GetUtcNow(), null, null),
                CancellationToken.None));

        var ownerAccess = await owner.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        var foreignAccess = await foreign.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, ownerAccess!.RemainingDownloads);
        Assert.Equal(0, foreignAccess!.RemainingDownloads);
    }

    private static async Task<PaymentCheckout> CheckoutAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(
                "test.credits3", "stars", Guid.NewGuid(), false));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentCheckout>())!;
    }

    private static async Task<long> ScalarAsync(
        ApiFixture f,
        string sql,
        object value)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (value is Guid guid)
            command.Parameters.AddWithValue("id", guid);
        else
            command.Parameters.AddWithValue("reference", value);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
