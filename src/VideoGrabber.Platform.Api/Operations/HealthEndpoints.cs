using Npgsql;

namespace VideoGrabber.Platform.Api.Operations;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapPlatformHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new
        {
            status = "live"
        })).AllowAnonymous();

        endpoints.MapGet("/health/ready", ReadyAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> ReadyAsync(
        NpgsqlDataSource database,
        IConfiguration configuration,
        PlatformMetrics metrics,
        PlatformOperationalCounters counters,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection =
                await database.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                select coalesce(max(name),'')
                from vg_migrations.applied_migrations
                """,
                connection);
            var latest = (string?)await command.ExecuteScalarAsync(cancellationToken)
                ?? string.Empty;

            var workerTokenReady =
                !string.IsNullOrWhiteSpace(
                    configuration["VG_SERVER_WORKER_TOKEN"]);
            var authIssuerReady =
                !string.IsNullOrWhiteSpace(
                    configuration["Security:SessionJwt:Issuer"])
                || !string.IsNullOrWhiteSpace(
                    configuration["VG_SESSION_JWT_ISSUER"]);

            var snapshot = await metrics.CaptureAsync(cancellationToken);
            var workerReady =
                snapshot.ActiveJobs == 0
                || snapshot.WorkerHeartbeatAge is null
                || !PlatformMetrics.WorkerHeartbeatStale(
                    snapshot.WorkerHeartbeatAge.Value);
            var diskReady =
                !PlatformMetrics.DiskTooLow(snapshot.FreeDiskPercent);

            if (string.IsNullOrWhiteSpace(latest)
                || !workerTokenReady
                || !authIssuerReady
                || !workerReady
                || !diskReady)
            {
                return Results.Json(
                    new
                    {
                        status = "not_ready",
                        database = !string.IsNullOrWhiteSpace(latest),
                        workerCapability = workerTokenReady,
                        authIssuer = authIssuerReady,
                        workerHeartbeat = workerReady,
                        disk = diskReady
                    },
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                status = "ready",
                migration = latest,
                database = true,
                workerCapability = true,
                authIssuer = true,
                workerHeartbeat = true,
                disk = true
            });
        }
        catch (Exception ex) when (
            ex is NpgsqlException
            or InvalidOperationException
            or IOException)
        {
            counters.RecordDependencyFailure();
            return Results.Json(
                new
                {
                    status = "not_ready",
                    database = false
                },
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
        }
    }
}
