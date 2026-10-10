using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Jobs;

public static class CloudDestinationEndpoints
{
    public static IEndpointRouteBuilder MapCloudDestinationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/cloud/config", async (HttpContext http, IConfiguration configuration,
            IAccountStore accounts, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId))
                return Results.Unauthorized();
            var profile = await accounts.ReadAsync(accountId, cancellationToken);
            if (profile is null) return Results.Unauthorized();
            if (profile.Blocked) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var enabled = bool.TryParse(configuration["VG_CLOUD_GOOGLE_ENABLED"], out var value) && value;
            var ownerOnly = !bool.TryParse(configuration["VG_CLOUD_GOOGLE_OWNER_ONLY"], out var restricted) || restricted;
            var clientId = configuration["VG_CLOUD_GOOGLE_CLIENT_ID"] ?? "";
            enabled &= !ownerOnly || profile.Role == "owner_admin";
            enabled &= System.Text.RegularExpressions.Regex.IsMatch(clientId,
                @"\A[0-9]+-[a-z0-9]+\.apps\.googleusercontent\.com\z");
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { google = new { enabled, clientId = enabled ? clientId : null } });
        }).RequireAuthorization().RequireRateLimiting("auth-config");
        return endpoints;
    }
}
