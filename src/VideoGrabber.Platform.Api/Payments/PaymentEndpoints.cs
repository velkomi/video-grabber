using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/payments", CreateAsync).RequireAuthorization();
        endpoints.MapGet("/v1/payments/{paymentId:guid}", ReadAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        PurchaseRequest request,
        HttpContext http,
        PaymentStore payments,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            return Results.Ok(await payments.BeginAsync(
                accountId, request, cancellationToken));
        }
        catch (PaymentDisabledException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (PaymentConflictException)
        { return Results.Conflict(new { code = "payment_idempotency_conflict" }); }
        catch (KeyNotFoundException)
        { return Results.BadRequest(new { code = "payment_product_unavailable" }); }
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

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
