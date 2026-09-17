using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Access;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/reservations", ReserveAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReserveAsync(
        ReservationRequest request,
        HttpContext http,
        CreditLedger ledger,
        DeviceStore devices,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId))
            return Results.Unauthorized();
        try
        {
            if (request.Executor == "desktop_worker" &&
                (request.DeviceId is not Guid deviceId ||
                 !await devices.IsActiveAsync(accountId, deviceId, cancellationToken)))
                return Results.Conflict(new { code = "registered_device_required" });
            return Results.Ok(await ledger.ReserveAsync(accountId, request, cancellationToken));
        }
        catch (ReservationConflictException)
        {
            return Results.Conflict(new { code = "reservation_conflict" });
        }
        catch (ReservationUnavailableException)
        {
            return Results.Conflict(new { code = "access_unavailable" });
        }
        catch (LedgerBusyException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { code = "invalid_reservation", detail = exception.Message });
        }
    }
}
