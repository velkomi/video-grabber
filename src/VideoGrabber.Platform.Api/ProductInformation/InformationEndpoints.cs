using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.ProductInformation;

public static class InformationEndpoints
{
    public static IEndpointRouteBuilder MapInformationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/documents", (DocumentCatalog catalog, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            try { return Results.Ok(new { documents = catalog.Read() }); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or KeyNotFoundException)
            { return Results.Json(new { code = "documents_unavailable" }, statusCode: 503); }
        }).AllowAnonymous();
        endpoints.MapGet("/v1/consents", async (HttpContext http, ConsentStore store, CancellationToken ct) =>
        {
            if (!Account(http, out var account)) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "no-store";
            try { return Results.Ok(new { events = await store.ReadAsync(account, ct) }); }
            catch (Npgsql.NpgsqlException) { return Results.Json(new { code = "consents_unavailable" }, statusCode: 503); }
        }).RequireAuthorization();
        endpoints.MapPost("/v1/consents", async (ConsentRequest request, HttpContext http, DocumentCatalog catalog, ConsentStore store, CancellationToken ct) =>
        {
            if (!Account(http, out var account)) return Results.Unauthorized();
            try
            {
                var document = catalog.Validate(request);
                var bound = request with { DocumentHash = document.Sha256 };
                return Results.Ok(await store.RecordAsync(account, bound, catalog.Snapshot(document.Id, document.Sha256), ct));
            }
            catch (ConsentConflictException) { return Results.Conflict(new { code = "consent_version_or_intent_conflict" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "invalid_consent" }); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or KeyNotFoundException or Npgsql.NpgsqlException)
            { return Results.Json(new { code = "consents_unavailable" }, statusCode: 503); }
        }).RequireAuthorization().RequireRateLimiting("consents")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4096));
        return endpoints;
    }
    private static bool Account(HttpContext http, out Guid id) => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out id) && id != Guid.Empty;
}
