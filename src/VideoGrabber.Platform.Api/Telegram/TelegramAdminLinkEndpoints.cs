using VideoGrabber.Platform.Api.Admin;

namespace VideoGrabber.Platform.Api.Telegram;

public static class TelegramAdminLinkEndpoints
{
    public static IEndpointRouteBuilder MapTelegramAdminLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/telegram/admin-links/{token}", OpenAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> OpenAsync(
        string token,
        HttpContext http,
        BotCallbackStore callbacks,
        AdminService admin,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId))
            return Results.Unauthorized();
        if (!HasFreshMfa(http, clock))
            return Results.Unauthorized();
        if (!await admin.IsAdminAsync(accountId, cancellationToken))
            return Results.Forbid();
        var grant = await callbacks.ConsumeAsync(accountId, token, cancellationToken);
        if (grant is null || !string.Equals(grant.Action, "admin:console", StringComparison.Ordinal))
            return Results.NotFound();
        return Results.Redirect("/admin/");
    }

    private static bool HasFreshMfa(HttpContext http, TimeProvider clock)
    {
        var raw = http.User.FindFirst("mfa_at")?.Value;
        if (!long.TryParse(raw, out var seconds)) return false;
        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
        return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(5);
    }
}