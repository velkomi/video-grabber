using System.Globalization;
using System.Text;
using Npgsql;

namespace VideoGrabber.Platform.Api.Operations;

public sealed class OperationsDataSource : IAsyncDisposable
{
    private OperationsDataSource(NpgsqlDataSource dataSource)
        => DataSource = dataSource;

    public NpgsqlDataSource DataSource { get; }

    public static OperationsDataSource CreateOwned(string dsn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dsn);
        return new OperationsDataSource(NpgsqlDataSource.Create(dsn));
    }

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}
public sealed record OperationalSnapshot(
    DateTimeOffset CapturedAt,
    long ActiveJobs,
    TimeSpan? OldestQueuedJobAge,
    long ReservationsReviewRequired,
    TimeSpan? PaymentVerificationLag,
    long DeliveryUnknown,
    TimeSpan? WorkerHeartbeatAge,
    TimeSpan? BackupAge,
    TimeSpan? RestoreDrillAge,
    double FreeDiskPercent,
    long BlockedAdmissions,
    long DependencyFailures,
    long OutboxBacklog = 0,
    int DatabaseConnections = 0,
    int DatabaseMaxConnections = 1,
    long RetentionCleanupFailures = 0);

public sealed record OperationalAlert(
    string Code,
    string Severity,
    string Message);

public sealed record AlertTransition(
    string Code,
    string State,
    DateTimeOffset At,
    bool Notify);

public sealed class PlatformOperationalCounters
{
    private long _blockedAdmissions;
    private long _dependencyFailures;
    private long _retentionCleanupFailures;

    public long BlockedAdmissions => Interlocked.Read(ref _blockedAdmissions);
    public long DependencyFailures => Interlocked.Read(ref _dependencyFailures);
    public long RetentionCleanupFailures => Interlocked.Read(ref _retentionCleanupFailures);

    public void RecordBlockedAdmission()
        => Interlocked.Increment(ref _blockedAdmissions);

    public void RecordDependencyFailure()
        => Interlocked.Increment(ref _dependencyFailures);

    public void RecordRetentionCleanupFailure()
        => Interlocked.Increment(ref _retentionCleanupFailures);
}

public sealed class AlertTransitionTracker(
    TimeSpan? reminderInterval = null)
{
    private readonly TimeSpan _reminderInterval =
        reminderInterval ?? TimeSpan.FromMinutes(60);
    private readonly Dictionary<string, State> _states =
        new(StringComparer.Ordinal);

    public AlertTransition Observe(
        string code,
        bool active,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (!_states.TryGetValue(code, out var previous))
        {
            _states[code] = new State(active, active ? now : null);
            return new AlertTransition(
                code,
                active ? "firing" : "healthy",
                now,
                active);
        }

        if (previous.Active != active)
        {
            _states[code] = new State(active, active ? now : null);
            return new AlertTransition(
                code,
                active ? "firing" : "resolved",
                now,
                true);
        }

        if (active
            && previous.LastNotification is DateTimeOffset notified
            && now - notified >= _reminderInterval)
        {
            _states[code] = previous with { LastNotification = now };
            return new AlertTransition(code, "firing", now, true);
        }

        return new AlertTransition(
            code,
            active ? "firing" : "healthy",
            now,
            false);
    }

    private sealed record State(
        bool Active,
        DateTimeOffset? LastNotification);
}

