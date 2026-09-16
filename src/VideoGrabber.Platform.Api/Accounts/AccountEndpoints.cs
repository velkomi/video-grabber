using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Accounts;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/me", ReadMeAsync).RequireAuthorization();
        endpoints.MapGet("/v1/accounts/{accountId:guid}", ReadAccountAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReadMeAsync(
        HttpContext http,
        IAccountStore store,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var accountId)) return Results.Unauthorized();
        var profile = await store.ReadAsync(accountId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> ReadAccountAsync(
        Guid accountId,
        HttpContext http,
        IAccountStore store,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(http, out var current) || current != accountId)
            return Results.NotFound();
        var profile = await store.ReadAsync(accountId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static bool TryGetAccountId(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
