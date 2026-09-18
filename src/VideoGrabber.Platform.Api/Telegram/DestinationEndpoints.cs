using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public static class DestinationEndpoints
{
    public static IEndpointRouteBuilder MapDestinationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/destinations", ListAsync).RequireAuthorization();
        endpoints.MapPost("/v1/destinations/challenges", BeginAsync).RequireAuthorization();
        endpoints.MapPost("/v1/destinations", LinkAsync).RequireAuthorization();
        endpoints.MapPost("/v1/destinations/{destinationId:guid}/revoke", RevokeAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        DestinationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await service.ListAsync(accountId, cancellationToken));
    }

    private static async Task<IResult> BeginAsync(
        DestinationChallengeRequest request,
        HttpContext http,
        DestinationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await service.BeginAsync(accountId, request.ChatId, cancellationToken)); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_destination", detail = ex.Message }); }
    }

    private static async Task<IResult> LinkAsync(
        DestinationRequest request,
        HttpContext http,
        DestinationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try { return Results.Ok(await service.LinkAsync(accountId, request, cancellationToken)); }
        catch (DestinationChallengeConflictException)
        { return Results.Conflict(new { code = "destination_challenge_unavailable" }); }
        catch (UnauthorizedAccessException)
        { return Results.Forbid(); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_destination", detail = ex.Message }); }
    }

    private static async Task<IResult> RevokeAsync(
        Guid destinationId,
        HttpContext http,
        DestinationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            await service.RevokeAsync(accountId, destinationId, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}