using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace VideoGrabber.Platform.Api.Operations;

public sealed record AlertNotification(
    string Code,
    string State,
    string Severity,
    string Message,
    DateTimeOffset At);

public interface IAlertTransport
{
    bool Enabled { get; }

    Task SendAsync(
        AlertNotification notification,
        CancellationToken cancellationToken);
}

public sealed class HttpAlertTransport(
    IHttpClientFactory clients,
    IConfiguration configuration,
    PlatformOperationalCounters counters) : IAlertTransport
{
    private readonly Uri? _uri = ParseUri(
        configuration["VG_ALERT_WEBHOOK_URL"]);

    public bool Enabled
        => string.Equals(
               configuration["VG_ALERT_DELIVERY_AUTHORIZED"],
               "YES",
               StringComparison.Ordinal)
           && _uri is not null;

    public async Task SendAsync(
        AlertNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!Enabled || _uri is null)
            return;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _uri)
        {
            Content = JsonContent.Create(notification)
        };

        var token = configuration["VG_ALERT_WEBHOOK_TOKEN"];
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation(
                "X-VideoGrabber-Alert-Token",
                token);

        try
        {
            using var response = await clients
                .CreateClient("OperationsAlerts")
                .SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                counters.RecordDependencyFailure();
                throw new HttpRequestException(
                    "alert_delivery_failed",
                    null,
                    response.StatusCode);
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            or IOException
            or TaskCanceledException)
        {
            counters.RecordDependencyFailure();
            throw;
        }
    }

    private static Uri? ParseUri(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo))
            return null;

        if (uri.Scheme == Uri.UriSchemeHttps)
            return uri;

        if (uri.Scheme == Uri.UriSchemeHttp
            && IsLoopback(uri.Host))
            return uri;

        return null;
    }

    private static bool IsLoopback(string host)
    {
        if (host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase))
            return true;
        if (!IPAddress.TryParse(host, out var address))
            return false;
        return IPAddress.IsLoopback(address);
    }
}

public sealed class PlatformAlertDispatcher(
    IAlertTransport transport)
{
    private static readonly string[] KnownCodes =
    [
        "backup_missing",
        "backup_old",
        "job_queue_old",
        "worker_stale",
        "disk_low",
        "database_saturated",
        "outbox_backlog",
        "restore_drill_missing",
        "restore_drill_old",
        "payment_verification_lag",
        "delivery_unknown",
        "reservation_review_required",
        "dependency_failures",
        "retention_cleanup_failed"
    ];

    private readonly AlertTransitionTracker _tracker =
        new(TimeSpan.FromMinutes(60));

    public async Task<IReadOnlyList<AlertNotification>> DispatchAsync(
        OperationalSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var active = PlatformMetrics.Evaluate(snapshot)
            .ToDictionary(
                alert => alert.Code,
                StringComparer.Ordinal);
        var notifications = new List<AlertNotification>();

        foreach (var code in KnownCodes)
        {
            var isActive = active.TryGetValue(
                code,
                out var alert);
            var transition = _tracker.Observe(
                code,
                isActive,
                snapshot.CapturedAt);
            if (!transition.Notify)
                continue;

            var notification = isActive
                ? new AlertNotification(
                    code,
                    transition.State,
                    alert!.Severity,
                    alert.Message,
                    transition.At)
                : new AlertNotification(
                    code,
                    transition.State,
                    "info",
                    "Operational alert resolved.",
                    transition.At);

            notifications.Add(notification);
            if (transport.Enabled)
                await transport.SendAsync(
                    notification,
                    cancellationToken)
                    .ConfigureAwait(false);
        }

        return notifications;
    }
}

public sealed class PlatformAlertHostedService(
    PlatformMetrics metrics,
    PlatformAlertDispatcher dispatcher,
    IConfiguration configuration,
    ILogger<PlatformAlertHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!string.Equals(
                configuration["VG_ALERTS_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase))
            return;

        var interval = TimeSpan.FromSeconds(
            int.TryParse(
                configuration["VG_ALERT_INTERVAL_SECONDS"],
                out var parsed)
            && parsed is >= 10 and <= 3600
                ? parsed
                : 30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await metrics.CaptureAsync(
                    stoppingToken).ConfigureAwait(false);
                await dispatcher.DispatchAsync(
                    snapshot,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Operations alert iteration failed: {ExceptionType}",
                    ex.GetType().Name);
            }

            try
            {
                await Task.Delay(
                    interval,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
