using System.IdentityModel.Tokens.Jwt;
using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Accounts;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/identities", ListAsync).RequireAuthorization();
        endpoints.MapPost("/v1/identities/link", BeginAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/identities/link/complete", CompleteAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/identities/link/browser/start", BeginBrowserAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/identities/link/browser/complete", CompleteBrowserAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapDelete("/v1/identities/{identityId:guid}", UnlinkAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/recovery/complete", CompleteRecoveryAsync).RequireRateLimiting("auth");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        IdentityLinkService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await service.ListAsync(accountId, cancellationToken));
    }

    private static async Task<IResult> BeginAsync(
        HttpContext http,
        IdentityLinkService service,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId) || !HasFreshSession(http, clock)) return Results.Unauthorized();
        return Results.Ok(await service.BeginAsync(accountId, cancellationToken));
    }

    private static async Task<IResult> BeginBrowserAsync(
        BeginIdentityLink request,
        HttpContext http,
        IdentityLinkService service,
        ProviderFlow flow,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId) || !HasFreshSession(http, clock)) return Results.Unauthorized();
        if (request.ChallengeId == Guid.Empty) return Results.BadRequest(new { code = "invalid_link_challenge" });
        try
        {
            await service.EnsureChallengeAvailableAsync(accountId, request.ChallengeId, cancellationToken);
            var signIn = new BeginSignIn(request.Provider, request.ReturnUri, request.ClientChallenge);
            var started = await flow.BeginBoundAsync(
                signIn, request.ChallengeId.ToString("D"), cancellationToken);
            return Results.Ok(started);
        }
        catch (LinkChallengeNotFoundException) { return Results.NotFound(); }
        catch (LinkChallengeConflictException) { return Results.Conflict(new { code = "link_challenge_unavailable" }); }
        catch (KeyNotFoundException) { return Results.BadRequest(new { code = "unknown_provider" }); }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { code = "invalid_auth_request", detail = ex.Message });
        }
    }

    private static async Task<IResult> CompleteBrowserAsync(
        CompleteIdentityLink request,
        HttpContext http,
        IdentityLinkService service,
        ProviderFlow flow,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId) || !HasFreshSession(http, clock)) return Results.Unauthorized();
        try
        {
            await service.EnsureChallengeAvailableAsync(accountId, request.ChallengeId, cancellationToken);
            var identity = await flow.ValidateCompletionAsync(
                new CompleteSignIn(request.FlowId, request.Code, request.State, request.ClientVerifier),
                cancellationToken);
            await service.CompleteVerifiedAsync(accountId, request.ChallengeId, identity, cancellationToken);
            return Results.NoContent();
        }
        catch (LinkChallengeNotFoundException) { return Results.NotFound(); }
        catch (LinkChallengeConflictException) { return Results.Conflict(new { code = "link_challenge_unavailable" }); }
        catch (IdentityLinkConflictException) { return Results.Conflict(new { code = "identity_already_linked" }); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (ArgumentException) { return Results.BadRequest(new { code = "invalid_auth_request" }); }
    }

    private static async Task<IResult> CompleteAsync(
        LinkProof proof,
        HttpContext http,
        IdentityLinkService service,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId) || !HasFreshSession(http, clock)) return Results.Unauthorized();
        try
        {
            await service.CompleteAsync(accountId, proof, cancellationToken);
            return Results.NoContent();
        }
        catch (LinkChallengeNotFoundException) { return Results.NotFound(); }
        catch (LinkChallengeConflictException) { return Results.Conflict(new { code = "link_challenge_unavailable" }); }
        catch (IdentityLinkConflictException) { return Results.Conflict(new { code = "identity_already_linked" }); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
    }

    private static async Task<IResult> UnlinkAsync(
        Guid identityId,
        HttpContext http,
        IdentityLinkService service,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId) || !HasFreshSession(http, clock)) return Results.Unauthorized();
        try
        {
            await service.UnlinkAsync(accountId, identityId, cancellationToken);
            return Results.NoContent();
        }
        catch (IdentityNotFoundException) { return Results.NotFound(); }
        catch (IdentityLinkConflictException) { return Results.Conflict(new { code = "last_identity_cannot_be_unlinked" }); }
    }

    private static bool HasFreshSession(HttpContext http, TimeProvider clock)
    {
        var raw = http.User.FindFirst(JwtRegisteredClaimNames.Nbf)?.Value;
        if (!long.TryParse(raw, out var seconds)) return false;
        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
        return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(5);
    }

    private static async Task<IResult> CompleteRecoveryAsync(
        LinkProof proof,
        IdentityLinkService service,
        CancellationToken cancellationToken)
    {
        try { await service.CompleteRecoveryAsync(proof, cancellationToken); return Results.NoContent(); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (IdentityLinkConflictException) { return Results.Conflict(new { code = "identity_already_owned" }); }
        catch (ArgumentException) { return Results.BadRequest(new { code = "invalid_recovery_request" }); }
    }

    private static bool TryGetAccountId(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}