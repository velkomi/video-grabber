# VideoGrabber operations dashboard and alert policy

## Internal metrics

The API exposes two internal endpoints protected by the separate X-VideoGrabber-Operations-Token capability:

- /internal/operations/metrics.json
- /internal/operations/metrics

The OpenMetrics payload contains only aggregate values. It must not contain account IDs, email addresses, source URLs, payment IDs, bot tokens, DSNs or webhook URLs.

Current aggregate signals:

- videograbber_active_jobs
- videograbber_oldest_queued_job_seconds
- videograbber_reservations_review_required
- videograbber_payment_verification_lag_seconds
- videograbber_delivery_unknown
- videograbber_worker_heartbeat_age_seconds
- videograbber_backup_age_seconds
- videograbber_restore_drill_age_seconds
- videograbber_free_disk_percent
- videograbber_blocked_admissions_total
- videograbber_dependency_failures_total
- videograbber_outbox_backlog
- videograbber_database_connections
- videograbber_database_max_connections
- videograbber_retention_cleanup_failures_total

Worker-side WorkerMetrics records only job-root bytes/file count, free disk and presence of the qualified media tools. It does not enumerate user-owned output directories.

## Alert thresholds

The machine-readable rules are in deploy/platform/alerts.json.

Critical:

- backup missing or older than 15 minutes;
- running worker heartbeat older than 60 seconds;
- free disk below 20%;
- PostgreSQL connections at or above 80% of max_connections;
- payment verification/reconciliation lag older than 5 minutes;
- dependency failures;
- retention cleanup failure.

Warning:

- oldest queued job older than 10 minutes;
- pending outbox events above 1000;
- restore drill missing or older than 7 days;
- any delivery_unknown;
- any reservation in review_required.

Alerts are transition-based. The first unhealthy observation sends firing; repeated observations are deduplicated; an unresolved alert may remind after 60 minutes; recovery sends resolved.

## Alert delivery authorization

HTTP alert delivery is off unless all of the following are true:

1. VG_ALERTS_ENABLED=true;
2. VG_ALERT_DELIVERY_AUTHORIZED=YES;
3. VG_ALERT_WEBHOOK_URL is HTTPS, or loopback HTTP for local qualification.

An optional VG_ALERT_WEBHOOK_TOKEN is sent only as a request header and must never appear in evidence or metrics. A real messaging destination requires separate authorization. Test-PlatformAlerts.ps1 uses a loopback receiver and produces local evidence only.

## Capacity measurement

Run Measure-PlatformCapacity.ps1 with EnvironmentName vg-stage-..., an absolute EvidenceDirectory, and DurationSeconds 120.

The script is observation-only. It records host CPU/memory, disk free space, WSL inode free space when available, Docker neighbors and Docker stats. It never changes Docker limits, host limits, swap, network or neighboring containers.

Synthetic /health/live traffic runs only when VG_CAPACITY_LOAD_AUTHORIZED=YES and an allowed VG_CAPACITY_API_BASE is provided. Concurrent downloader+ASR load is a separate gate and remains BLOCKED unless an explicitly authorized stage job driver supplies evidence. If either load gate is missing or capacity is uncertain, the recommendation is DO_NOT_INCREASE_CONCURRENCY.

Disk exhaustion forecasting requires a measured time-series growth rate in VG_CAPACITY_DISK_GROWTH_BYTES_PER_HOUR; without it the forecast is explicitly BLOCKED rather than invented.

## Readiness

/health/live reports only process liveness.

/health/ready checks:

- database/migration availability;
- worker capability configuration;
- auth issuer configuration;
- worker heartbeat freshness when jobs are active;
- free disk threshold.

A dependency exception increments the aggregate dependency failure counter. Readiness never returns secrets.

## Retention safety

Retention remains disabled unless every gate passes simultaneously:

- VG_RETENTION_ENABLED=true;
- exact owned root is qualified and is not a filesystem root;
- VG_RETENTION_QUALIFIED=true;
- dry-run is clean and less than 24 hours old;
- latest backup is no more than 15 minutes old;
- VG_RETENTION_APPROVED=true.

Cleanup never traverses desktop/user output roots. An unsuccessful cleanup ACK increments videograbber_retention_cleanup_failures_total. Production retention requires an exact approved root and policy; broad cleanup is not authorized by the staging files.
