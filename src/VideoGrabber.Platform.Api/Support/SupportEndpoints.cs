using System.Text.Json;
using Altcha;
using VideoGrabber.Platform.Api.Security;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Support;

public static class SupportEndpoints
{
    private const int MaxBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions InputOptions = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, MaxDepth = 12 };

    public static void MapSupportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/v1/support/config", (HttpContext http, SupportOptions options) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new
            { enabled = options.Ready, topics = SupportOptions.Topics, messageMinLength = 20, messageMaxLength = 4000,
              csrfToken = http.Request.Cookies.TryGetValue("vg_csrf", out var csrf) ? csrf : null });
        })
            .RequireRateLimiting("auth-config");
        routes.MapGet("/v1/support/challenge", (SupportOptions options, SupportChallengeService challenges) =>
        {
            if (!options.Ready) return Results.Json(new { code = "support_temporarily_unavailable" }, statusCode: 503);
            return Results.Json(challenges.Create(), AltchaJson.SerializerOptions);
        }).RequireRateLimiting("support-challenge");
        routes.MapPost("/v1/support/requests", SubmitAsync).RequireRateLimiting("support-submit");
    }

    private static async Task<IResult> SubmitAsync(HttpContext http, SupportOptions options,
        SupportChallengeService challenges, SupportStore store, CancellationToken ct)
    {
        if (!options.Ready) return Results.Json(new { code = "support_temporarily_unavailable" }, statusCode: 503);
        if (!http.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (http.Request.ContentLength > MaxBodyBytes) return Results.StatusCode(413);
        try
        {
            var body = await ReadLimitedAsync(http.Request.Body, ct);
            var request = SupportOptions.Validate(JsonSerializer.Deserialize<SupportRequest>(body, InputOptions)
                ?? throw new JsonException());
            Guid? accountId = http.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var id) ? id : null;
            var existing = await store.FindExistingAsync(request, accountId, ct);
            if (existing is not null) return Results.Ok(existing);
            var claim = challenges.Verify(request.Altcha);
            var source = http.Request.Headers["X-VideoGrabber-Client"].ToString() switch
            { "windows" => "windows", "miniapp" => "miniapp", _ => "form" };
            var accepted = await store.SubmitVerifiedAsync(request, accountId,
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown", source, claim, ct);
            return Results.Json(accepted, statusCode: 201);
        }
        catch (SupportRequestException ex)
        {
            if (ex.RetryAfterSeconds > 0) http.Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Json(new { code = ex.Code }, statusCode: ex.Status);
        }
        catch (JsonException) { return Results.Json(new { code = "invalid_support_request" }, statusCode: 400); }
        catch (ArgumentException) { return Results.Json(new { code = "invalid_support_request" }, statusCode: 400); }
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (memory.Length + read > MaxBodyBytes) throw new SupportRequestException(413, "support_request_too_large");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }
}
