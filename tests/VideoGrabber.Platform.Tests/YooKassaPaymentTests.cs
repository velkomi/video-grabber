using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class YooKassaPaymentTests
{
    [Fact]
    public void Exact_money_conversion_does_not_round_kopecks()
    {
        Assert.Equal(3001, YooKassaPaymentAdapter.ParseRub("30.01").MinorUnits);
        Assert.Equal("30.01", YooKassaPaymentAdapter.FormatRub(3001));
        Assert.Throws<FormatException>(
            () => YooKassaPaymentAdapter.ParseRub("30.001"));
        Assert.Throws<FormatException>(
            () => YooKassaPaymentAdapter.ParseRub("-1.00"));
        Assert.Throws<FormatException>(
            () => YooKassaPaymentAdapter.ParseRub("0.00"));
        Assert.Throws<OverflowException>(
            () => YooKassaPaymentAdapter.ParseRub(
                "99999999999999999999.00"));
    }

    [Fact]
    public async Task Create_uses_server_catalog_basic_auth_and_stable_idempotence_key()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-create");
        var key = Guid.NewGuid();
        var request = new PurchaseRequest(
            "test.credits3", "yookassa", key, false);

        using var first = await user.Client.PostAsJsonAsync(
            "/v1/payments", request);
        first.EnsureSuccessStatusCode();
        var one = (await first.Content.ReadFromJsonAsync<PaymentCheckout>())!;
        using var second = await user.Client.PostAsJsonAsync(
            "/v1/payments", request);
        second.EnsureSuccessStatusCode();
        var two = (await second.Content.ReadFromJsonAsync<PaymentCheckout>())!;

        Assert.Equal(one.PaymentId, two.PaymentId);
        Assert.NotNull(one.RedirectUri);
        var requests = f.YooKassaApi.Requests
            .Where(x => x.Method == "POST"
                        && x.Path.EndsWith("/v3/payments", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, item =>
        {
            Assert.Equal(key.ToString("D"), item.IdempotenceKey);
            Assert.True(item.HasBasicAuthorization);
            Assert.DoesNotContain(
                "test-secret-never-production",
                item.Body,
                StringComparison.Ordinal);
            using var json = JsonDocument.Parse(item.Body);
            var root = json.RootElement;
            Assert.Equal("30.00",
                root.GetProperty("amount").GetProperty("value").GetString());
            Assert.Equal("RUB",
                root.GetProperty("amount").GetProperty("currency").GetString());
            Assert.True(root.GetProperty("capture").GetBoolean());
            Assert.Equal(
                "https://desktop.example.test/payment-return",
                root.GetProperty("confirmation").GetProperty("return_url").GetString());
        });
    }

    [Fact]
    public async Task Spoofed_succeeded_webhook_does_not_override_provider_pending()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-spoof");
        var checkout = await CreateAsync(user.Client);
        var providerId = f.YooKassaApi.LastPaymentId!;

        using var webhook = await f.Anonymous.PostAsJsonAsync(
            "/v1/payments/yookassa/webhook",
            new
            {
                type = "notification",
                @event = "payment.succeeded",
                @object = new
                {
                    id = providerId,
                    status = "succeeded",
                    paid = true,
                    amount = new { value = "30.00", currency = "RUB" }
                }
            });
        webhook.EnsureSuccessStatusCode();

        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
        var view = await user.Client.GetFromJsonAsync<PaymentView>(
            $"/v1/payments/{checkout.PaymentId:D}");
        Assert.Equal("pending", view!.Status);
    }

    [Fact]
    public async Task Provider_verified_succeeded_payment_grants_once()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-success");
        var checkout = await CreateAsync(user.Client);
        var providerId = f.YooKassaApi.LastPaymentId!;
        SetProviderPayment(
            f,
            providerId,
            checkout.InvoicePayload!,
            status: "succeeded",
            paid: true,
            test: true,
            value: "30.00");

        for (var i = 0; i < 2; i++)
        {
            using var webhook = await f.Anonymous.PostAsJsonAsync(
                "/v1/payments/yookassa/webhook",
                new
                {
                    type = "notification",
                    @event = "payment.succeeded",
                    @object = new { id = providerId, status = "pending" }
                });
            webhook.EnsureSuccessStatusCode();
        }

        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(3, access!.RemainingDownloads);
        var payment = await f.Service<PaymentStore>().ReadAsync(
            user.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("succeeded", payment!.Status);
    }

    [Theory]
    [InlineData(false, "30.00", HttpStatusCode.BadRequest)]
    [InlineData(true, "30.01", HttpStatusCode.BadRequest)]
    public async Task Provider_environment_or_amount_mismatch_is_rejected(
        bool test,
        string value,
        HttpStatusCode expected)
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync(
            "google", "yk-mismatch-" + Guid.NewGuid().ToString("N"));
        var checkout = await CreateAsync(user.Client);
        var providerId = f.YooKassaApi.LastPaymentId!;
        SetProviderPayment(
            f,
            providerId,
            checkout.InvoicePayload!,
            status: "succeeded",
            paid: true,
            test: test,
            value: value);

        using var webhook = await f.Anonymous.PostAsJsonAsync(
            "/v1/payments/yookassa/webhook",
            new
            {
                type = "notification",
                @event = "payment.succeeded",
                @object = new { id = providerId }
            });

        Assert.Equal(expected, webhook.StatusCode);
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Unknown_payment_notification_grants_nothing()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-unknown");

        using var webhook = await f.Anonymous.PostAsJsonAsync(
            "/v1/payments/yookassa/webhook",
            new
            {
                type = "notification",
                @event = "payment.succeeded",
                @object = new { id = "yk-pay-unknown" }
            });

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Provider_429_returns_503_and_leaves_payment_pending()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-429");
        var checkout = await CreateAsync(user.Client);
        var providerId = f.YooKassaApi.LastPaymentId!;
        f.YooKassaApi.FailNextStatusCode = HttpStatusCode.TooManyRequests;

        using var webhook = await f.Anonymous.PostAsJsonAsync(
            "/v1/payments/yookassa/webhook",
            new
            {
                type = "notification",
                @event = "payment.succeeded",
                @object = new { id = providerId }
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, webhook.StatusCode);
        var view = await user.Client.GetFromJsonAsync<PaymentView>(
            $"/v1/payments/{checkout.PaymentId:D}");
        Assert.Equal("pending", view!.Status);
    }

    [Fact]
    public async Task Browser_return_query_cannot_mark_payment_paid()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-return");
        var checkout = await CreateAsync(user.Client);

        var view = await user.Client.GetFromJsonAsync<PaymentView>(
            $"/v1/payments/{checkout.PaymentId:D}?success=true&paid=true");

        Assert.Equal("pending", view!.Status);
        var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
        Assert.Equal(0, access!.RemainingDownloads);
    }

    [Fact]
    public async Task Refund_uses_server_amount_and_provider_payment_id()
    {
        await using var f = await ApiFixture.StartAsync();
        var user = await f.AccountAsync("google", "yk-refund");
        var checkout = await CreateAsync(user.Client);
        var providerId = f.YooKassaApi.LastPaymentId!;
        SetProviderPayment(
            f,
            providerId,
            checkout.InvoicePayload!,
            "succeeded",
            paid: true,
            test: true,
            value: "30.00");
        var verified = await f.Service<YooKassaPaymentAdapter>()
            .ReadVerifiedAsync(providerId, CancellationToken.None);
        await f.Service<PaymentStore>()
            .ApplyAsync(verified, CancellationToken.None);
        f.YooKassaApi.Clear();

        var refundId = await f.Service<YooKassaPaymentAdapter>()
            .RefundAsync(
                new RefundRequest(
                    checkout.PaymentId, Guid.NewGuid(), "sandbox refund"),
                CancellationToken.None);

        Assert.StartsWith("yk-ref-", refundId, StringComparison.Ordinal);
        var request = Assert.Single(
            f.YooKassaApi.Requests,
            x => x.Method == "POST"
                 && x.Path.EndsWith("/v3/refunds", StringComparison.Ordinal));
        Assert.True(request.HasBasicAuthorization);
        Assert.False(string.IsNullOrWhiteSpace(request.IdempotenceKey));
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal(providerId,
            json.RootElement.GetProperty("payment_id").GetString());
        Assert.Equal("30.00",
            json.RootElement.GetProperty("amount").GetProperty("value").GetString());
        var payment = await f.Service<PaymentStore>().ReadAsync(
            user.Id, checkout.PaymentId, CancellationToken.None);
        Assert.Equal("refund_pending", payment!.Status);
    }

    private static async Task<PaymentCheckout> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/payments",
            new PurchaseRequest(
                "test.credits3", "yookassa", Guid.NewGuid(), false));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentCheckout>())!;
    }

    private static void SetProviderPayment(
        ApiFixture f,
        string providerId,
        string invoicePayload,
        string status,
        bool paid,
        bool test,
        string value)
    {
        f.YooKassaApi.SetPayment(
            providerId,
            new
            {
                id = providerId,
                status,
                paid,
                test,
                amount = new { value, currency = "RUB" },
                created_at = f.Clock.GetUtcNow().ToString("O"),
                metadata = new { order_id = invoicePayload },
                payment_method = new
                {
                    type = "bank_card",
                    id = "pm-" + providerId,
                    saved = false
                }
            });
    }
}
