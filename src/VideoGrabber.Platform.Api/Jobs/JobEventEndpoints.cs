using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Jobs;

public static class JobEventEndpoints
{
    public static IEndpointRouteBuilder MapJobEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/queue/order", ReorderAsync).RequireAuthorization();
        endpoints.MapGet("/v1/jobs/events", StreamAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReorderAsync(
        QueueOrder request,
        HttpContext http,
        JobStore jobs,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            return Results.Ok(await jobs.ReorderAsync(
                accountId, request, cancellationToken));
        }
        catch (JobRequestConflictException)
        {
            return Results.Conflict(new { code = "queue_version_conflict" });
        }
    }

    private static async Task StreamAsync(
        HttpContext http,
        JobStore jobs,
        CancellationToken cancellationToken,
        bool snapshot = false)
    {
        if (!TryAccount(http, out var accountId))
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Guid? cursor = null;
        var lastEvent = http.Request.Headers["Last-Event-ID"].ToString();
        if (!string.IsNullOrWhiteSpace(lastEvent)
            && Guid.TryParseExact(lastEvent, "N", out var parsed))
            cursor = parsed;

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        while (!cancellationToken.IsCancellationRequested
               && DateTimeOffset.UtcNow < deadline)
        {
            var events = await jobs.ReadEventsAsync(
                accountId, cursor, 50, cancellationToken);
            if (events.Count == 0)
            {
                if (snapshot) break;
                await http.Response.WriteAsync(": keepalive\n\n", cancellationToken);
                await http.Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            foreach (var item in events)
            {
                await http.Response.WriteAsync(
                    "id: " + item.EventId + "\n", cancellationToken);
                await http.Response.WriteAsync(
                    "event: " + item.EventType + "\n", cancellationToken);
                await http.Response.WriteAsync(
                    "data: " + JsonSerializer.Serialize(item) + "\n\n",
                    cancellationToken);
                cursor = Guid.ParseExact(item.EventId, "N");
            }
            await http.Response.Body.FlushAsync(cancellationToken);
            if (snapshot) break;
        }
    }

    private static bool TryAccount(HttpContext http, out Guid accountId)
        => Guid.TryParse(http.User.FindFirst("account_id")?.Value, out accountId);
}
