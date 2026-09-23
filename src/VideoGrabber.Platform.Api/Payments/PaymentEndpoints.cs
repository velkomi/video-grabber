using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/payment-products", ProductsAsync).RequireAuthorization();
        endpoints.MapPost("/v1/payments", CreateAsync).RequireAuthorization();
        endpoints.MapGet("/v1/payments/{paymentId:guid}", ReadAsync).RequireAuthorization();
        endpoints.MapPost(
                "/v1/admin/payments/{paymentId:guid}/stars-refund",
                RefundStarsAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static IResult ProductsAsync(
        string? surface,
        PaymentStore payments)
    {
        try
        {
            var telegram = string.Equals(surface, "telegram", StringComparison.OrdinalIgnoreCase);
            var products = payments.Catalog.Products.Values
                .Select(product => new
                {
                    sku = product.Sku,
                    kind = product.Kind,
                    planId = product.PlanId,
                    credits = product.Credits,
                    days = product.Days,
                    recurringAllowed = product.RecurringAllowed,
                    prices = telegram
                        ? product.Prices
                            .Where(x => x.Key == "stars")
                            .ToDictionary(x => x.Key, x => x.Value)
                        : product.Prices
                })
                .Where(x => x.prices.Count > 0)
                .ToArray();
            return Results.Ok(new
            {
                version = payments.Catalog.Version,
                environment = payments.Catalog.Environment,
                products
            });
        }
        catch (PaymentDisabledException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
    private static async Task<IResult> CreateAsync(
        PurchaseRequest request,
        HttpContext http,
        PaymentStore payments,
        StarsPaymentAdapter stars,
        YooKassaPaymentAdapter yookassa,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            var checkout = request.Provider switch
            {
                "stars" => await stars.CreateAsync(
                    accountId, request, cancellationToken),
                "yookassa" => await yookassa.CreateAsync(
                    accountId, request, cancellationToken),
                _ => throw new KeyNotFoundException(
                    "Payment provider is unavailable.")
            };
            return Results.Ok(checkout);
        }
        catch (PaymentDisabledException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (PaymentConflictException)
        { return Results.Conflict(new { code = "payment_idempotency_conflict" }); }
        catch (KeyNotFoundException)
        { return Results.BadRequest(new { code = "payment_product_unavailable" }); }
        catch (UnauthorizedAccessException)
        { return Results.BadRequest(new { code = "telegram_identity_required" }); }
        catch (InvalidOperationException ex)
        { return Results.BadRequest(new { code = ex.Message }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_payment", detail = ex.Message }); }
    }

    private static async Task<IResult> ReadAsync(
        Guid paymentId,
        HttpContext http,
        PaymentStore payments,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            var payment = await payments.ReadAsync(
                accountId, paymentId, cancellationToken);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        }
        catch (PaymentDisabledException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    private static async Task<IResult> RefundStarsAsync(
        Guid paymentId,
        RefundRequest request,
        HttpContext http,
        AdminService admin,
        StarsPaymentAdapter stars,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        if (request.PaymentId != paymentId)
            return Results.BadRequest(new { code = "payment_id_mismatch" });
        if (!await admin.IsAdminAsync(actorId, cancellationToken))
            return Results.Forbid();
        if (!HasFreshMfa(http, clock))
            return Results.Unauthorized();
        try
        {
            var state = await stars.RefundAsync(
                request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { state });
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (PaymentConflictException)
        { return Results.Conflict(new { code = "payment_not_refundable" }); }
    }

    private static bool HasFreshMfa(HttpContext http, TimeProvider clock)
    {
        var raw = http.User.FindFirst("mfa_at")?.Value;
        if (!long.TryParse(raw, out var seconds)) return false;
        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
        return age >= TimeSpan.FromSeconds(-30)
            && age <= TimeSpan.FromMinutes(5);
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
