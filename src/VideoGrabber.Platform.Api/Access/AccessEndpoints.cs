using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Access;

public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/access", ReadAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/grants", GiftAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> ReadAsync(
        HttpContext http,
        GrantStore grants,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await grants.EvaluateAsync(accountId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
    private static async Task<IResult> GiftAsync(
        GrantRequest request,
        HttpContext http,
        GrantStore grants,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var adminId)) return Results.Unauthorized();
        if (!HasFreshMfa(http, clock))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return Results.Ok(await grants.GiftAsync(adminId, request, cancellationToken));
        }
        catch (GrantConflictException)
        {
            return Results.Conflict(new { code = "idempotency_conflict" });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { code = "invalid_grant", detail = exception.Message });
        }
    }
    private static bool HasFreshMfa(HttpContext http, TimeProvider clock)
    {
        var raw = http.User.FindFirst("mfa_at")?.Value;
        if (!long.TryParse(raw, out var seconds)) return false;
        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
        return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(5);
    }

    private static bool TryGetAccountId(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
