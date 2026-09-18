using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public sealed class YooKassaPaymentAdapter(
    PaymentStore payments,
    IHttpClientFactory clients,
    IConfiguration configuration,
    TimeProvider clock) : IPaymentAdapter
{
    private static readonly Uri ApiBase =
        new("https://api.yookassa.ru/v3/", UriKind.Absolute);

    public static Money ParseRub(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Regex.IsMatch(value, @"^[0-9]+\.[0-9]{2}$"))
            throw new FormatException("invalid_money");
        var parts = value.Split('.');
        var major = long.Parse(parts[0], CultureInfo.InvariantCulture);
        var minorPart = long.Parse(parts[1], CultureInfo.InvariantCulture);
        var minor = checked(major * 100 + minorPart);
        if (minor <= 0) throw new FormatException("invalid_money");
        return new Money(minor, "RUB");
    }

    public static string FormatRub(long minorUnits)
    {
        if (minorUnits <= 0) throw new ArgumentOutOfRangeException(nameof(minorUnits));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minorUnits / 100}.{minorUnits % 100:00}");
    }

    public async Task<PaymentCheckout> CreateAsync(
        Guid accountId,
        PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Provider != "yookassa")
            throw new ArgumentException("YooKassa adapter requires provider=yookassa.");
        var credentials = Credentials();
        var returnUri = ReturnUri();
        var checkout = await payments.BeginAsync(
            accountId, request, cancellationToken).ConfigureAwait(false);
        var product = payments.Catalog.RequireProduct(
            request.Sku, "yookassa", request.Recurring);
        var price = product.Prices["yookassa"];
        if (price.Currency != "RUB")
            throw new InvalidOperationException("YooKassa catalog price must be RUB.");

        var body = new Dictionary<string, object?>
        {
            ["amount"] = new
            {
                value = FormatRub(price.MinorUnits),
                currency = "RUB"
            },
            ["capture"] = true,
            ["confirmation"] = new
            {
                type = "redirect",
                return_url = returnUri.AbsoluteUri
            },
            ["description"] = "VideoGrabber " + request.Sku,
            ["metadata"] = new
            {
                order_id = checkout.InvoicePayload
            }
        };
        if (request.Recurring)
            body["save_payment_method"] = true;

        using var message = Request(
            HttpMethod.Post, "payments", credentials, request.IdempotencyKey.ToString("D"));
        message.Content = JsonContent.Create(body);
        using var response = await clients.CreateClient("YooKassa")
            .SendAsync(message, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode == 429
            || response.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException(
                "yookassa_retryable",
                null,
                response.StatusCode);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var providerId = RequiredString(root, "id");
        await payments.BindProviderReferenceAsync(
            checkout.PaymentId, providerId, cancellationToken).ConfigureAwait(false);

        ValidateTestEnvironment(root);
        ValidateAmount(root, price);
        if (!root.TryGetProperty("metadata", out var metadata)
            || metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("order_id", out var orderId)
            || orderId.ValueKind != JsonValueKind.String
            || orderId.GetString() != checkout.InvoicePayload)
            throw new PaymentConflictException();

        Uri? redirect = null;
        if (root.TryGetProperty("confirmation", out var confirmation)
            && confirmation.ValueKind == JsonValueKind.Object
            && confirmation.TryGetProperty("confirmation_url", out var confirmationUrl)
            && confirmationUrl.ValueKind == JsonValueKind.String)
        {
            if (!Uri.TryCreate(
                    confirmationUrl.GetString(),
                    UriKind.Absolute,
                    out redirect)
                || redirect.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("YooKassa confirmation URL is invalid.");
        }

        return checkout with
        {
            State = NormalizeStatus(root),
            RedirectUri = redirect
        };
    }

    public async Task<VerifiedPayment> ReadVerifiedAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        ValidateProviderId(providerPaymentId);
        var credentials = Credentials();
        using var message = Request(
            HttpMethod.Get,
            "payments/" + Uri.EscapeDataString(providerPaymentId),
            credentials,
            null);
        using var response = await clients.CreateClient("YooKassa")
            .SendAsync(message, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode == 429
            || response.StatusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException(
                "yookassa_retryable",
                null,
                response.StatusCode);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException("YooKassa payment was not found.");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (RequiredString(root, "id") != providerPaymentId)
            throw new PaymentConflictException();
        ValidateTestEnvironment(root);

        if (!root.TryGetProperty("metadata", out var metadata)
            || metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("order_id", out var orderId)
            || orderId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(orderId.GetString()))
            throw new PaymentConflictException();
        var intent = await payments.ReadIntentByInvoiceAsync(
            orderId.GetString()!, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Unknown YooKassa order.");
        if (intent.Provider != "yookassa")
            throw new PaymentConflictException();
        if (intent.ProviderPaymentId is { Length: > 0 }
            && intent.ProviderPaymentId != providerPaymentId)
            throw new PaymentConflictException();

        ValidateAmount(root, new CatalogPrice(
            intent.Amount.MinorUnits, intent.Amount.Currency));

        var providerStatus = RequiredString(root, "status");
        var paid = root.TryGetProperty("paid", out var paidElement)
            && paidElement.ValueKind == JsonValueKind.True;
        var status = providerStatus switch
        {
            "pending" => "pending",
            "waiting_for_capture" => "pending",
            "succeeded" when paid => "succeeded",
            "canceled" => "canceled",
            _ => throw new PaymentConflictException()
        };

        var occurred = clock.GetUtcNow();
        if (root.TryGetProperty("created_at", out var createdAt)
            && createdAt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                createdAt.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
            occurred = parsed;

        string? savedMethod = null;
        if (root.TryGetProperty("payment_method", out var paymentMethod)
            && paymentMethod.ValueKind == JsonValueKind.Object
            && paymentMethod.TryGetProperty("saved", out var saved)
            && saved.ValueKind == JsonValueKind.True
            && paymentMethod.TryGetProperty("id", out var methodId)
            && methodId.ValueKind == JsonValueKind.String)
            savedMethod = methodId.GetString();

        return new VerifiedPayment(
            "yookassa",
            intent.Environment,
            providerPaymentId,
            intent.PaymentId,
            intent.AccountId,
            intent.Amount,
            status,
            occurred,
            savedMethod,
            null);
    }

    public async Task<string> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken)
    {
        var intent = await payments.ReadIntentAsync(
            request.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Payment was not found.");
        if (intent.Provider != "yookassa"
            || intent.State != "succeeded"
            || string.IsNullOrWhiteSpace(intent.ProviderPaymentId))
            throw new PaymentConflictException();
        await payments.MarkRefundPendingAsync(
            intent.AccountId, request, cancellationToken).ConfigureAwait(false);

        var credentials = Credentials();
        using var message = Request(
            HttpMethod.Post,
            "refunds",
            credentials,
            request.IdempotencyKey.ToString("D"));
        message.Content = JsonContent.Create(new
        {
            amount = new
            {
                value = FormatRub(intent.Amount.MinorUnits),
                currency = "RUB"
            },
            payment_id = intent.ProviderPaymentId
        });
        using var response = await clients.CreateClient("YooKassa")
            .SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return RequiredString(json.RootElement, "id");
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var candidates = await payments.ListReconcileCandidatesAsync(
            "yookassa", 100, cancellationToken).ConfigureAwait(false);
        var applied = 0;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.ProviderPaymentId))
                continue;
            try
            {
                var verified = await ReadVerifiedAsync(
                    candidate.ProviderPaymentId, cancellationToken).ConfigureAwait(false);
                await payments.ApplyAsync(
                    verified, cancellationToken).ConfigureAwait(false);
                applied++;
            }
            catch (HttpRequestException)
            {
                // A provider timeout/rate limit stays pending for the next scheduled pass.
            }
            catch (KeyNotFoundException)
            {
                // Unknown provider object stays pending/reconcile-required; no grant.
            }
        }
        return applied;
    }

    private HttpRequestMessage Request(
        HttpMethod method,
        string relative,
        (string ShopId, string Secret) credentials,
        string? idempotencyKey)
    {
        var message = new HttpRequestMessage(
            method, new Uri(ApiBase, relative));
        var bytes = Encoding.UTF8.GetBytes(
            credentials.ShopId + ":" + credentials.Secret);
        try
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(bytes));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            message.Headers.TryAddWithoutValidation(
                "Idempotence-Key", idempotencyKey);
        message.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    private (string ShopId, string Secret) Credentials()
    {
        var shop = configuration["VG_YOOKASSA_SHOP_ID"];
        var secret = configuration["VG_YOOKASSA_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(shop)
            || string.IsNullOrWhiteSpace(secret))
            throw new PaymentDisabledException();
        return (shop, secret);
    }

    private Uri ReturnUri()
    {
        var raw = configuration["VG_YOOKASSA_RETURN_URL"];
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new PaymentDisabledException();
        return uri;
    }

    private void ValidateTestEnvironment(JsonElement root)
    {
        var isTest = root.TryGetProperty("test", out var test)
            && test.ValueKind == JsonValueKind.True;
        if (payments.Environment == "test" && !isTest)
            throw new PaymentConflictException();
        if (payments.Environment == "live" && isTest)
            throw new PaymentConflictException();
    }

    private static void ValidateAmount(
        JsonElement root,
        CatalogPrice expected)
    {
        if (!root.TryGetProperty("amount", out var amount)
            || amount.ValueKind != JsonValueKind.Object)
            throw new PaymentConflictException();
        var value = RequiredString(amount, "value");
        var currency = RequiredString(amount, "currency");
        var money = currency == "RUB"
            ? ParseRub(value)
            : throw new PaymentConflictException();
        if (expected.Currency != "RUB"
            || money.MinorUnits != expected.MinorUnits)
            throw new PaymentConflictException();
    }

    private static string NormalizeStatus(JsonElement root)
    {
        var status = RequiredString(root, "status");
        return status switch
        {
            "pending" or "waiting_for_capture" => "pending",
            "succeeded" => "succeeded",
            "canceled" => "canceled",
            _ => throw new InvalidDataException(
                "Unsupported YooKassa payment status.")
        };
    }

    private static void ValidateProviderId(string providerPaymentId)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId)
            || providerPaymentId.Length > 128
            || providerPaymentId.Any(
                ch => !char.IsAsciiLetterOrDigit(ch)
                      && ch is not '-' and not '_'))
            throw new ArgumentException("YooKassa payment ID is invalid.");
    }

    private static string RequiredString(
        JsonElement element,
        string name)
        => element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
           && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException(
                "YooKassa field " + name + " is invalid.");
}
