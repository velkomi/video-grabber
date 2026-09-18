using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Jobs;

public static class RetentionEndpoints
{
    private const string WorkerHeader = "X-VideoGrabber-Worker-Token";

    public static IEndpointRouteBuilder MapRetentionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/worker/retention/claim", ClaimAsync);
        endpoints.MapPost("/v1/worker/retention/ack", AckAsync);
        endpoints.MapGet("/v1/admin/retention/dry-run", DryRunAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ClaimAsync(
        HttpContext http,
        IConfiguration configuration,
        ArtifactRetentionService retention,
        CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration)) return Results.Unauthorized();
        var candidate = await retention.ClaimExpiredAsync(cancellationToken);
        return candidate is null ? Results.NoContent() : Results.Ok(candidate);
    }

    private static async Task<IResult> AckAsync(
        RetentionCleanupResult result,
        HttpContext http,
        IConfiguration configuration,
        ArtifactRetentionService retention,
        CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration)) return Results.Unauthorized();
        await retention.AcknowledgeAsync(result, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DryRunAsync(
        HttpContext http,
        ArtifactRetentionService retention,
        CancellationToken cancellationToken)
    {
        var role = http.User.FindFirst("role")?.Value;
        if (!string.Equals(role, "owner_admin", StringComparison.Ordinal))
            return Results.Forbid();
        return Results.Ok(await retention.DryRunAsync(cancellationToken));
    }

    private static bool Authorized(HttpContext http, IConfiguration configuration)
    {
        var expected = configuration["VG_SERVER_WORKER_TOKEN"];
        var actual = http.Request.Headers[WorkerHeader].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;
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
