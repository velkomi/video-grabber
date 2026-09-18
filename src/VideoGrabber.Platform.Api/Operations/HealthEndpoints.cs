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
                !string.IsNullOrWhiteSpace(configuration["VG_SERVER_WORKER_TOKEN"]);
            var authIssuerReady =
                !string.IsNullOrWhiteSpace(configuration["Security:SessionJwt:Issuer"])
                || !string.IsNullOrWhiteSpace(configuration["VG_SESSION_JWT_ISSUER"]);

            if (string.IsNullOrWhiteSpace(latest)
                || !workerTokenReady
                || !authIssuerReady)
            {
                return Results.Json(
                    new
                    {
                        status = "not_ready",
                        database = !string.IsNullOrWhiteSpace(latest),
                        workerCapability = workerTokenReady,
                        authIssuer = authIssuerReady
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                status = "ready",
                migration = latest,
                database = true,
                workerCapability = true,
                authIssuer = true
            });
        }
        catch (Exception ex) when (
            ex is NpgsqlException
            or InvalidOperationException)
        {
            return Results.Json(
                new
                {
                    status = "not_ready",
                    database = false
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
