using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class StarsPaymentTests
{
    [Fact]
    public void Precheckout_does_not_mean_payment_succeeded()
    {
        var update = JsonSerializer.SerializeToElement(new
        {
            update_id = 10,
            pre_checkout_query = new
            {
                id = "query",
                currency = "XTR",
                total_amount = 30,
                invoice_payload = "opaque-invoice"
            }
        });
        Assert.False(StarsUpdateHandler.ContainsSuccessfulPayment(update));
    }

    [Fact]
    public async Task Invoice_uses_server_catalog_and_precheckout_grants_nothing()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9201;
        var account = await f.AccountAsync("telegram", payer.ToString());
        f.TelegramApi.Clear();

        using var create = await account.Client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(
                "test.credits3", "stars", Guid.NewGuid(), false));
        create.EnsureSuccessStatusCode();
        var checkout = (await create.Content.ReadFromJsonAsync<PaymentCheckout>())!;
        Assert.Equal("pending", checkout.State);
        Assert.NotNull(checkout.RedirectUri);
        Assert.NotNull(checkout.InvoicePayload);

        var invoice = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith("/createInvoiceLink", StringComparison.Ordinal));
        using (var invoiceJson = JsonDocument.Parse(invoice.Body))
        {
            var root = invoiceJson.RootElement;
            Assert.Equal("XTR", root.GetProperty("currency").GetString());
            Assert.Equal("", root.GetProperty("provider_token").GetString());
            Assert.Equal(30, root.GetProperty("prices")[0].GetProperty("amount").GetInt64());
            Assert.Equal(checkout.InvoicePayload, root.GetProperty("payload").GetString());
        }

        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9901, new
        {
            update_id = 9901L,
            pre_checkout_query = new
            {
                id = "pre-1",
                from = new { id = payer },
                currency = "XTR",
                total_amount = 30,
                invoice_payload = checkout.InvoicePayload
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));

        var answer = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith("/answerPreCheckoutQuery", StringComparison.Ordinal));
        using (var answerJson = JsonDocument.Parse(answer.Body))
            Assert.True(answerJson.RootElement.GetProperty("ok").GetBoolean());
        var access = await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Successful_payment_grants_once_and_replay_is_idempotent()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9202;
        var account = await f.AccountAsync("telegram", payer.ToString());
        var checkout = await CreateStarsCheckoutAsync(account.Client, "test.credits3", false);
        var payment = new
        {
            currency = "XTR",
            total_amount = 30,
            invoice_payload = checkout.InvoicePayload,
            telegram_payment_charge_id = "stars-charge-9202",
            provider_payment_charge_id = ""
        };

        await PostWebhookAsync(f, 9902, new
        {
            update_id = 9902L,
            message = new
            {
                message_id = 1L,
                date = f.Clock.GetUtcNow().ToUnixTimeSeconds(),
                from = new { id = payer },
                chat = new { id = payer, type = "private" },
                successful_payment = payment
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));

        await PostWebhookAsync(f, 9903, new
        {
            update_id = 9903L,
            message = new
            {
                message_id = 2L,
                date = f.Clock.GetUtcNow().ToUnixTimeSeconds(),
                from = new { id = payer },
                chat = new { id = payer, type = "private" },
                successful_payment = payment
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));

        var access = await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
        var view = await f.Service<PaymentStore>()
            .ReadAsync(account.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("succeeded", view!.Status);
    }

    [Theory]
    [InlineData(999999L, "XTR", 30)]
    [InlineData(9203L, "USD", 30)]
    [InlineData(9203L, "XTR", 31)]
    public async Task Precheckout_rejects_payer_currency_or_amount_mismatch(
        long payer,
        string currency,
        long amount)
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "9203");
        var checkout = await CreateStarsCheckoutAsync(account.Client, "test.credits3", false);
        f.TelegramApi.Clear();

        await PostWebhookAsync(f, 9904 + amount, new
        {
            update_id = 9904L + amount,
            pre_checkout_query = new
            {
                id = "pre-bad-" + amount,
                from = new { id = payer },
                currency,
                total_amount = amount,
                invoice_payload = checkout.InvoicePayload
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var answer = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith("/answerPreCheckoutQuery", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(answer.Body);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        var access = await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Reconciliation_can_recover_missed_success_from_star_transaction()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9204;
        var account = await f.AccountAsync("telegram", payer.ToString());
        var checkout = await CreateStarsCheckoutAsync(account.Client, "test.credits3", false);
        var occurred = f.Clock.GetUtcNow();

        f.TelegramApi.SetStarTransactions(new
        {
            id = "reconcile-charge-9204",
            amount = 30,
            date = occurred.ToUnixTimeSeconds(),
            source = new
            {
                type = "user",
                transaction_type = "invoice_payment",
                user = new { id = payer },
                invoice_payload = checkout.InvoicePayload
            }
        });

        var count = await f.Service<StarsPaymentAdapter>()
            .ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, count);
        var access = await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Lost_refund_ack_stays_pending_until_provider_transaction_confirms_refund()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9205;
        var account = await f.AccountAsync("telegram", payer.ToString());
        var checkout = await CreateStarsCheckoutAsync(account.Client, "test.credits3", false);
        var store = f.Service<PaymentStore>();
        var succeeded = new VerifiedPayment(
            "stars", "test", "refund-stars-9205",
            checkout.PaymentId, account.Id,
            new Money(30, "XTR"), "succeeded",
            f.Clock.GetUtcNow(), null, null);
        await store.ApplyAsync(succeeded, CancellationToken.None);

        f.TelegramApi.LoseNextRefundAck = true;
        await Assert.ThrowsAsync<HttpRequestException>(
            () => f.Service<StarsPaymentAdapter>().RefundAsync(
                new RefundRequest(
                    checkout.PaymentId, Guid.NewGuid(), "test refund"),
                CancellationToken.None));
        var pending = await store.ReadAsync(
            account.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("refund_pending", pending!.Status);

        f.TelegramApi.SetStarTransactions(new
        {
            id = "refund-stars-9205",
            amount = -30,
            date = f.Clock.GetUtcNow().AddMinutes(1).ToUnixTimeSeconds(),
            receiver = new
            {
                type = "user",
                transaction_type = "invoice_payment",
                user = new { id = payer },
                invoice_payload = checkout.InvoicePayload
            }
        });
        Assert.Equal(1, await f.Service<StarsPaymentAdapter>()
            .ReconcileAsync(CancellationToken.None));

        var refunded = await store.ReadAsync(
            account.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("refunded", refunded!.Status);
        var access = await account.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Paysupport_is_private_and_lists_only_current_accounts_references()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9206;
        var account = await f.AccountAsync("telegram", payer.ToString());
        var checkout = await CreateStarsCheckoutAsync(account.Client, "test.credits3", false);
        var foreign = await f.AccountAsync("telegram", "9207");
        var foreignCheckout = await CreateStarsCheckoutAsync(
            foreign.Client, "test.credits3", false);
        f.TelegramApi.Clear();

        await PostWebhookAsync(f, 9920, new
        {
            update_id = 9920L,
            message = new
            {
                message_id = 20L,
                from = new { id = payer },
                chat = new { id = payer, type = "private" },
                text = "/paysupport"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var request = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(request.Body);
        var text = json.RootElement.GetProperty("text").GetString() ?? "";
        Assert.Contains("support@example.test", text);
        Assert.Contains(checkout.PaymentId.ToString("D"), text);
        Assert.DoesNotContain(foreignCheckout.PaymentId.ToString("D"), text);

        f.TelegramApi.Clear();
        await PostWebhookAsync(f, 9921, new
        {
            update_id = 9921L,
            message = new
            {
                message_id = 21L,
                from = new { id = payer },
                chat = new { id = -1009206L, type = "group" },
                text = "/paysupport"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));
        var group = Assert.Single(
            f.TelegramApi.Requests,
            x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        using var groupJson = JsonDocument.Parse(group.Body);
        Assert.Contains(
            "личн",
            groupJson.RootElement.GetProperty("text").GetString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bot_buy_creates_Stars_invoice_and_never_external_checkout()
    {
        await using var f = await ApiFixture.StartAsync();
        const long payer = 9208;
        _ = await f.AccountAsync("telegram", payer.ToString());
        f.TelegramApi.Clear();

        await PostWebhookAsync(f, 9930, new
        {
            update_id = 9930L,
            message = new
            {
                message_id = 30L,
                from = new { id = payer },
                chat = new { id = payer, type = "private" },
                text = "/buy test.credits3"
            }
        });
        Assert.True(await f.Service<TelegramInboxWorker>()
            .RunOnceAsync(CancellationToken.None));

        Assert.Contains(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/createInvoiceLink", StringComparison.Ordinal));
        var send = Assert.Single(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/sendMessage", StringComparison.Ordinal));
        Assert.DoesNotContain("yookassa", send.Body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(send.Body);
        var url = json.RootElement.GetProperty("reply_markup")
            .GetProperty("inline_keyboard")[0][0].GetProperty("url").GetString();
        Assert.StartsWith("https://t.me/", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_stars_refund_requires_fresh_MFA()
    {
        await using var f = await ApiFixture.StartAsync();
        var payer = await f.AccountAsync("telegram", "9209");
        var checkout = await CreateStarsCheckoutAsync(
            payer.Client, "test.credits3", false);
        await f.Service<PaymentStore>().ApplyAsync(
            new VerifiedPayment(
                "stars", "test", "admin-refund-charge",
                checkout.PaymentId, payer.Id,
                new Money(30, "XTR"), "succeeded",
                f.Clock.GetUtcNow(), null, null),
            CancellationToken.None);

        var actor = await f.AccountAsync("google", "refund-admin");
        await f.PromoteAdminAsync(actor.Id);
        var body = new RefundRequest(
            checkout.PaymentId, Guid.NewGuid(), "customer requested refund");
        using var withoutMfa = await actor.Client.PostAsJsonAsync(
            $"/v1/admin/payments/{checkout.PaymentId:D}/stars-refund", body);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutMfa.StatusCode);

        f.TelegramApi.Clear();
        using var fresh = f.FreshMfaClient(actor.Id);
        using var refunded = await fresh.PostAsJsonAsync(
            $"/v1/admin/payments/{checkout.PaymentId:D}/stars-refund", body);
        refunded.EnsureSuccessStatusCode();
        Assert.Contains(f.TelegramApi.Requests,
            x => x.Path.EndsWith("/refundStarPayment", StringComparison.Ordinal));
        var view = await f.Service<PaymentStore>().ReadAsync(
            payer.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("refund_pending", view!.Status);
    }
    private static async Task<PaymentCheckout> CreateStarsCheckoutAsync(
        HttpClient client,
        string sku,
        bool recurring)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(sku, "stars", Guid.NewGuid(), recurring));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentCheckout>())!;
    }

    private static async Task PostWebhookAsync(
        ApiFixture f,
        long updateId,
        object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/v1/telegram/webhook")
        {
            Content = new StringContent(
                json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(
            "X-Telegram-Bot-Api-Secret-Token",
            TelegramWebhookTests.WebhookSecret);
        using var response = await f.Anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
