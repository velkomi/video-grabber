using System.Diagnostics;
using System.Text.Json;
using VideoGrabber.Platform.Core.Operations;

namespace VideoGrabber.Platform.Api.Operations;

public static class RequestTelemetry
{
    public const string JobIdKey = "VideoGrabber.Telemetry.JobId";
    public static async Task InvokeAsync(HttpContext context, Func<Task> next, ILogger logger)
    {
        var correlation = PlatformTelemetry.Correlation(context.Request.Headers["X-Correlation-Id"].ToString());
        context.TraceIdentifier = correlation;
        context.Response.Headers["X-Correlation-Id"] = correlation;
        // Endpoint template excludes actual account/job IDs and every query parameter.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var method = context.Request.Method switch
        {
            "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" or "HEAD" => context.Request.Method,
            _ => "OTHER"
        };
        using var activity = PlatformTelemetry.Activities.StartActivity("http.request", ActivityKind.Server);
        activity?.SetTag("vg.correlation_id", correlation);
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;
        logger.LogInformation("VG_TELEMETRY {DiagnosticEvent}", JsonSerializer.Serialize(
            PlatformTelemetry.Create("api", "http.request", "started", correlation, method:method, route:route)));
        try { await next(); }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            // UseRouting runs later in the existing security pipeline; resolve its result after next.
            route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var status = failure is null ? context.Response.StatusCode : context.RequestAborted.IsCancellationRequested ? 499 : 500;
            var outcome = status == 499 ? "cancelled" : status >= 400 ? "failed" : "succeeded";
            var tags = new TagList { { "http.route", route }, { "http.request.method", method }, { "outcome", outcome } };
            PlatformTelemetry.Requests.Add(1, tags);
            PlatformTelemetry.RequestDuration.Record(elapsed, tags);
            activity?.SetStatus(status >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            activity?.SetTag("http.route", route);
            logger.LogInformation("VG_TELEMETRY {DiagnosticEvent}", JsonSerializer.Serialize(
                PlatformTelemetry.Create("api", "http.request", outcome, correlation, elapsed, method, route, status, failure,
                    context.Items.TryGetValue(JobIdKey, out var value) && value is Guid jobId ? jobId : null)));
        }
    }
}
