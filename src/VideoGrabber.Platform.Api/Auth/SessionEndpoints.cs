using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/auth/supabase-config", SupabaseConfigAsync)
            .RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/supabase-session", SupabaseSessionAsync)
            .RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/desktop/start", DesktopStartAsync)
            .RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/desktop/approve", DesktopApproveAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/desktop/consume", DesktopConsumeAsync)
            .RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/start", StartAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/complete", CompleteAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/refresh", RefreshAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/v1/auth/logout", LogoutAsync).RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> SupabaseConfigAsync(
        SupabaseAuthService supabase,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await supabase.ReadBrowserConfigAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Supabase auth is not configured.",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> SupabaseSessionAsync(
        SupabaseSessionRequest request,
        SupabaseAuthService supabase,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await supabase.ExchangeAsync(
                request.AccessToken,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (HttpRequestException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> DesktopStartAsync(
        DesktopSignInStartRequest request,
        DesktopAuthHandoffStore handoffs,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await handoffs.StartAsync(
                request.ReturnUri,
                cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new
            {
                code = "invalid_desktop_return",
                detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DesktopApproveAsync(
        DesktopSignInApprovalRequest request,
        HttpContext http,
        DesktopAuthHandoffStore handoffs,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                http.User.FindFirst("account_id")?.Value,
                out var accountId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(await handoffs.ApproveAsync(
                accountId,
                request,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new
            {
                code = "invalid_desktop_approval",
                detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DesktopConsumeAsync(
        DesktopSignInConsumeRequest request,
        DesktopAuthHandoffStore handoffs,
        SessionStore sessions,
        CancellationToken cancellationToken)
    {
        try
        {
            var accountId = await handoffs.ConsumeAsync(
                request,
                cancellationToken);
            return Results.Ok(await sessions.IssueAsync(
                accountId,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
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
