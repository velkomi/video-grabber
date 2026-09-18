using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public static class AttemptEndpoints
{
    private const string WorkerHeader = "X-VideoGrabber-Worker-Token";

    public static IEndpointRouteBuilder MapAttemptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/worker/jobs/claim", ClaimAsync);
        endpoints.MapPost("/v1/worker/jobs/heartbeat", HeartbeatAsync);
        endpoints.MapPost("/v1/worker/jobs/complete", CompleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ClaimAsync(
        WorkerClaim request, HttpContext http, IConfiguration configuration,
        JobStore jobs, CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration)) return Results.Unauthorized();
        try
        {
            var lease = await jobs.ClaimAsync(
                request.WorkerId, null, null, cancellationToken);
            return lease is null ? Results.NoContent() : Results.Ok(lease);
        }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_worker_claim", detail = ex.Message }); }
    }

    private static async Task<IResult> HeartbeatAsync(
        AttemptLease lease, HttpContext http, IConfiguration configuration,
        JobStore jobs, CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration)) return Results.Unauthorized();
        return await jobs.HeartbeatAsync(lease, cancellationToken)
            ? Results.NoContent()
            : Results.Conflict(new { code = "attempt_lease_lost" });
    }

    private static async Task<IResult> CompleteAsync(
        AttemptCompletion completion, HttpContext http, IConfiguration configuration,
        JobStore jobs, CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration)) return Results.Unauthorized();
        try { return Results.Ok(await jobs.CompleteAsync(completion, cancellationToken)); }
        catch (JobFenceConflictException)
        { return Results.Conflict(new { code = "attempt_fence_stale" }); }
        catch (ReservationConflictException)
        { return Results.Conflict(new { code = "reservation_conflict" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_completion", detail = ex.Message }); }
    }

    private static bool Authorized(HttpContext http, IConfiguration configuration)
    {
        var expected = configuration["VG_SERVER_WORKER_TOKEN"];
        var actual = http.Request.Headers[WorkerHeader].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        try
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(left, right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }
}