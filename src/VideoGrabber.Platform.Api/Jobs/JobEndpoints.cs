using VideoGrabber.Platform.Api.Operations;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/jobs", CreateAsync).RequireAuthorization();
        endpoints.MapGet("/v1/jobs", ListAsync).RequireAuthorization();
        endpoints.MapGet("/v1/jobs/{jobId:guid}", ReadAsync).RequireAuthorization();
        endpoints.MapPost("/v1/jobs/{jobId:guid}/download-link", CreateDownloadLinkAsync)
            .RequireAuthorization();
        endpoints.MapGet("/v1/downloads/{ticket}", DownloadTicketAsync)
            .AllowAnonymous();
        endpoints.MapPost("/v1/jobs/{jobId:guid}/cancel", CancelAsync).RequireAuthorization();
        endpoints.MapPost("/v1/jobs/{jobId:guid}/retry", RetryAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateJob request, HttpContext http, JobStore jobs, DeviceStore devices,
        IAccountStore accounts,
        PlatformOperationalCounters counters,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            var profile = await accounts.ReadAsync(accountId, cancellationToken);
            if (profile is null) return Results.Unauthorized();
            if (!AccountEligibility.CanUseProtectedDownloads(profile))
            {
                counters.RecordBlockedAdmission();
                return Results.Json(
                    new { code = "primary_account_link_required" },
                    statusCode: StatusCodes.Status403Forbidden);
            }
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
        {
            counters.RecordBlockedAdmission();
            return Results.Conflict(new { code = "access_unavailable" });
        }
        catch (LedgerBusyException)
        {
            counters.RecordDependencyFailure();
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
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

    private static async Task<IResult> CreateDownloadLinkAsync(
        Guid jobId,
        HttpContext http,
        JobStore jobs,
        BrowserDownloadTicketService tickets,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        var artifact = await jobs.ReadCompletedArtifactAsync(
            accountId, jobId, cancellationToken);
        if (artifact is null) return Results.NotFound();

        return Results.Ok(new
        {
            url = "/v1/downloads/" + tickets.Create(accountId, jobId),
            expiresInSeconds = 300
        });
    }

    private static async Task<IResult> DownloadTicketAsync(
        string ticket,
        HttpContext http,
        JobStore jobs,
        BrowserDownloadTicketService tickets,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!tickets.TryValidate(ticket, out var accountId, out var jobId))
            return Results.NotFound();
        return await DownloadArtifactAsync(
            accountId, jobId, http, jobs, configuration, cancellationToken);
    }

    private static async Task<IResult> DownloadArtifactAsync(
        Guid accountId,
        Guid jobId,
        HttpContext http,
        JobStore jobs,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var artifact = await jobs.ReadCompletedArtifactAsync(
            accountId, jobId, cancellationToken);
        if (artifact is null) return Results.NotFound();

        var retentionRoot = configuration["VG_RETENTION_ROOT"];
        if (!TrySafeArtifactPath(retentionRoot, artifact.StoragePath, out var path)
            || !File.Exists(path))
            return Results.NotFound();

        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return Results.NotFound();
            var info = new FileInfo(path);
            if (!string.IsNullOrEmpty(info.LinkTarget))
                return Results.NotFound();
        }
        catch (IOException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var extension = Path.GetExtension(path);
        var fileName = "VideoGrabber-" + jobId.ToString("N")[..10]
            + (string.IsNullOrWhiteSpace(extension) ? ".bin" : extension);
        return Results.File(
            path,
            artifact.MediaType,
            fileDownloadName: fileName,
            enableRangeProcessing: true);
    }

    private static bool TrySafeArtifactPath(
        string? configuredRoot,
        string path,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredRoot)
            || string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path))
            return false;
        try
        {
            var root = Path.GetFullPath(configuredRoot);
            fullPath = Path.GetFullPath(path);
            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return fullPath.StartsWith(prefix, comparison)
                && !string.Equals(fullPath, root, comparison);
        }
        catch
        {
            fullPath = string.Empty;
            return false;
        }
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