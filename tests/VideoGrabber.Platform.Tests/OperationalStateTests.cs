using System.Net;
using VideoGrabber.Platform.Api.Operations;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class OperationalStateTests
{
    [Theory]
    [InlineData(14, false)]
    [InlineData(16, true)]
    public void Backup_age_above_target_alerts(
        int minutes,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.BackupTooOld(
                TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(9, false)]
    [InlineData(11, true)]
    public void Queue_age_above_target_alerts(
        int minutes,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.QueueTooOld(
                TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(59, false)]
    [InlineData(61, true)]
    public void Worker_heartbeat_above_target_alerts(
        int seconds,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.WorkerHeartbeatStale(
                TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(20, false)]
    [InlineData(19.9, true)]
    public void Free_disk_below_twenty_percent_alerts(
        double percent,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.DiskTooLow(percent));

    [Theory]
    [InlineData(6, false)]
    [InlineData(8, true)]
    public void Restore_drill_older_than_seven_days_alerts(
        int days,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.RestoreDrillTooOld(
                TimeSpan.FromDays(days)));

    [Theory]
    [InlineData(4, false)]
    [InlineData(6, true)]
    public void Payment_verification_lag_above_five_minutes_alerts(
        int minutes,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.PaymentVerificationTooOld(
                TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Delivery_unknown_and_other_bad_states_create_named_alerts()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new OperationalSnapshot(
            now,
            ActiveJobs: 1,
            OldestQueuedJobAge: TimeSpan.FromMinutes(11),
            ReservationsReviewRequired: 1,
            PaymentVerificationLag: TimeSpan.FromMinutes(6),
            DeliveryUnknown: 1,
            WorkerHeartbeatAge: TimeSpan.FromSeconds(61),
            BackupAge: TimeSpan.FromMinutes(16),
            RestoreDrillAge: TimeSpan.FromDays(8),
            FreeDiskPercent: 19,
            BlockedAdmissions: 10,
            DependencyFailures: 1);

        var codes = PlatformMetrics.Evaluate(snapshot)
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("backup_old", codes);
        Assert.Contains("job_queue_old", codes);
        Assert.Contains("worker_stale", codes);
        Assert.Contains("disk_low", codes);
        Assert.Contains("restore_drill_old", codes);
        Assert.Contains("payment_verification_lag", codes);
        Assert.Contains("delivery_unknown", codes);
        Assert.Contains("reservation_review_required", codes);
        Assert.Contains("dependency_failures", codes);
    }

    [Fact]
    public void Missing_backup_and_restore_evidence_are_not_reported_healthy()
    {
        var snapshot = new OperationalSnapshot(
            DateTimeOffset.UtcNow,
            0,
            null,
            0,
            null,
            0,
            null,
            null,
            null,
            80,
            0,
            0);

        var codes = PlatformMetrics.Evaluate(snapshot)
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("backup_missing", codes);
        Assert.Contains("restore_drill_missing", codes);
    }

    [Fact]
    public void Alert_state_changes_are_deduplicated_and_remind_after_sixty_minutes()
    {
        var tracker =
            new AlertTransitionTracker(TimeSpan.FromMinutes(60));
        var t0 = new DateTimeOffset(
            2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

        var fire = tracker.Observe("disk_low", true, t0);
        var duplicate = tracker.Observe(
            "disk_low",
            true,
            t0.AddMinutes(30));
        var reminder = tracker.Observe(
            "disk_low",
            true,
            t0.AddMinutes(61));
        var resolved = tracker.Observe(
            "disk_low",
            false,
            t0.AddMinutes(62));
        var healthyAgain = tracker.Observe(
            "disk_low",
            false,
            t0.AddMinutes(63));

        Assert.True(fire.Notify);
        Assert.Equal("firing", fire.State);
        Assert.False(duplicate.Notify);
        Assert.True(reminder.Notify);
        Assert.True(resolved.Notify);
        Assert.Equal("resolved", resolved.State);
        Assert.False(healthyAgain.Notify);
    }

    [Fact]
    public void Retention_requires_every_safety_gate()
    {
        Assert.True(PlatformMetrics.RetentionMayRun(
            enabled: true,
            ownedRootQualified: true,
            dryRunFreshAndClean: true,
            backupCurrent: true,
            explicitApproval: true));

        Assert.False(PlatformMetrics.RetentionMayRun(
            enabled: true,
            ownedRootQualified: true,
            dryRunFreshAndClean: true,
            backupCurrent: false,
            explicitApproval: true));
        Assert.False(PlatformMetrics.RetentionMayRun(
            enabled: true,
            ownedRootQualified: false,
            dryRunFreshAndClean: true,
            backupCurrent: true,
            explicitApproval: true));
        Assert.False(PlatformMetrics.RetentionMayRun(
            enabled: true,
            ownedRootQualified: true,
            dryRunFreshAndClean: true,
            backupCurrent: true,
            explicitApproval: false));
    }

    [Fact]
    public async Task Metrics_endpoint_requires_operations_token_and_has_no_sensitive_labels()
    {
        await using var f = await ApiFixture.StartAsync();

        using var denied =
            await f.Anonymous.GetAsync(
                "/internal/operations/metrics");
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            denied.StatusCode);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/internal/operations/metrics");
        request.Headers.Add(
            "X-VideoGrabber-Operations-Token",
            "test-operations-token");
        using var response =
            await f.Anonymous.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Status={(int)response.StatusCode} Body={text} Logs={string.Join(" | ", f.Logs)}");

        Assert.Contains(
            "videograbber_active_jobs",
            text);
        Assert.DoesNotContain(
            "account_id",
            text,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "email",
            text,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "token",
            text,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "http://",
            text,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "https://",
            text,
            StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData(79, 100, false)]
    [InlineData(80, 100, true)]
    [InlineData(8, 10, true)]
    public void Database_connection_saturation_alerts_at_eighty_percent(
        int active,
        int maximum,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.DatabaseSaturated(active, maximum));

    [Theory]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void Outbox_backlog_above_one_thousand_alerts(
        long pending,
        bool expected)
        => Assert.Equal(
            expected,
            PlatformMetrics.OutboxBacklogged(pending));

    [Fact]
    public void Database_outbox_and_retention_failures_have_named_alerts()
    {
        var snapshot = Healthy(DateTimeOffset.UtcNow) with
        {
            DatabaseConnections = 80,
            DatabaseMaxConnections = 100,
            OutboxBacklog = 1001,
            RetentionCleanupFailures = 1
        };

        var codes = PlatformMetrics.Evaluate(snapshot)
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("database_saturated", codes);
        Assert.Contains("outbox_backlog", codes);
        Assert.Contains("retention_cleanup_failed", codes);
    }

    [Fact]
    public async Task Dispatcher_emits_firing_reminder_and_resolution_once()
    {
        var transport = new RecordingAlertTransport();
        var dispatcher = new PlatformAlertDispatcher(transport);
        var t0 = new DateTimeOffset(
            2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

        var firing = Healthy(t0) with { FreeDiskPercent = 19 };
        var duplicate = firing with { CapturedAt = t0.AddMinutes(30) };
        var reminder = firing with { CapturedAt = t0.AddMinutes(61) };
        var resolved = Healthy(t0.AddMinutes(62));

        var first = await dispatcher.DispatchAsync(
            firing, CancellationToken.None);
        var second = await dispatcher.DispatchAsync(
            duplicate, CancellationToken.None);
        var third = await dispatcher.DispatchAsync(
            reminder, CancellationToken.None);
        var fourth = await dispatcher.DispatchAsync(
            resolved, CancellationToken.None);

        Assert.Single(first, x => x.Code == "disk_low" && x.State == "firing");
        Assert.Empty(second);
        Assert.Single(third, x => x.Code == "disk_low" && x.State == "firing");
        Assert.Single(fourth, x => x.Code == "disk_low" && x.State == "resolved");
        Assert.Equal(3, transport.Notifications.Count(x => x.Code == "disk_low"));
        Assert.DoesNotContain(
            transport.Notifications,
            x => x.Message.Contains("account", StringComparison.OrdinalIgnoreCase)
                 || x.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    private static OperationalSnapshot Healthy(DateTimeOffset at)
        => new(
            at,
            ActiveJobs: 0,
            OldestQueuedJobAge: null,
            ReservationsReviewRequired: 0,
            PaymentVerificationLag: null,
            DeliveryUnknown: 0,
            WorkerHeartbeatAge: null,
            BackupAge: TimeSpan.FromMinutes(1),
            RestoreDrillAge: TimeSpan.FromDays(1),
            FreeDiskPercent: 80,
            BlockedAdmissions: 0,
            DependencyFailures: 0,
            OutboxBacklog: 0,
            DatabaseConnections: 1,
            DatabaseMaxConnections: 100,
            RetentionCleanupFailures: 0);

    private sealed class RecordingAlertTransport : IAlertTransport
    {
        public bool Enabled => true;
        public List<AlertNotification> Notifications { get; } = [];

        public Task SendAsync(
            AlertNotification notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
