using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Promotions;

public static class PromotionEndpoints
{
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/v1/referrals", async (HttpContext http,PromotionStore store,CancellationToken ct) =>
            await ResultAsync(async () => Results.Ok(await store.SummaryAsync(Account(http),ct)))).RequireAuthorization();
        routes.MapPost("/v1/referrals/claim", async (ReferralClaimRequest request,HttpContext http,PromotionStore store,CancellationToken ct) =>
            await ResultAsync(async () => Results.Ok(new { claimed = await store.ClaimAsync(Account(http),request.Code,ct) })))
            .RequireAuthorization().RequireRateLimiting("promotions");
        routes.MapPost("/v1/promotions/quote", async (PromotionQuoteRequest request,HttpContext http,PaymentStore payments,PromotionStore store,CancellationToken ct) =>
            await ResultAsync(async () => Results.Ok(await store.QuoteAsync(Account(http),request,
                payments.Catalog.RequireProduct(request.Sku,request.Provider,request.Recurring),payments.Catalog.Version,ct))))
            .RequireAuthorization().RequireRateLimiting("promotions");
        routes.MapGet("/v1/admin/promotions", async (HttpContext http,AdminService admin,PromotionStore store,CancellationToken ct) =>
        {
            if (!await admin.IsAdminAsync(Account(http),ct)) return Results.Forbid();
            return await ResultAsync(async () => Results.Ok(await store.ListPromosAsync(ct)));
        }).RequireAuthorization();
        routes.MapPost("/v1/admin/promotions", async (PromoDefinition request,HttpContext http,AdminService admin,PromotionStore store,TimeProvider clock,CancellationToken ct) =>
        {
            var actor=Account(http);
            if (!await admin.IsAdminAsync(actor,ct)) return Results.Forbid();
            if (!FreshMfa(http,clock)) return Results.Unauthorized();
            return await ResultAsync(async () => Results.Ok(await store.SavePromoAsync(actor,request,ct)));
        }).RequireAuthorization().RequireRateLimiting("promotions");
        return routes;
    }

    private static Guid Account(HttpContext http) => Guid.TryParse(http.User.FindFirst("account_id")?.Value,out var id) && id!=Guid.Empty
        ? id : throw new UnauthorizedAccessException();
    private static bool FreshMfa(HttpContext http,TimeProvider clock)
    {
        if (!long.TryParse(http.User.FindFirst("mfa_at")?.Value,out var at)) return false;
        var age=clock.GetUtcNow().ToUnixTimeSeconds()-at;
        return age is >= -30 and <= 300;
    }
    private static async Task<IResult> ResultAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (PromotionDisabledException) { return Results.Json(new {code="promotions_disabled"},statusCode:503); }
        catch (PaymentDisabledException) { return Results.Json(new {code="payments_disabled"},statusCode:503); }
        catch (PromotionConflictException) { return Results.Conflict(new {code="promotion_conflict"}); }
        catch (PromotionUnavailableException e) { return Results.BadRequest(new {code=e.Code}); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (KeyNotFoundException) { return Results.BadRequest(new {code="payment_product_unavailable"}); }
        catch (ArgumentException) { return Results.BadRequest(new {code="invalid_promotion"}); }
        catch (InvalidOperationException e) when (e.Message=="one_time_discount_requires_single_purchase")
        { return Results.BadRequest(new {code="one_time_discount_requires_single_purchase"}); }
    }
}
