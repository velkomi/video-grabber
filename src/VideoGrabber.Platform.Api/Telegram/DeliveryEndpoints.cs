using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/deliveries", RequestAsync).RequireAuthorization();
        endpoints.MapGet("/v1/deliveries", ListAsync).RequireAuthorization();
        endpoints.MapGet("/v1/deliveries/{deliveryId:guid}", ReadAsync).RequireAuthorization();
        endpoints.MapPost("/v1/deliveries/{deliveryId:guid}/retry-unknown", RetryUnknownAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RequestAsync(
        DeliveryRequest request,
        HttpContext http,
        ArtifactDeliveryService deliveries,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            return Results.Ok(await deliveries.RequestAsync(
                accountId, request, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (DeliveryConflictException)
        { return Results.Conflict(new { code = "delivery_conflict" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_delivery", detail = ex.Message }); }
    }

    private static async Task<IResult> RetryUnknownAsync(
        Guid deliveryId,
        DeliveryRetry request,
        HttpContext http,
        ArtifactDeliveryService deliveries,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            return Results.Ok(await deliveries.RetryUnknownAsync(
                accountId, deliveryId, request.AcknowledgedWarning, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (DeliveryConflictException)
        { return Results.Conflict(new { code = "delivery_not_retryable" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = ex.Message }); }
    }

    private static async Task<IResult> ReadAsync(
        Guid deliveryId,
        HttpContext http,
        ArtifactDeliveryService deliveries,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        var delivery = await deliveries.ReadAsync(
            accountId, deliveryId, cancellationToken);
        return delivery is null ? Results.NotFound() : Results.Ok(delivery);
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        ArtifactDeliveryService deliveries,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await deliveries.ListAsync(accountId, cancellationToken));
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
