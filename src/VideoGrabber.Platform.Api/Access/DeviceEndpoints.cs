using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Access;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/devices", RegisterAsync).RequireAuthorization();
        endpoints.MapGet("/v1/devices", ListAsync).RequireAuthorization();
        endpoints.MapPost("/v1/devices/{deviceId:guid}/revoke", RevokeAsync).RequireAuthorization();
        endpoints.MapPost("/v1/devices/{deviceId:guid}/challenge", ChallengeAsync).RequireAuthorization();
        endpoints.MapPost("/v1/devices/{deviceId:guid}/lease", LeaseAsync).RequireAuthorization();
        endpoints.MapGet("/v1/lease-keys", Keys);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        DeviceRegistration request, HttpContext http, DeviceStore store, CancellationToken ct)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await store.RegisterAsync(accountId, request, ct)); }
        catch (DeviceLimitException) { return Results.Conflict(new { code = "device_limit_reached" }); }
        catch (DeviceConflictException) { return Results.Conflict(new { code = "device_idempotency_conflict" }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { code = "invalid_device", detail = ex.Message }); }
    }

    private static async Task<IResult> ListAsync(
        HttpContext http, DeviceStore store, CancellationToken ct)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await store.ListAsync(accountId, ct));
    }
    private static async Task<IResult> RevokeAsync(
        Guid deviceId, HttpContext http, DeviceStore store, CancellationToken ct)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { await store.RevokeAsync(accountId, deviceId, ct); return Results.NoContent(); }
        catch (DeviceNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> ChallengeAsync(
        Guid deviceId, HttpContext http, DeviceStore store, CancellationToken ct)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await store.CreateChallengeAsync(accountId, deviceId, ct)); }
        catch (DeviceNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> LeaseAsync(
        Guid deviceId, DeviceLeaseProof proof, HttpContext http,
        DeviceStore store, OfflineLeaseService leases, CancellationToken ct)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            await store.ConsumeProofAsync(accountId, deviceId, proof, ct);
            return Results.Ok(await leases.IssueAsync(accountId, deviceId, ct));
        }
        catch (DeviceProofException) { return Results.Unauthorized(); }
        catch (DeviceNotFoundException) { return Results.NotFound(); }
        catch (TimeAccessRequiredException)
        {
            return Results.Json(new { code = "time_access_required" }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException) { return Results.BadRequest(new { code = "invalid_device_proof" }); }
    }

    private static IResult Keys(OfflineLeaseService leases) => Results.Ok(leases.PublicKeys());

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}