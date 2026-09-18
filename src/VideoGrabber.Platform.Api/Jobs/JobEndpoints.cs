using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/jobs", CreateAsync).RequireAuthorization();
        endpoints.MapGet("/v1/jobs", ListAsync).RequireAuthorization();
        endpoints.MapGet("/v1/jobs/{jobId:guid}", ReadAsync).RequireAuthorization();
        endpoints.MapPost("/v1/jobs/{jobId:guid}/cancel", CancelAsync).RequireAuthorization();
        endpoints.MapPost("/v1/jobs/{jobId:guid}/retry", RetryAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateJob request, HttpContext http, JobStore jobs, DeviceStore devices,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            if (request.Executor == "desktop_worker"
                && (request.DeviceId is not Guid deviceId
                    || !await devices.IsActiveAsync(accountId, deviceId, cancellationToken)))
                return Results.Conflict(new { code = "registered_device_required" });
            return Results.Ok(await jobs.CreateAsync(accountId, request, cancellationToken));
        }
        catch (JobRequestConflictException)
        { return Results.Conflict(new { code = "job_request_conflict" }); }
        catch (JobUnavailableException)
        { return Results.Conflict(new { code = "job_source_unavailable" }); }
        catch (ReservationConflictException)
        { return Results.Conflict(new { code = "reservation_conflict" }); }
        catch (ReservationUnavailableException)
        { return Results.Conflict(new { code = "access_unavailable" }); }
        catch (LedgerBusyException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_job", detail = ex.Message }); }
    }

    private static async Task<IResult> ListAsync(
        HttpContext http, JobStore jobs, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await jobs.ListAsync(accountId, cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid jobId, HttpContext http, JobStore jobs, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        var job = await jobs.GetAsync(accountId, jobId, cancellationToken);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static Task<IResult> CancelAsync(
        Guid jobId, HttpContext http, JobStore jobs, CancellationToken cancellationToken)
        => MutateAsync(jobId, http, jobs.CancelAsync, cancellationToken);

    private static Task<IResult> RetryAsync(
        Guid jobId, HttpContext http, JobStore jobs, CancellationToken cancellationToken)
        => MutateAsync(jobId, http, jobs.RetryAsync, cancellationToken);

    private static async Task<IResult> MutateAsync(
        Guid jobId, HttpContext http,
        Func<Guid, Guid, CancellationToken, Task<JobView>> action,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await action(accountId, jobId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (JobRequestConflictException)
        { return Results.Conflict(new { code = "job_conflict" }); }
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}