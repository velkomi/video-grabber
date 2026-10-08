using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;

namespace VideoGrabber.Platform.Core.Operations;

public static class PlatformTelemetry
{
    public const string Name = "VideoGrabber.Platform";
    public static readonly ActivitySource Activities = new(Name);
    public static readonly Meter Meter = new(Name, "1.0");
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("vg.http.requests");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("vg.http.duration", "ms");
    public static readonly Counter<long> Jobs = Meter.CreateCounter<long>("vg.worker.jobs");
    public static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("vg.worker.duration", "ms");

    public static string Correlation(string? value) => value is not null
        && (Guid.TryParseExact(value, "N", out _) || Guid.TryParseExact(value, "D", out _))
        ? value : Guid.NewGuid().ToString("N");

    private static string Label(string? value) => value is { Length: > 0 and <= 80 }
        && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_') ? value : "UNKNOWN";

    public static TelemetryEvent Create(string service, string name, string outcome, string traceId,
        double? durationMs = null, string? method = null, string? route = null, int? status = null,
        Exception? error = null, Guid? jobId = null, Guid? attemptId = null) => new(
            1, DateTimeOffset.UtcNow, Label(service), Label(Environment.GetEnvironmentVariable("VG_ENVIRONMENT")),
            Label(Environment.GetEnvironmentVariable("VG_REVISION")), Label(name), Label(outcome), Correlation(traceId),
            durationMs, method, route, status, error?.GetType().Name, jobId, attemptId);
}

public sealed record TelemetryEvent(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("durationMs")] double? DurationMs,
    [property: JsonPropertyName("method")] string? Method,
    [property: JsonPropertyName("route")] string? Route,
    [property: JsonPropertyName("status")] int? Status,
    [property: JsonPropertyName("errorType")] string? ErrorType,
    [property: JsonPropertyName("jobId")] Guid? JobId,
    [property: JsonPropertyName("attemptId")] Guid? AttemptId);
