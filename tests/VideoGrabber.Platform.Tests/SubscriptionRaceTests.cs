using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SubscriptionRaceTests
{
    [Fact]
    public void Cancel_keeps_the_already_paid_period()
    {
        var paidThrough = new DateTimeOffset(
            2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var active = new SubscriptionView(
            Guid.NewGuid(), Guid.NewGuid(),
            "active", true, paidThrough, "stars");

        var canceled = SubscriptionService.ApplyCancel(active);

        Assert.False(canceled.AutoRenew);
        Assert.Equal("canceled", canceled.State);
        Assert.Equal(paidThrough, canceled.PaidThrough);
    }

    [Fact]
    public async Task Hundred_duplicate_renewals_create_one_period_grant()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9401");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        var start = seeded.View.PaidThrough;
        var end = start.AddDays(30);
        var renewal = new RenewalEvent(
            "stars", "test", "renew-9401",
            seeded.View.SubscriptionId, start, end,
            new Money(100, "XTR"));
        var store = f.Service<SubscriptionStore>();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => store.ApplyRenewalAsync(
                    renewal, CancellationToken.None)));

        Assert.All(results, x => Assert.Equal(end, x.PaidThrough));
        Assert.Equal(1L, await RenewalEventCountAsync(
            f, seeded.View.SubscriptionId, "renew-9401"));
        Assert.Equal(1L, await RenewalGrantCountAsync(
            f, seeded.View.SubscriptionId, start));
    }

    [Fact]
    public async Task Late_previous_period_never_shortens_paid_through()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9402");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        var store = f.Service<SubscriptionStore>();
        var currentEnd = seeded.View.PaidThrough.AddDays(30);
        await store.ApplyRenewalAsync(
            new RenewalEvent(
                "stars", "test", "newer-charge",
                seeded.View.SubscriptionId,
                seeded.View.PaidThrough,
                currentEnd,
                new Money(100, "XTR")),
            CancellationToken.None);

        var lateEnd = seeded.View.PaidThrough;
        var lateStart = lateEnd.AddDays(-30);
        var result = await store.ApplyRenewalAsync(
            new RenewalEvent(
                "stars", "test", "late-charge",
                seeded.View.SubscriptionId,
                lateStart,
                lateEnd,
                new Money(100, "XTR")),
            CancellationToken.None);

        Assert.Equal(currentEnd, result.PaidThrough);
    }

    [Fact]
    public async Task Renewal_while_cancel_pending_is_honored_but_auto_renew_stays_off()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9403");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        var store = f.Service<SubscriptionStore>();
        var pending = await store.RequestCancelAsync(
            user.Id, seeded.View.SubscriptionId,
            Guid.NewGuid(), CancellationToken.None);
        Assert.False(pending.AutoRenew);
        Assert.Equal("cancel_pending", pending.State);

        var renewedEnd = seeded.View.PaidThrough.AddDays(30);
        var renewed = await store.ApplyRenewalAsync(
            new RenewalEvent(
                "stars", "test", "cancel-race-charge",
                seeded.View.SubscriptionId,
                seeded.View.PaidThrough,
                renewedEnd,
                new Money(100, "XTR")),
            CancellationToken.None);

        Assert.Equal("cancel_pending", renewed.State);
        Assert.False(renewed.AutoRenew);
        Assert.Equal(renewedEnd, renewed.PaidThrough);

        var canceled = await store.ConfirmCancelAsync(
            user.Id, seeded.View.SubscriptionId,
            CancellationToken.None);
        Assert.Equal("canceled", canceled.State);
        Assert.False(canceled.AutoRenew);
        Assert.Equal(renewedEnd, canceled.PaidThrough);
    }

    [Fact]
    public async Task Refund_of_renewal_revokes_period_and_replayed_success_cannot_restore_it()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9404");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        var store = f.Service<SubscriptionStore>();
        var start = seeded.View.PaidThrough;
        var end = start.AddDays(30);
        var renewal = new RenewalEvent(
            "stars", "test", "refundable-renewal",
            seeded.View.SubscriptionId, start, end,
            new Money(100, "XTR"));

        await store.ApplyRenewalAsync(renewal, CancellationToken.None);
        await store.ApplyTerminalAdjustmentAsync(
            seeded.View.SubscriptionId,
            renewal.ChargeId,
            "refund",
            end.AddMinutes(1),
            CancellationToken.None);
        await store.ApplyRenewalAsync(renewal, CancellationToken.None);

        Assert.True(await RenewalGrantRevokedAsync(
            f, seeded.View.SubscriptionId, start));
        Assert.Equal(1L, await RenewalEventCountAsync(
            f, seeded.View.SubscriptionId, renewal.ChargeId));
    }

    [Fact]
    public async Task Chargeback_enters_review_and_disables_auto_renew()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9405");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        var store = f.Service<SubscriptionStore>();
        var start = seeded.View.PaidThrough;
        var renewal = new RenewalEvent(
            "stars", "test", "chargeback-renewal",
            seeded.View.SubscriptionId,
            start, start.AddDays(30),
            new Money(100, "XTR"));
        await store.ApplyRenewalAsync(renewal, CancellationToken.None);

        var view = await store.ApplyTerminalAdjustmentAsync(
            seeded.View.SubscriptionId,
            renewal.ChargeId,
            "chargeback",
            start.AddDays(1),
            CancellationToken.None);

        Assert.Equal("review_required", view.State);
        Assert.False(view.AutoRenew);
    }

    [Fact]
    public async Task Stars_cancel_calls_provider_and_preserves_paid_through()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("telegram", "9406");
        var seeded = await SeedRecurringAsync(f, user.Id, "stars");
        f.TelegramApi.Clear();

        using var response = await user.Client.PostAsJsonAsync(
            $"/v1/subscriptions/{seeded.View.SubscriptionId:D}/cancel",
            new CancelSubscriptionRequest(Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        var canceled = (await response.Content
            .ReadFromJsonAsync<SubscriptionView>())!;

        Assert.Equal("canceled", canceled.State);
        Assert.False(canceled.AutoRenew);
        Assert.Equal(seeded.View.PaidThrough, canceled.PaidThrough);
        var provider = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith(
                "/editUserStarSubscription",
                StringComparison.Ordinal));
        using var json = JsonDocument.Parse(provider.Body);
        Assert.Equal(9406,
            json.RootElement.GetProperty("user_id").GetInt64());
        Assert.True(json.RootElement.GetProperty("is_canceled").GetBoolean());
    }

    [Fact]
    public async Task Stars_webhook_first_and_renewal_charge_project_separate_periods()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9407;
        var user = await f.AccountAsync("telegram", payer.ToString());
        using var create = await user.Client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(
                "test.time30", "stars", Guid.NewGuid(), true));
        create.EnsureSuccessStatusCode();
        var checkout = (await create.Content
            .ReadFromJsonAsync<PaymentCheckout>())!;
        var firstEnd = f.Clock.GetUtcNow().AddDays(30);

        await PostStarsPaymentAsync(
            f, 12001, payer, checkout.InvoicePayload!,
            "stars-first-9407", firstEnd,
            isFirst: true);
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var sub = Assert.Single(
            await f.Service<SubscriptionStore>()
                .ListAsync(user.Id, CancellationToken.None));
        Assert.Equal(firstEnd.ToUnixTimeSeconds(),
            sub.PaidThrough.ToUnixTimeSeconds());

        var secondEnd = firstEnd.AddDays(30);
        await PostStarsPaymentAsync(
            f, 12002, payer, checkout.InvoicePayload!,
            "stars-renew-9407", secondEnd,
            isFirst: false);
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));

        var renewed = await f.Service<SubscriptionStore>()
            .ReadAsync(user.Id, sub.SubscriptionId, CancellationToken.None);
        Assert.Equal(secondEnd.ToUnixTimeSeconds(),
            renewed!.PaidThrough.ToUnixTimeSeconds());
        Assert.Equal(1L, await RenewalEventCountAsync(
            f, sub.SubscriptionId, "stars-renew-9407"));
    }

    [Fact]
    public async Task YooKassa_period_uses_stable_idempotency_and_verified_renewal()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-subscription");
        var seeded = await SeedRecurringAsync(f, user.Id, "yookassa");
        var service = f.Service<SubscriptionService>();
        var start = seeded.View.PaidThrough;

        var one = await service.CreateYooKassaRenewalAsync(
            user.Id, seeded.View.SubscriptionId,
            start, CancellationToken.None);
        var two = await service.CreateYooKassaRenewalAsync(
            user.Id, seeded.View.SubscriptionId,
            start, CancellationToken.None);

        Assert.Equal(one.IdempotencyKey, two.IdempotencyKey);
        Assert.Equal(one.ProviderPaymentId, two.ProviderPaymentId);
        var creates = f.YooKassaApi.Requests
            .Where(x => x.Method == "POST"
                        && x.Path.EndsWith("/v3/payments",
                            StringComparison.Ordinal))
            .ToArray();
        Assert.True(creates.Length >= 2);
        Assert.Equal(
            creates[^1].IdempotenceKey,
            creates[^2].IdempotenceKey);

        f.YooKassaApi.SetPayment(
            one.ProviderPaymentId,
            new
            {
                id = one.ProviderPaymentId,
                status = "succeeded",
                paid = true,
                test = true,
                amount = new { value = "100.00", currency = "RUB" },
                created_at = f.Clock.GetUtcNow().ToString("O"),
                metadata = new
                {
                    subscription_id =
                        seeded.View.SubscriptionId.ToString("D"),
                    period_start_ms = start.ToUnixTimeMilliseconds()
                },
                payment_method = new
                {
                    type = "bank_card",
                    id = seeded.Record.ProviderReference,
                    saved = true
                }
            });

        var renewed = await service.VerifyYooKassaRenewalAsync(
            one.ProviderPaymentId,
            CancellationToken.None);

        Assert.Equal(start.AddDays(30), renewed.PaidThrough);
        Assert.Equal(1L, await RenewalGrantCountAsync(
            f, seeded.View.SubscriptionId, start));
    }

    private static async Task<SeededSubscription> SeedRecurringAsync(
        ApiFixture f,
        Guid accountId,
        string provider)
    {
        var store = f.Service<PaymentStore>();
        var subscriptions = f.Service<SubscriptionStore>();
        var request = new PurchaseRequest(
            "test.time30", provider, Guid.NewGuid(), true);
        var checkout = await store.BeginAsync(
            accountId, request, CancellationToken.None);
        var now = f.Clock.GetUtcNow();
        var amount = provider == "stars"
            ? new Money(100, "XTR")
            : new Money(10000, "RUB");
        var charge = provider + "-origin-" + Guid.NewGuid().ToString("N");
        var verified = new VerifiedPayment(
            provider, "test", charge,
            checkout.PaymentId, accountId,
            amount, "succeeded", now,
            provider == "stars" ? charge : "pm-saved-" + accountId.ToString("N"),
            now.AddDays(30));
        await store.ApplyAsync(verified, CancellationToken.None);
        var intent = await store.ReadIntentAsync(
            checkout.PaymentId, CancellationToken.None)
            ?? throw new InvalidDataException();
        var view = await subscriptions.ProjectInitialPaymentAsync(
            intent, verified, CancellationToken.None)
            ?? throw new InvalidDataException();
        var record = await subscriptions.ReadRecordAsync(
            accountId, view.SubscriptionId, CancellationToken.None)
            ?? throw new InvalidDataException();
        return new SeededSubscription(view, record);
    }

    private static async Task PostStarsPaymentAsync(
        ApiFixture f,
        long updateId,
        long payer,
        string invoicePayload,
        string chargeId,
        DateTimeOffset paidThrough,
        bool isFirst)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    update_id = updateId,
                    message = new
                    {
                        message_id = updateId,
                        date = f.Clock.GetUtcNow().ToUnixTimeSeconds(),
                        from = new { id = payer },
                        chat = new { id = payer, type = "private" },
                        successful_payment = new
                        {
                            currency = "XTR",
                            total_amount = 100,
                            invoice_payload = invoicePayload,
                            telegram_payment_charge_id = chargeId,
                            provider_payment_charge_id = "",
                            is_recurring = true,
                            is_first_recurring = isFirst,
                            subscription_expiration_date =
                                paidThrough.ToUnixTimeSeconds()
                        }
                    }
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(
            "X-Telegram-Bot-Api-Secret-Token",
            TelegramWebhookTests.WebhookSecret);
        using var response = await f.Anonymous.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<long> RenewalEventCountAsync(
        ApiFixture f,
        Guid subscriptionId,
        string chargeId)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select count(*) from licensing.subscription_events
            where subscription_id=@subscription
              and event_kind='renewal'
              and charge_id=@charge
            """,
            connection);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        command.Parameters.AddWithValue("charge", chargeId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> RenewalGrantCountAsync(
        ApiFixture f,
        Guid subscriptionId,
        DateTimeOffset periodStart)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select count(*) from licensing.entitlement_grants
            where source='purchase' and source_reference=@reference
            """,
            connection);
        command.Parameters.AddWithValue(
            "reference",
            "subscription:" + subscriptionId.ToString("D")
            + ":" + periodStart.ToUnixTimeSeconds());
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> RenewalGrantRevokedAsync(
        ApiFixture f,
        Guid subscriptionId,
        DateTimeOffset periodStart)
    {
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select revoked_at is not null
            from licensing.entitlement_grants
            where source='purchase' and source_reference=@reference
            """,
            connection);
        command.Parameters.AddWithValue(
            "reference",
            "subscription:" + subscriptionId.ToString("D")
            + ":" + periodStart.ToUnixTimeSeconds());
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private sealed record SeededSubscription(
        SubscriptionView View,
        SubscriptionRecord Record);
}
