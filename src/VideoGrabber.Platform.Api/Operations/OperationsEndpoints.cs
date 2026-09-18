using System.Security.Cryptography;
using System.Text;

namespace VideoGrabber.Platform.Api.Operations;

public static class OperationsEndpoints
{
    private const string HeaderName =
        "X-VideoGrabber-Operations-Token";

    public static IEndpointRouteBuilder MapOperationsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/internal/operations/metrics.json",
            JsonAsync);
        endpoints.MapGet(
            "/internal/operations/metrics",
            OpenMetricsAsync);
        return endpoints;
    }

    private static async Task<IResult> JsonAsync(
        HttpContext http,
        IConfiguration configuration,
        PlatformMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration))
            return Results.Unauthorized();

        var snapshot = await metrics.CaptureAsync(cancellationToken);
        var alerts = PlatformMetrics.Evaluate(snapshot);
        return Results.Ok(new
        {
            snapshot,
            alerts
        });
    }

    private static async Task<IResult> OpenMetricsAsync(
        HttpContext http,
        IConfiguration configuration,
        PlatformMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (!Authorized(http, configuration))
            return Results.Unauthorized();

        var snapshot = await metrics.CaptureAsync(cancellationToken);
        return Results.Text(
            PlatformMetrics.ToOpenMetrics(snapshot),
            "text/plain; version=0.0.4; charset=utf-8");
    }

    private static bool Authorized(
        HttpContext http,
        IConfiguration configuration)
    {
        var expected = configuration["VG_OPERATIONS_TOKEN"];
        var actual = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrWhiteSpace(actual))
            return false;

        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        try
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(
                    left,
                    right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }
}
