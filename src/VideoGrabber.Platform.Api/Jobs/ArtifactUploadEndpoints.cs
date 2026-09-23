using System.Net;
using System.Net.Http.Headers;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public static class ArtifactUploadEndpoints
{
    public static IEndpointRouteBuilder MapArtifactUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/v1/desktop-worker/devices/{deviceId:guid}/claim", ClaimAsync)
            .RequireAuthorization();
        endpoints.MapPost(
            "/v1/desktop-worker/devices/{deviceId:guid}/heartbeat", HeartbeatAsync)
            .RequireAuthorization();
        endpoints.MapGet(
            "/v1/desktop-worker/devices/{deviceId:guid}/sources/{sourceId}", ResolveSourceAsync)
            .RequireAuthorization();
        endpoints.MapPost(
            "/v1/desktop-worker/devices/{deviceId:guid}/uploads", CreateUploadAsync)
            .RequireAuthorization();
        endpoints.MapPut(
            "/v1/desktop-worker/devices/{deviceId:guid}/uploads/{uploadId:guid}", UploadAsync)
            .RequireAuthorization();
        endpoints.MapPost(
            "/v1/desktop-worker/devices/{deviceId:guid}/complete", CompleteAsync)
            .RequireAuthorization();
        endpoints.MapPost(
            "/v1/desktop-worker/devices/{deviceId:guid}/complete-local", CompleteLocalAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ClaimAsync(
        Guid deviceId,
        DeviceLeaseProof proof,
        HttpContext http,
        DeviceStore devices,
        JobStore jobs,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            await devices.ConsumeProofAsync(
                accountId, deviceId, proof, cancellationToken);
            var lease = await jobs.ClaimAsync(
                deviceId, accountId, deviceId, cancellationToken);
            return lease is null ? Results.NoContent() : Results.Ok(lease);
        }
        catch (DeviceProofException) { return Results.Unauthorized(); }
        catch (DeviceNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> HeartbeatAsync(
        Guid deviceId,
        AttemptLease lease,
        HttpContext http,
        DeviceStore devices,
        JobStore jobs,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        if (!await jobs.ValidateDesktopAttemptAsync(
                accountId, deviceId, lease, cancellationToken))
            return Results.NotFound();
        return await jobs.HeartbeatAsync(lease, cancellationToken)
            ? Results.NoContent()
            : Results.Conflict(new { code = "attempt_lease_lost" });
    }

    private static async Task<IResult> ResolveSourceAsync(
        Guid deviceId,
        string sourceId,
        string quality,
        HttpContext http,
        DeviceStore devices,
        SourceAnalysisService sources,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        try
        {
            var source = await sources.ResolveForDesktopAsync(
                accountId, sourceId, quality, cancellationToken);
            return source is null ? Results.NotFound() : Results.Ok(source);
        }
        catch (UnauthorizedAccessException)
        { return Results.StatusCode(StatusCodes.Status403Forbidden); }
    }

    private static async Task<IResult> CompleteLocalAsync(
        Guid deviceId,
        DesktopLocalCompletionRequest request,
        HttpContext http,
        DeviceStore devices,
        JobStore jobs,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        if (!await jobs.ValidateDesktopAttemptAsync(
                accountId, deviceId, request.Lease, cancellationToken))
            return Results.NotFound();

        try
        {
            if (request.Outcome == "success")
            {
                return Results.Ok(await jobs.CompleteLocalAsync(
                    accountId, deviceId, request.Lease,
                    request.EvidenceId, cancellationToken));
            }

            if (request.Outcome is not ("failed" or "review_required" or "cancelled"))
                return Results.BadRequest(new { code = "invalid_completion_outcome" });

            return Results.Ok(await jobs.CompleteAsync(
                new AttemptCompletion(
                    request.Lease.JobId,
                    request.Lease.AttemptId,
                    request.Lease.Fence,
                    request.Outcome,
                    null,
                    request.EvidenceId),
                cancellationToken));
        }
        catch (JobFenceConflictException)
        { return Results.Conflict(new { code = "attempt_fence_stale" }); }
        catch (ReservationConflictException)
        { return Results.Conflict(new { code = "reservation_conflict" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_completion", detail = ex.Message }); }
    }

    private static async Task<IResult> CreateUploadAsync(
        Guid deviceId,
        UploadTicketRequest request,
        HttpContext http,
        DeviceStore devices,
        ArtifactUploadService uploads,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        try
        {
            return Results.Ok(await uploads.CreateTicketAsync(
                accountId, deviceId, request, cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Results.NotFound(); }
        catch (InvalidDataException ex)
        { return Results.BadRequest(new { code = ex.Message }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_upload", detail = ex.Message }); }
    }

    private static async Task<IResult> UploadAsync(
        Guid deviceId,
        Guid uploadId,
        HttpContext http,
        DeviceStore devices,
        ArtifactUploadService uploads,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        if (http.Request.ContentLength is not long length || length <= 0)
            return Results.BadRequest(new { code = "content_length_required" });
        try
        {
            return Results.Ok(await uploads.UploadAsync(
                accountId, deviceId, uploadId,
                http.Request.Body, length, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InvalidDataException ex)
        { return Results.BadRequest(new { code = ex.Message }); }
    }

    private static async Task<IResult> CompleteAsync(
        Guid deviceId,
        DesktopCompletionRequest request,
        HttpContext http,
        DeviceStore devices,
        JobStore jobs,
        ArtifactUploadService uploads,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        if (!await devices.IsActiveAsync(accountId, deviceId, cancellationToken))
            return Results.NotFound();
        if (!await jobs.ValidateDesktopAttemptAsync(
                accountId, deviceId, request.Lease, cancellationToken))
            return Results.NotFound();
        try
        {
            var artifact = await uploads.ValidateReceiptAsync(
                accountId, deviceId, request.Lease, request.Artifact, cancellationToken);
            return Results.Ok(await jobs.CompleteAsync(
                new AttemptCompletion(
                    request.Lease.JobId,
                    request.Lease.AttemptId,
                    request.Lease.Fence,
                    "success",
                    artifact,
                    artifact.VerificationEvidenceId),
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (UnauthorizedAccessException) { return Results.NotFound(); }
        catch (JobFenceConflictException)
        { return Results.Conflict(new { code = "attempt_fence_stale" }); }
        catch (ReservationConflictException)
        { return Results.Conflict(new { code = "reservation_conflict" }); }
    }
    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
