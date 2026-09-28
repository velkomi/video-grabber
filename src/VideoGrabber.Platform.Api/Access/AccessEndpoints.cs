using VideoGrabber.Platform.Api.Admin;
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
        AdminFeatureOverrideService overrides,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId)) return Results.Unauthorized();
        try
        {
            var access = await grants.EvaluateAsync(accountId, cancellationToken);
            var features = await overrides.ReadEffectiveAsync(accountId, cancellationToken);
            if (features.TryGetValue("download", out var download))
                access = access with
                {
                    CanDownload = download,
                    Unlimited = download || access.Unlimited
                };
            if (features.TryGetValue("course_download", out var course))
                access = access with { CanDownloadCourse = course };
            access = access with { FeatureOverrides = features };
            return Results.Ok(access);
        }
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
