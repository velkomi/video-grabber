namespace VideoGrabber.Platform.Api.Telegram;

public sealed record CompleteTelegramAccountLinkRequest(string Token);

public static class TelegramAccountLinkEndpoints
{
    public static IEndpointRouteBuilder MapTelegramAccountLinkEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/v1/telegram/account-link/complete",
                CompleteAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> CompleteAsync(
        CompleteTelegramAccountLinkRequest request,
        HttpContext http,
        TelegramAccountLinkService links,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                http.User.FindFirst("account_id")?.Value,
                out var accountId))
            return Results.Unauthorized();

        try
        {
            await links.CompleteAsync(
                accountId,
                request.Token,
                cancellationToken);
            return Results.NoContent();
        }
        catch (TelegramAccountLinkUnavailableException)
        {
            return Results.NotFound();
        }
        catch (TelegramAccountLinkConflictException)
        {
            return Results.Conflict(new
            {
                code = "telegram_link_conflict"
            });
        }
        catch (TelegramAccountLinkReconciliationRequiredException)
        {
            return Results.Conflict(new
            {
                code = "telegram_link_reconciliation_required"
            });
        }
    }
}
