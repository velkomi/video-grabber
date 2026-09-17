using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/auth/start", StartAsync);
        endpoints.MapPost("/v1/auth/complete", CompleteAsync);
        endpoints.MapPost("/v1/auth/refresh", RefreshAsync);
        endpoints.MapPost("/v1/auth/logout", LogoutAsync);
        return endpoints;
    }

    private static async Task<IResult> StartAsync(
        BeginSignIn request,
        ProviderFlow flow,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await flow.BeginAsync(request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return Results.BadRequest(new { code = "unknown_provider" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                code = "invalid_auth_request",
                detail = exception.Message
            });
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshSession request, SessionStore sessions, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await sessions.RefreshAsync(request.RefreshToken, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    }

    private static async Task<IResult> LogoutAsync(
        RefreshSession request, SessionStore sessions, CancellationToken cancellationToken)
    {
        try { await sessions.LogoutAsync(request.RefreshToken, cancellationToken); return Results.NoContent(); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    }

    private static async Task<IResult> CompleteAsync(
        CompleteSignIn request,
        ProviderFlow flow,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await flow.CompleteAsync(request, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { code = "invalid_auth_request" });
        }
    }
}