public sealed class PlatformMetrics(
    OperationsDataSource operationsDataSource,
    IConfiguration configuration,
    TimeProvider clock,
    PlatformOperationalCounters counters)
{
    public static bool BackupTooOld(TimeSpan age)
        => age > TimeSpan.FromMinutes(15);

    public static bool QueueTooOld(TimeSpan age)
        => age > TimeSpan.FromMinutes(10);

    public static bool WorkerHeartbeatStale(TimeSpan age)
        => age > TimeSpan.FromSeconds(60);

    public static bool DiskTooLow(double freePercent)
        => freePercent < 20d;

    public static bool RestoreDrillTooOld(TimeSpan age)
        => age > TimeSpan.FromDays(7);

    public static bool PaymentVerificationTooOld(TimeSpan age)
        => age > TimeSpan.FromMinutes(5);

    public static bool DatabaseSaturated(int active, int maximum)
        => maximum <= 0 || (100d * active / maximum) >= 80d;

    public static bool OutboxBacklogged(long pending)
        => pending > 1000;

    public static bool RetentionMayRun(
        bool enabled,
        bool ownedRootQualified,
        bool dryRunFreshAndClean,
        bool backupCurrent,
        bool explicitApproval)
        => enabled
           && ownedRootQualified
           && dryRunFreshAndClean
           && backupCurrent
           && explicitApproval;

    public async Task<OperationalSnapshot> CaptureAsync(
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection =
            await operationsDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select
              (select count(*) from licensing.jobs
               where state in ('queued','waiting_for_worker','running','cancel_requested')),
              (select extract(epoch from (@now-min(created_at)))::double precision
               from licensing.jobs where state in ('queued','waiting_for_worker')),
              (select count(*) from licensing.reservations
               where state='review_required'),
              (select extract(epoch from (@now-min(updated_at)))::double precision
               from licensing.payments
               where state in ('pending','refund_pending','reconcile_required')),
              (select count(*) from licensing.delivery_attempts
               where state='delivery_unknown'),
              (select extract(epoch from (@now-min(heartbeat_at)))::double precision
               from licensing.job_attempts where state='running'),
              (select count(*) from licensing.job_outbox where consumed_at is null),
              (select count(*) from pg_stat_activity where datname=current_database()),
              current_setting('max_connections')::int
            """,
            connection);
        command.Parameters.AddWithValue("now", now);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException("Operational metrics query returned no row.");

        var activeJobs = reader.GetInt64(0);
        var oldestQueue = Seconds(reader, 1);
        var reviewRequired = reader.GetInt64(2);
        var paymentLag = Seconds(reader, 3);
        var deliveryUnknown = reader.GetInt64(4);
        var heartbeatAge = Seconds(reader, 5);
        var outboxBacklog = reader.GetInt64(6);
        var dbConnections = checked((int)reader.GetInt64(7));
        var dbMaxConnections = reader.GetInt32(8);

        var backupAge = AgeFromConfiguration(
            configuration["Operations:LastBackupUtc"]
            ?? configuration["VG_LAST_BACKUP_UTC"],
            now);
        var restoreAge = AgeFromConfiguration(
            configuration["Operations:LastRestoreDrillUtc"]
            ?? configuration["VG_LAST_RESTORE_DRILL_UTC"],
            now);
        var freeDisk = FreeDiskPercent(
            configuration["Operations:CapacityPath"]
            ?? AppContext.BaseDirectory);

        return new OperationalSnapshot(
            now,
            activeJobs,
            oldestQueue,
            reviewRequired,
            paymentLag,
            deliveryUnknown,
            heartbeatAge,
            backupAge,
            restoreAge,
            freeDisk,
            counters.BlockedAdmissions,
            counters.DependencyFailures,
            outboxBacklog,
            dbConnections,
            dbMaxConnections,
            counters.RetentionCleanupFailures);
    }

    public static IReadOnlyList<OperationalAlert> Evaluate(
        OperationalSnapshot snapshot)
    {
        var alerts = new List<OperationalAlert>();

        if (snapshot.BackupAge is null)
            alerts.Add(new(
                "backup_missing",
                "critical",
                "No successful backup timestamp is available."));
        else if (BackupTooOld(snapshot.BackupAge.Value))
            alerts.Add(new(
                "backup_old",
                "critical",
                "Last successful backup is older than 15 minutes."));

        if (snapshot.OldestQueuedJobAge is TimeSpan queueAge
            && QueueTooOld(queueAge))
            alerts.Add(new(
                "job_queue_old",
                "warning",
                "Oldest queued job is older than 10 minutes."));

        if (snapshot.ActiveJobs > 0
            && snapshot.WorkerHeartbeatAge is TimeSpan heartbeat
            && WorkerHeartbeatStale(heartbeat))
            alerts.Add(new(
                "worker_stale",
                "critical",
                "Running worker heartbeat is older than 60 seconds."));

        if (DiskTooLow(snapshot.FreeDiskPercent))
            alerts.Add(new(
                "disk_low",
                "critical",
                "Free disk is below 20 percent."));

        if (DatabaseSaturated(
                snapshot.DatabaseConnections,
                snapshot.DatabaseMaxConnections))
            alerts.Add(new(
                "database_saturated",
                "critical",
                "Database connection utilization is at or above 80 percent."));

        if (OutboxBacklogged(snapshot.OutboxBacklog))
            alerts.Add(new(
                "outbox_backlog",
                "warning",
                "Pending job outbox events exceed 1000."));

        if (snapshot.RestoreDrillAge is null)
            alerts.Add(new(
                "restore_drill_missing",
                "warning",
                "No restore drill timestamp is available."));
        else if (RestoreDrillTooOld(snapshot.RestoreDrillAge.Value))
            alerts.Add(new(
                "restore_drill_old",
                "warning",
                "Last restore drill is older than 7 days."));

        if (snapshot.PaymentVerificationLag is TimeSpan paymentLag
            && PaymentVerificationTooOld(paymentLag))
            alerts.Add(new(
                "payment_verification_lag",
                "critical",
                "Pending payment verification is older than 5 minutes."));

        if (snapshot.DeliveryUnknown > 0)
            alerts.Add(new(
                "delivery_unknown",
                "warning",
                "At least one delivery has an uncertain provider ACK."));

        if (snapshot.ReservationsReviewRequired > 0)
            alerts.Add(new(
                "reservation_review_required",
                "warning",
                "At least one credit reservation requires review."));

        if (snapshot.DependencyFailures > 0)
            alerts.Add(new(
                "dependency_failures",
                "critical",
                "Runtime dependency failures have been observed."));

        if (snapshot.RetentionCleanupFailures > 0)
            alerts.Add(new(
                "retention_cleanup_failed",
                "critical",
                "At least one retention cleanup failed."));

        return alerts;
    }

    public static string ToOpenMetrics(
        OperationalSnapshot snapshot)
    {
        var lines = new StringBuilder();
        Gauge(lines, "videograbber_active_jobs", snapshot.ActiveJobs);
        Gauge(
            lines,
            "videograbber_oldest_queued_job_seconds",
            snapshot.OldestQueuedJobAge?.TotalSeconds ?? 0);
        Gauge(
            lines,
            "videograbber_reservations_review_required",
            snapshot.ReservationsReviewRequired);
        Gauge(
            lines,
            "videograbber_payment_verification_lag_seconds",
            snapshot.PaymentVerificationLag?.TotalSeconds ?? 0);
        Gauge(
            lines,
            "videograbber_delivery_unknown",
            snapshot.DeliveryUnknown);
        Gauge(
            lines,
            "videograbber_worker_heartbeat_age_seconds",
            snapshot.WorkerHeartbeatAge?.TotalSeconds ?? 0);
        Gauge(
            lines,
            "videograbber_backup_age_seconds",
            snapshot.BackupAge?.TotalSeconds ?? -1);
        Gauge(
            lines,
            "videograbber_restore_drill_age_seconds",
            snapshot.RestoreDrillAge?.TotalSeconds ?? -1);
        Gauge(
            lines,
            "videograbber_free_disk_percent",
            snapshot.FreeDiskPercent);
        Gauge(
            lines,
            "videograbber_blocked_admissions_total",
            snapshot.BlockedAdmissions);
        Gauge(
            lines,
            "videograbber_dependency_failures_total",
            snapshot.DependencyFailures);
        Gauge(
            lines,
            "videograbber_outbox_backlog",
            snapshot.OutboxBacklog);
        Gauge(
            lines,
            "videograbber_database_connections",
            snapshot.DatabaseConnections);
        Gauge(
            lines,
            "videograbber_database_max_connections",
            snapshot.DatabaseMaxConnections);
        Gauge(
            lines,
            "videograbber_retention_cleanup_failures_total",
            snapshot.RetentionCleanupFailures);
        return lines.ToString();
    }

    private static void Gauge(
        StringBuilder builder,
        string name,
        double value)
    {
        builder.Append(name)
            .Append(' ')
            .Append(value.ToString(
                "0.###",
                CultureInfo.InvariantCulture))
            .AppendLine();
    }

    private static TimeSpan? Seconds(
        NpgsqlDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var seconds = reader.GetDouble(ordinal);
        if (seconds < 0) seconds = 0;
        return TimeSpan.FromSeconds(seconds);
    }

    private static TimeSpan? AgeFromConfiguration(
        string? raw,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
            return null;
        var age = now - timestamp;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static double FreeDiskPercent(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)) return 0;
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0) return 0;
            return 100d * drive.AvailableFreeSpace / drive.TotalSize;
        }
        catch
        {
            return 0;
        }
    }
}
