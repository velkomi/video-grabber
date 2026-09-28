using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;

namespace VideoGrabber.Platform.Core.Payments;

public sealed record CatalogPrice(long MinorUnits, string Currency);

public sealed record CatalogProduct(
    string Sku,
    string Kind,
    long Credits,
    int Days,
    bool RecurringAllowed,
    IReadOnlyDictionary<string, CatalogPrice> Prices)
{
    public string? PlanId { get; init; }
}

public sealed record PaymentCatalog(
    string Version,
    string Environment,
    IReadOnlyDictionary<string, CatalogProduct> Products)
{
    public static PaymentCatalog Load(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException("Payment catalog was not found.", full);
        using var document = JsonDocument.Parse(
            File.ReadAllText(full),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = 16
            });
        var root = document.RootElement;
        var version = RequiredString(root, "version", 64);
        var environment = RequiredString(root, "environment", 32);
        if (environment is not ("test" or "stage" or "live"))
            throw new InvalidDataException("Payment catalog environment is invalid.");
        if (!root.TryGetProperty("products", out var productsElement)
            || productsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Payment catalog products are missing.");

        var products = new Dictionary<string, CatalogProduct>(StringComparer.Ordinal);
        foreach (var item in productsElement.EnumerateArray())
        {
            var sku = RequiredString(item, "sku", 80);
            var kind = RequiredString(item, "kind", 24);
            if (kind is not ("credits" or "time"))
                throw new InvalidDataException("Unsupported payment product kind.");
            var credits = RequiredInt64(item, "credits");
            var days = checked((int)RequiredInt64(item, "days"));
            var recurringAllowed = item.TryGetProperty("recurringAllowed", out var recurring)
                && recurring.ValueKind == JsonValueKind.True;
            string? planId = null;
            if (item.TryGetProperty("planId", out var planElement))
            {
                if (planElement.ValueKind != JsonValueKind.String
                    || ProductPlans.Find(planElement.GetString()) is null)
                    throw new InvalidDataException("Payment product planId is invalid.");
                planId = planElement.GetString();
            }
            if (credits < 0 || days < 0
                || (kind == "credits" && credits <= 0)
                || (kind == "time" && days <= 0))
                throw new InvalidDataException("Payment product entitlement is invalid.");

            if (!item.TryGetProperty("prices", out var pricesElement)
                || pricesElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Payment product prices are missing.");
            var prices = new Dictionary<string, CatalogPrice>(StringComparer.Ordinal);
            foreach (var price in pricesElement.EnumerateObject())
            {
                var provider = price.Name;
                if (provider is not ("stars" or "yookassa"))
                    throw new InvalidDataException("Unsupported payment provider in catalog.");
                var minor = RequiredInt64(price.Value, "minorUnits");
                var currency = RequiredString(price.Value, "currency", 8);
                if (minor <= 0) throw new InvalidDataException("Payment amount must be positive.");
                if (!prices.TryAdd(provider, new CatalogPrice(minor, currency)))
                    throw new InvalidDataException("Duplicate payment provider price.");
            }

            if (!products.TryAdd(
                    sku,
                    new CatalogProduct(
                        sku, kind, credits, days, recurringAllowed, prices)
                    {
                        PlanId = planId
                    }))
                throw new InvalidDataException("Duplicate payment SKU.");
        }

        if (products.Count == 0)
            throw new InvalidDataException("Payment catalog is empty.");
        return new PaymentCatalog(version, environment, products);
    }

    public CatalogProduct RequireProduct(
        string sku,
        string provider,
        bool recurring)
    {
        if (!Products.TryGetValue(sku, out var product))
            throw new KeyNotFoundException("Unknown payment SKU.");
        if (!product.Prices.ContainsKey(provider))
            throw new KeyNotFoundException("Payment provider is unavailable for SKU.");
        if (recurring && !product.RecurringAllowed)
            throw new InvalidOperationException("Recurring purchase is unavailable for SKU.");
        return product;
    }

    private static string RequiredString(
        JsonElement element,
        string name,
        int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || value.GetString() is not { Length: > 0 } text
            || text.Length > maximumLength)
            throw new InvalidDataException($"Payment catalog field {name} is invalid.");
        return text;
    }

    private static long RequiredInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var result))
            throw new InvalidDataException($"Payment catalog field {name} is invalid.");
        return result;
    }
}

public static class PaymentProjection
{
    public static void ValidateMoney(Money money)
    {
        if (money.MinorUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(money));
        if (string.IsNullOrWhiteSpace(money.Currency)
            || money.Currency.Length is < 3 or > 8
            || money.Currency.Any(ch => !char.IsAsciiLetter(ch)))
            throw new ArgumentException("Currency is invalid.", nameof(money));
    }

    public static void ValidateVerified(
        VerifiedPayment payment,
        string expectedEnvironment,
        CatalogProduct product,
        CatalogPrice price)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ValidateMoney(payment.Amount);
        if (payment.PaymentId == Guid.Empty
            || payment.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
            throw new ArgumentException("Verified payment identity is incomplete.");
        if (!string.Equals(payment.Environment, expectedEnvironment, StringComparison.Ordinal))
            throw new InvalidOperationException("payment_environment_mismatch");
        if (!string.Equals(payment.Amount.Currency, price.Currency, StringComparison.Ordinal)
            || payment.Amount.MinorUnits != price.MinorUnits)
            throw new InvalidOperationException("payment_amount_mismatch");
        if (payment.Status is not ("pending" or "succeeded" or "refunded" or "canceled"))
            throw new InvalidOperationException("payment_status_invalid");
        if (payment.PaidThrough is not null && payment.PaidThrough <= payment.OccurredAt)
            throw new InvalidOperationException("payment_paid_through_invalid");
        _ = product;
    }

    public static string RequestHash(PurchaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("Payment idempotency key is required.");
        var canonical = string.Join(
            "\n",
            request.Sku,
            request.Provider,
            request.IdempotencyKey.ToString("D"),
            request.Recurring ? "1" : "0");
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
