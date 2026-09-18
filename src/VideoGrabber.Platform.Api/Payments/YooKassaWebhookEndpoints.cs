using System.Text.Json;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public static class YooKassaWebhookEndpoints
{
    public static IEndpointRouteBuilder MapYooKassaWebhookEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/v1/payments/yookassa/webhook",
            HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        JsonElement notification,
        YooKassaPaymentAdapter adapter,
        PaymentStore payments,
        CancellationToken cancellationToken)
    {
        if (notification.ValueKind != JsonValueKind.Object
            || !notification.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String
            || type.GetString() != "notification"
            || !notification.TryGetProperty("object", out var providerObject)
            || providerObject.ValueKind != JsonValueKind.Object
            || !providerObject.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()))
            return Results.BadRequest(new { code = "invalid_yookassa_notification" });

        var providerId = id.GetString()!;
        try
        {
            var verified = await adapter.ReadVerifiedAsync(
                providerId, cancellationToken).ConfigureAwait(false);
            await payments.ApplyAsync(
                verified, cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests
                or System.Net.HttpStatusCode.ServiceUnavailable
                or System.Net.HttpStatusCode.GatewayTimeout)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (KeyNotFoundException)
        {
            // Do not grant from an unknown object. A provider retry or scheduled
            // reconciliation can resolve it later.
            return Results.Ok();
        }
        catch (PaymentConflictException)
        {
            return Results.BadRequest(new { code = "yookassa_verification_conflict" });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { code = "invalid_yookassa_object_id" });
        }
    }
}
