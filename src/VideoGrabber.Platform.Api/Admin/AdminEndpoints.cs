using Microsoft.AspNetCore.Mvc;
using VideoGrabber.Platform.Api.Accounts;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Admin;

public sealed record AdminReasonRequest(string Reason);
public sealed record AdminAdjustmentRequest(string EvidenceId, string Reason);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/admin/mfa/status", MfaStatusAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/admin/mfa/enroll", MfaEnrollAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/admin/mfa/confirm", MfaConfirmAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapPost("/v1/admin/mfa/verify", MfaVerifyAsync).RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapGet("/v1/admin/accounts", SearchAsync).RequireAuthorization();
        endpoints.MapGet("/v1/admin/accounts/{accountId:guid}", ReadAsync).RequireAuthorization();
        endpoints.MapGet("/v1/admin/accounts/{accountId:guid}/audit", AuditAsync).RequireAuthorization();
        endpoints.MapGet("/v1/admin/accounts/{accountId:guid}/grants", GrantsAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/accounts/{accountId:guid}/block", BlockAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/accounts/{accountId:guid}/unblock", UnblockAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/accounts/{accountId:guid}/devices/reset", ResetDevicesAsync).RequireAuthorization();
        endpoints.MapGet("/v1/admin/accounts/{accountId:guid}/features", FeaturesAsync).RequireAuthorization();
        endpoints.MapPut("/v1/admin/accounts/{accountId:guid}/features/{feature}", SetFeatureAsync).RequireAuthorization();
        endpoints.MapDelete("/v1/admin/accounts/{accountId:guid}/features/{feature}", DeleteFeatureAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/grants/{grantId:guid}/revoke", RevokeGiftAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/reservations/{reservationId:guid}/adjust", AdjustAsync).RequireAuthorization();
        endpoints.MapPost("/v1/admin/accounts/merge", MergeAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> MfaStatusAsync(
        HttpContext http,
        AdminMfaService mfa,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId))
            return Results.Unauthorized();
        return Results.Ok(await mfa.StatusAsync(
            accountId,
            cancellationToken));
    }

    private static async Task<IResult> MfaEnrollAsync(
        HttpContext http,
        AdminMfaService mfa,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(await mfa.BeginEnrollAsync(
                accountId,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new
            {
                code = "mfa_already_enrolled",
                detail = ex.Message
            });
        }
    }

    private static async Task<IResult> MfaConfirmAsync(
        AdminMfaVerifyRequest request,
        HttpContext http,
        AdminMfaService mfa,
        CancellationToken cancellationToken)
        => await MfaVerifyCoreAsync(
            request,
            http,
            mfa,
            confirm: true,
            cancellationToken);

    private static async Task<IResult> MfaVerifyAsync(
        AdminMfaVerifyRequest request,
        HttpContext http,
        AdminMfaService mfa,
        CancellationToken cancellationToken)
        => await MfaVerifyCoreAsync(
            request,
            http,
            mfa,
            confirm: false,
            cancellationToken);

    private static async Task<IResult> MfaVerifyCoreAsync(
        AdminMfaVerifyRequest request,
        HttpContext http,
        AdminMfaService mfa,
        bool confirm,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(confirm
                ? await mfa.ConfirmAsync(
                    accountId,
                    request.Code,
                    cancellationToken)
                : await mfa.VerifyAsync(
                    accountId,
                    request.Code,
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
                code = "invalid_mfa_code",
                detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new
            {
                code = "mfa_state_conflict",
                detail = ex.Message
            });
        }
    }

    private static async Task<IResult> SearchAsync(string identity, HttpContext http,
        AdminService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        try { return Results.Ok(await service.SearchAsync(actorId, identity, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { code = "invalid_identity", detail = ex.Message }); }
    }

    private static async Task<IResult> ReadAsync(Guid accountId, HttpContext http,
        AdminService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        try
        {
            var result = await service.ReadAsync(actorId, accountId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
    }


    private static async Task<IResult> GrantsAsync(
        Guid accountId,
        HttpContext http,
        AdminService service,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(await service.ReadGrantsAsync(
                actorId,
                accountId,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> AuditAsync(Guid accountId, HttpContext http,
        AdminService service, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        try { return Results.Ok(await service.ReadAuditAsync(actorId, accountId, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
    }
    private static Task<IResult> BlockAsync(Guid accountId, AdminReasonRequest request,
        HttpContext http, AdminService service, TimeProvider clock, CancellationToken ct)
        => MutateAsync(http, clock, service,
            (actor, token) => service.BlockAsync(actor, accountId, true, request.Reason, token), ct);

    private static Task<IResult> UnblockAsync(Guid accountId, AdminReasonRequest request,
        HttpContext http, AdminService service, TimeProvider clock, CancellationToken ct)
        => MutateAsync(http, clock, service,
            (actor, token) => service.BlockAsync(actor, accountId, false, request.Reason, token), ct);

    private static Task<IResult> ResetDevicesAsync(Guid accountId, AdminReasonRequest request,
        HttpContext http, AdminService service, TimeProvider clock, CancellationToken ct)
        => MutateAsync(http, clock, service,
            (actor, token) => service.ResetDevicesAsync(actor, accountId, request.Reason, token), ct);


    private static async Task<IResult> FeaturesAsync(
        Guid accountId,
        HttpContext http,
        AdminFeatureOverrideService overrides,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId))
            return Results.Unauthorized();
        try
        {
            return Results.Ok(await overrides.ListAdminAsync(
                actorId,
                accountId,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> SetFeatureAsync(
        Guid accountId,
        string feature,
        AdminFeatureOverrideRequest request,
        HttpContext http,
        AdminFeatureOverrideService overrides,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId))
            return Results.Unauthorized();
        if (!HasFreshMfa(http, clock))
            return Results.Unauthorized();
        try
        {
            await overrides.SetAsync(
                actorId,
                accountId,
                feature,
                request,
                cancellationToken);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new
            {
                code = "invalid_feature_override",
                detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DeleteFeatureAsync(
        Guid accountId,
        string feature,
        [FromBody] AdminReasonRequest request,
        HttpContext http,
        AdminFeatureOverrideService overrides,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId))
            return Results.Unauthorized();
        if (!HasFreshMfa(http, clock))
            return Results.Unauthorized();
        try
        {
            await overrides.DeleteAsync(
                actorId,
                accountId,
                feature,
                request.Reason,
                cancellationToken);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new
            {
                code = "invalid_feature_override",
                detail = ex.Message
            });
        }
    }

    private static Task<IResult> RevokeGiftAsync(Guid grantId, AdminReasonRequest request,
        HttpContext http, AdminService service, TimeProvider clock, CancellationToken ct)
        => MutateAsync(http, clock, service,
            (actor, token) => service.RevokeGiftAsync(actor, grantId, request.Reason, token), ct);

    private static Task<IResult> AdjustAsync(Guid reservationId, AdminAdjustmentRequest request,
        HttpContext http, AdminService service, TimeProvider clock, CancellationToken ct)
        => MutateAsync(http, clock, service,
            (actor, token) => service.AdjustAsync(actor, reservationId, request.EvidenceId, request.Reason, token), ct);
    private static async Task<IResult> MergeAsync(MergeRequest request, HttpContext http,
        IdentityLinkService links, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        if (!HasFreshMfa(http, clock)) return Results.Unauthorized();
        try
        {
            await links.MergeAsync(actorId, request, cancellationToken);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (FinancialMergeRequiresReconciliationException)
        { return Results.Conflict(new { code = "merge_requires_reconciliation" }); }
        catch (IdentityLinkConflictException)
        { return Results.Conflict(new { code = "merge_conflict" }); }
        catch (IdentityNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_merge", detail = ex.Message }); }
    }

    private static async Task<IResult> MutateAsync(HttpContext http, TimeProvider clock,
        AdminService service, Func<Guid, CancellationToken, Task> action, CancellationToken ct)
    {
        if (!TryAccount(http, out var actorId)) return Results.Unauthorized();
        if (!HasFreshMfa(http, clock)) return Results.Unauthorized();
        try { await action(actorId, ct); return Results.NoContent(); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (AdminConflictException) { return Results.Conflict(new { code = "admin_conflict" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = "invalid_admin_request", detail = ex.Message }); }
    }
    private static bool HasFreshMfa(HttpContext http, TimeProvider clock)
    {
        var raw = http.User.FindFirst("mfa_at")?.Value;
        if (!long.TryParse(raw, out var seconds)) return false;
        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
        return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(5);
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
