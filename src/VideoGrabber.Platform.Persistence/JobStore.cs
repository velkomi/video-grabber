using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Jobs;

namespace VideoGrabber.Platform.Persistence;

public sealed class JobRequestConflictException : Exception;
public sealed class JobFenceConflictException : Exception;
public sealed class JobUnavailableException : Exception;

public sealed class JobStore(CreditLedger ledger, TimeProvider clock)
{
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RuntimeLimit = TimeSpan.FromHours(2);
    private const int MaxAttempts = 3;
    private readonly NpgsqlDataSource _dataSource = ledger.DataSource;
    private readonly Action<string>? _fault;

    private JobStore(CreditLedger ledger, TimeProvider clock, Action<string>? fault)
        : this(ledger, clock)
    {
        _fault = fault;
    }

    public static JobStore CreateForTesting(
        CreditLedger ledger,
        TimeProvider clock,
        Action<string>? fault = null)
        => new(ledger, clock, fault);

    public async Task<JobView> CreateAsync(
        Guid accountId,
        CreateJob request,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);
        var expectedHash = JobRequestHasher.Hash(request);
        if (!string.Equals(expectedHash, request.RequestHash, StringComparison.Ordinal))
            throw new JobRequestConflictException();
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        await ValidateSourceAsync(connection, transaction, accountId, request, now, cancellationToken);
        await ValidateOwnedInputsAsync(connection, transaction, accountId, request, cancellationToken);

        var existing = await ReadByIntentAsync(
            connection, transaction, accountId, request.IntentId, cancellationToken);

        if (existing is not null)
        {
            if (!existing.Matches(request)) throw new JobRequestConflictException();
            await transaction.CommitAsync(cancellationToken);
            return existing.ToView();
        }

        ReservationReceipt? reservation = null;
        var state = request.Executor == "server_worker" ? "queued" : "waiting_for_worker";
        if (request.Executor == "server_worker")
        {
            reservation = await ledger.ReserveInTransactionAsync(
                connection, transaction, accountId, ReservationFor(request), cancellationToken);
            _fault?.Invoke("after_reservation");
        }

        var jobId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            insert into licensing.jobs(
              job_id,account_id,intent_id,reservation_id,request_hash,kind,executor,device_id,
              source_id,quality,input_artifact_ids,trim_start_ms,trim_duration_ms,state,fence,
              reason,created_at,updated_at)
            values(@job,@account,@intent,@reservation,@hash,@kind,@executor,@device,
              @source,@quality,@inputs,@trim_start,@trim_duration,@state,0,@reason,@now,@now)
            """, connection, transaction))

        {
            insert.Parameters.AddWithValue("job", jobId);
            insert.Parameters.AddWithValue("account", accountId);
            insert.Parameters.AddWithValue("intent", request.IntentId);
            insert.Parameters.AddWithValue("reservation",
                (object?)reservation?.ReservationId ?? DBNull.Value);
            insert.Parameters.AddWithValue("hash", request.RequestHash);
            insert.Parameters.AddWithValue("kind", request.Kind);
            insert.Parameters.AddWithValue("executor", request.Executor);
            insert.Parameters.AddWithValue("device", (object?)request.DeviceId ?? DBNull.Value);
            insert.Parameters.AddWithValue("source", request.SourceId);
            insert.Parameters.AddWithValue("quality", request.Quality);
            insert.Parameters.AddWithValue("inputs", request.InputArtifactIds ?? []);
            insert.Parameters.AddWithValue("trim_start", (object?)request.TrimStartMs ?? DBNull.Value);
            insert.Parameters.AddWithValue("trim_duration", (object?)request.TrimDurationMs ?? DBNull.Value);
            insert.Parameters.AddWithValue("state", state);
            insert.Parameters.AddWithValue("reason", state);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertOutboxAsync(connection, transaction, accountId, jobId,
            state == "queued" ? "job_queued" : "job_waiting_for_worker",
            new { state, request.Executor }, now, cancellationToken);

        _fault?.Invoke("after_job_insert");
        await transaction.CommitAsync(cancellationToken);
        return new JobView(jobId, accountId, request.IntentId, state,
            request.Executor, null, state);
    }

    public async Task<AttemptLease?> ClaimAsync(
        Guid workerId,
        Guid? accountId,
        Guid? deviceId,
        CancellationToken cancellationToken)
    {
        if (workerId == Guid.Empty)
            throw new ArgumentException("Worker id is required.", nameof(workerId));
        if ((accountId is null) != (deviceId is null))
            throw new ArgumentException("Desktop claim requires account and device together.");
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await RecoverExpiredAttemptsAsync(connection, transaction, now, cancellationToken);
        var executor = accountId is null ? "server_worker" : "desktop_worker";
        var state = accountId is null ? "queued" : "waiting_for_worker";
        var job = await ClaimCandidateAsync(
            connection, transaction, executor, state, accountId, deviceId, cancellationToken);

        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (await CountAttemptsAsync(connection, transaction, job.JobId, cancellationToken)
            >= MaxAttempts)
        {
            await MarkReviewRequiredAsync(
                connection, transaction, job, "retry_exhausted", now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var reservationId = job.ReservationId;
        if (executor == "desktop_worker" && reservationId is null)
        {
            try
            {
                var reservation = await ledger.ReserveInTransactionAsync(
                    connection, transaction, job.AccountId,
                    ReservationFor(job.Work), cancellationToken);
                reservationId = reservation.ReservationId;
            }
            catch (ReservationUnavailableException)
            {
                await UpdateReasonAsync(
                    connection, transaction, job.JobId, "access_unavailable", now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
        }

        var attemptId = Guid.NewGuid();
        var capability = Base64Url(RandomNumberGenerator.GetBytes(32));
        var fence = job.Fence + 1;
        var leaseUntil = now.Add(LeaseLifetime);
        var runtimeDeadline = now.Add(RuntimeLimit);
        await using (var update = new NpgsqlCommand("""
            update licensing.jobs
            set state='running',fence=@fence,reservation_id=@reservation,
                reason='running',updated_at=@now
            where job_id=@job and state=@expected
            """, connection, transaction))
        {
            update.Parameters.AddWithValue("fence", fence);
            update.Parameters.AddWithValue("reservation",
                (object?)reservationId ?? DBNull.Value);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("job", job.JobId);
            update.Parameters.AddWithValue("expected", state);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new JobFenceConflictException();
        }

        await using (var attempt = new NpgsqlCommand("""
            insert into licensing.job_attempts(
              attempt_id,job_id,worker_id,fence,capability_hash,lease_until,runtime_deadline,
              state,started_at,heartbeat_at)

            values(@attempt,@job,@worker,@fence,@capability,@lease,@deadline,
              'running',@now,@now)
            """, connection, transaction))
        {
            attempt.Parameters.AddWithValue("attempt", attemptId);
            attempt.Parameters.AddWithValue("job", job.JobId);
            attempt.Parameters.AddWithValue("worker", workerId);
            attempt.Parameters.AddWithValue("fence", fence);
            attempt.Parameters.AddWithValue("capability", Hash(capability));
            attempt.Parameters.AddWithValue("lease", leaseUntil);
            attempt.Parameters.AddWithValue("deadline", runtimeDeadline);
            attempt.Parameters.AddWithValue("now", now);
            await attempt.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertOutboxAsync(connection, transaction, job.AccountId, job.JobId,
            "job_started", new { attemptId, fence, workerId }, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AttemptLease(
            job.JobId, attemptId, fence, leaseUntil, capability, job.Work);
    }

    public async Task<bool> ValidateDesktopAttemptAsync(
        Guid accountId,
        Guid deviceId,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || deviceId == Guid.Empty
            || lease.JobId == Guid.Empty || lease.AttemptId == Guid.Empty
            || lease.Fence <= 0 || string.IsNullOrWhiteSpace(lease.CapabilityToken))
            return false;
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select exists(
              select 1
              from licensing.jobs j
              join licensing.job_attempts a on a.job_id=j.job_id
              where j.job_id=@job and j.account_id=@account
                and j.executor='desktop_worker' and j.device_id=@device
                and j.state in ('running','cancel_requested')
                and j.fence=@fence
                and a.attempt_id=@attempt and a.fence=@fence
                and a.state='running' and a.lease_until>@now
                and a.capability_hash=@capability)
            """, connection);
        command.Parameters.AddWithValue("job", lease.JobId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("fence", lease.Fence);
        command.Parameters.AddWithValue("attempt", lease.AttemptId);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("capability", Hash(lease.CapabilityToken));
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
    public async Task<IReadOnlyList<WorkerArtifactDescriptor>> ReadWorkerArtifactsAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.JobId == Guid.Empty || lease.AttemptId == Guid.Empty || lease.Fence <= 0
            || string.IsNullOrWhiteSpace(lease.CapabilityToken))
            throw new JobFenceConflictException();
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var scope = new NpgsqlCommand("""
            select j.account_id,j.input_artifact_ids
            from licensing.jobs j
            join licensing.job_attempts a on a.job_id=j.job_id
            where j.job_id=@job and j.fence=@fence and j.state in ('running','cancel_requested')
              and a.attempt_id=@attempt and a.fence=@fence and a.state='running'
              and a.lease_until>@now and a.capability_hash=@capability
            """, connection, transaction);
        scope.Parameters.AddWithValue("job", lease.JobId);
        scope.Parameters.AddWithValue("attempt", lease.AttemptId);
        scope.Parameters.AddWithValue("fence", lease.Fence);
        scope.Parameters.AddWithValue("now", now);
        scope.Parameters.AddWithValue("capability", Hash(lease.CapabilityToken));
        await using var reader = await scope.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new JobFenceConflictException();
        var accountId = reader.GetGuid(0);
        var ids = reader.GetFieldValue<Guid[]>(1);
        await reader.DisposeAsync();
        if (ids.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }
        await using var command = new NpgsqlCommand("""
            select artifact_id,storage_path,media_type,sha256,bytes
            from licensing.artifacts
            where account_id=@account and artifact_id=any(@ids) and storage_path is not null
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("ids", ids);
        await using var artifacts = await command.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<Guid,WorkerArtifactDescriptor>();
        while (await artifacts.ReadAsync(cancellationToken))
        {
            var item = new WorkerArtifactDescriptor(
                artifacts.GetGuid(0), artifacts.GetString(1), artifacts.GetString(2),
                artifacts.GetString(3), artifacts.GetInt64(4));
            map[item.ArtifactId] = item;
        }
        await artifacts.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        if (map.Count != ids.Length) throw new JobUnavailableException();
        return ids.Select(id => map[id]).ToArray();
    }
    public async Task<bool> HeartbeatAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.JobId == Guid.Empty || lease.AttemptId == Guid.Empty || lease.Fence <= 0
            || string.IsNullOrWhiteSpace(lease.CapabilityToken))
            return false;

        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.job_attempts a
            set heartbeat_at=@now,lease_until=least(@lease,a.runtime_deadline)
            where a.attempt_id=@attempt and a.job_id=@job and a.fence=@fence
              and a.capability_hash=@capability and a.state='running'
              and a.lease_until>@now
              and exists(
                select 1 from licensing.jobs j
                where j.job_id=a.job_id
                  and j.state in ('running','cancel_requested')
                  and j.fence=a.fence)
            """, connection);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease", now.Add(LeaseLifetime));
        command.Parameters.AddWithValue("attempt", lease.AttemptId);
        command.Parameters.AddWithValue("job", lease.JobId);
        command.Parameters.AddWithValue("fence", lease.Fence);
        command.Parameters.AddWithValue("capability", Hash(lease.CapabilityToken));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<JobView> CompleteAsync(
        AttemptCompletion completion,
        CancellationToken cancellationToken)
    {
        ValidateCompletion(completion);

        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var job = await ReadByIdForUpdateAsync(
            connection, transaction, completion.JobId, null, cancellationToken)
            ?? throw new JobFenceConflictException();
        var attempt = await ReadAttemptAsync(
            connection, transaction, completion.AttemptId, cancellationToken);
        if (job.State == "completed" && attempt is not null
            && attempt.Fence == completion.Fence
            && attempt.State == "completed"
            && string.Equals(attempt.EvidenceId, completion.EvidenceId,
                StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return job.ToView();
        }
        if (attempt is null
            || attempt.JobId != job.JobId
            || attempt.Fence != completion.Fence
            || job.Fence != completion.Fence
            || attempt.State != "running"
            || job.State is not ("running" or "cancel_requested"))
            throw new JobFenceConflictException();
        if (attempt.LeaseUntil <= now || attempt.RuntimeDeadline <= now)
            throw new JobFenceConflictException();
        if (job.ReservationId is not Guid reservationId)
            throw new InvalidDataException("Running job has no reservation.");

        if (completion.Outcome == "success")
        {
            var artifact = completion.Artifact!;
            await InsertArtifactAsync(
                connection, transaction, job, attempt, artifact, now, cancellationToken);
            await ledger.FinalizeInTransactionAsync(
                connection, transaction, job.AccountId,
                new FinalizeReservation(
                    reservationId, attempt.AttemptId, attempt.Fence,
                    "success", completion.EvidenceId),
                cancellationToken);
            await UpdateAttemptTerminalAsync(
                connection, transaction, attempt.AttemptId,
                "completed", completion.Outcome, completion.EvidenceId,
                now, cancellationToken);
            await using var update = new NpgsqlCommand("""
                update licensing.jobs
                set state='completed',artifact_id=@artifact,
                    reason='verified_output',updated_at=@now
                where job_id=@job and fence=@fence
                  and state in ('running','cancel_requested')
                """, connection, transaction);
            update.Parameters.AddWithValue("artifact", artifact.ArtifactId);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("job", job.JobId);
            update.Parameters.AddWithValue("fence", completion.Fence);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new JobFenceConflictException();

            await InsertOutboxAsync(
                connection, transaction, job.AccountId, job.JobId,
                "job_completed",
                new { artifactId = artifact.ArtifactId, completion.EvidenceId },
                now, cancellationToken);
        }
        else if (completion.Outcome == "cancelled"
                 && job.State == "cancel_requested")
        {
            await ledger.ReleaseUnstartedInTransactionAsync(
                connection, transaction, reservationId, cancellationToken);
            await UpdateAttemptTerminalAsync(
                connection, transaction, attempt.AttemptId,
                "cancelled", completion.Outcome, completion.EvidenceId,
                now, cancellationToken);
            await SetJobStateAsync(
                connection, transaction, job.JobId, completion.Fence,
                "cancelled", "cancelled", now, cancellationToken);
            await InsertOutboxAsync(
                connection, transaction, job.AccountId, job.JobId,
                "job_cancelled", new { completion.EvidenceId },
                now, cancellationToken);
        }
        else
        {
            var attempts = await CountAttemptsAsync(
                connection, transaction, job.JobId, cancellationToken);
            await UpdateAttemptTerminalAsync(
                connection, transaction, attempt.AttemptId,
                "failed", completion.Outcome, completion.EvidenceId,
                now, cancellationToken);

            if (completion.Outcome == "review_required"
                || attempts >= MaxAttempts)
            {
                await ledger.FinalizeInTransactionAsync(
                    connection, transaction, job.AccountId,
                    new FinalizeReservation(
                        reservationId, attempt.AttemptId, attempt.Fence,
                        "review_required", completion.EvidenceId),
                    cancellationToken);
                await SetJobStateAsync(
                    connection, transaction, job.JobId, completion.Fence,
                    "review_required", "review_required", now, cancellationToken);
            }
            else
            {
                var nextState = job.Executor == "server_worker"
                    ? "queued"
                    : "waiting_for_worker";
                await SetJobStateAsync(
                    connection, transaction, job.JobId, completion.Fence,
                    nextState, "retry_after_failure", now, cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredAsync(
            job.AccountId, job.JobId, cancellationToken);
    }

    public async Task<JobView?> GetAsync(
        Guid accountId,
        Guid jobId,
        CancellationToken cancellationToken)

    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var row = await ReadByIdAsync(
            connection, accountId, jobId, cancellationToken);
        return row?.ToView();
    }

    public async Task<IReadOnlyList<JobView>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select job_id,account_id,intent_id,reservation_id,request_hash,kind,
              executor,device_id,source_id,quality,input_artifact_ids,
              trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason
            from licensing.jobs
            where account_id=@account
            order by created_at,job_id
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<JobView>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadJob(reader).ToView());
        return result;
    }

    public async Task<QueueOrderView> ReorderAsync(
        Guid accountId,
        QueueOrder request,
        CancellationToken cancellationToken)
    {
        if (request.Version <= 0 || request.JobIds is null
            || request.JobIds.Length == 0
            || request.JobIds.Distinct().Count() != request.JobIds.Length)
            throw new JobRequestConflictException();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var read = new NpgsqlCommand("""
            select job_id,state,queue_version
            from licensing.jobs
            where account_id=@account and job_id=any(@ids)
            order by created_at,job_id
            for update
            """, connection, transaction);
        read.Parameters.AddWithValue("account", accountId);
        read.Parameters.AddWithValue("ids", request.JobIds);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        var states = new Dictionary<Guid,(string State,long Version)>();
        while (await reader.ReadAsync(cancellationToken))
            states[reader.GetGuid(0)] = (reader.GetString(1), reader.GetInt64(2));
        await reader.DisposeAsync();
        if (states.Count != request.JobIds.Length
            || states.Values.Any(x => x.State is not ("queued" or "waiting_for_worker"))
            || states.Values.Any(x => x.Version != request.Version))
            throw new JobRequestConflictException();
        var nextVersion = checked(request.Version + 1);
        for (var i = 0; i < request.JobIds.Length; i++)
        {
            await using var update = new NpgsqlCommand("""
                update licensing.jobs
                set queue_order=@order,queue_version=@version,updated_at=@now
                where account_id=@account and job_id=@job
                  and state in ('queued','waiting_for_worker')
                """, connection, transaction);
            update.Parameters.AddWithValue("order", (long)i + 1);
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("now", clock.GetUtcNow());
            update.Parameters.AddWithValue("account", accountId);
            update.Parameters.AddWithValue("job", request.JobIds[i]);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new JobRequestConflictException();
        }
        await transaction.CommitAsync(cancellationToken);
        return new QueueOrderView(nextVersion, request.JobIds);
    }

    public async Task<IReadOnlyList<JobEvent>> ReadEventsAsync(
        Guid accountId,
        Guid? afterEventId,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        DateTimeOffset? afterCreated = null;
        if (afterEventId is Guid cursor)
        {
            await using var cursorCommand = new NpgsqlCommand(
                "select created_at from licensing.job_outbox where account_id=@account and event_id=@event",
                connection);
            cursorCommand.Parameters.AddWithValue("account", accountId);
            cursorCommand.Parameters.AddWithValue("event", cursor);
            var value = await cursorCommand.ExecuteScalarAsync(cancellationToken);
            if (value is DateTimeOffset timestamp) afterCreated = timestamp;
        }
        await using var command = new NpgsqlCommand("""
            select event_id,job_id,event_type,created_at,payload::text
            from licensing.job_outbox
            where account_id=@account
              and (@after::timestamptz is null or created_at>@after
                   or (created_at=@after and event_id>@cursor))
            order by created_at,event_id
            limit @limit
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("after", (object?)afterCreated ?? DBNull.Value);
        command.Parameters.AddWithValue("cursor", (object?)afterEventId ?? Guid.Empty);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<JobEvent>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new JobEvent(
                reader.GetGuid(0).ToString("N"), reader.GetGuid(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4)));
        return result;
    }
    public async Task<JobView> CancelAsync(
        Guid accountId,
        Guid jobId,
        CancellationToken cancellationToken)

    {
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var job = await ReadByIdForUpdateAsync(
            connection, transaction, jobId, accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Job was not found.");
        if (job.State is "completed" or "cancelled" or "review_required")
        {
            await transaction.CommitAsync(cancellationToken);
            return job.ToView();
        }
        if (job.State is "queued" or "waiting_for_worker")
        {
            if (job.ReservationId is Guid reservationId)
                await ledger.ReleaseUnstartedInTransactionAsync(
                    connection, transaction, reservationId, cancellationToken);
            await SetJobStateAsync(
                connection, transaction, job.JobId, job.Fence,
                "cancelled", "cancelled_before_start", now, cancellationToken);
        }
        else if (job.State == "running")
        {
            await SetJobStateAsync(
                connection, transaction, job.JobId, job.Fence,
                "cancel_requested", "cancel_requested", now, cancellationToken);
        }
        await InsertOutboxAsync(
            connection, transaction, job.AccountId, job.JobId,
            "job_cancel_requested", new { job.State }, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredAsync(accountId, jobId, cancellationToken);
    }

    public async Task<JobView> RetryAsync(
        Guid accountId,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var job = await ReadByIdForUpdateAsync(
            connection, transaction, jobId, accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Job was not found.");
        if (job.State != "failed"
            || await CountAttemptsAsync(
                connection, transaction, jobId, cancellationToken) >= MaxAttempts)
            throw new JobRequestConflictException();
        var next = job.Executor == "server_worker"
            ? "queued"
            : "waiting_for_worker";
        await SetJobStateAsync(
            connection, transaction, jobId, job.Fence,
            next, "manual_retry", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredAsync(accountId, jobId, cancellationToken);
    }

    private async Task RecoverExpiredAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select a.attempt_id,a.job_id,a.fence
            from licensing.job_attempts a
            join licensing.jobs j
              on j.job_id=a.job_id and j.fence=a.fence
            where a.state='running'
              and a.lease_until<=@now
              and j.state in ('running','cancel_requested')
            order by a.started_at,a.attempt_id
            for update of a,j
            """, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var expired = new List<(Guid AttemptId, Guid JobId, long Fence)>();
        while (await reader.ReadAsync(cancellationToken))
            expired.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2)));
        await reader.DisposeAsync();

        foreach (var item in expired)
        {
            var job = await ReadByIdForUpdateAsync(
                connection, transaction, item.JobId, null, cancellationToken);
            if (job is null
                || job.Fence != item.Fence
                || job.State is not ("running" or "cancel_requested"))
                continue;

            await UpdateAttemptTerminalAsync(
                connection, transaction, item.AttemptId,
                "expired", "lease_expired", "lease_expired",
                now, cancellationToken);
            var attempts = await CountAttemptsAsync(
                connection, transaction, item.JobId, cancellationToken);
            if (job.State == "cancel_requested"
                && job.ReservationId is Guid cancelReservation)
            {
                await ledger.ReleaseUnstartedInTransactionAsync(
                    connection, transaction, cancelReservation, cancellationToken);
                await SetJobStateAsync(
                    connection, transaction, job.JobId, job.Fence,
                    "cancelled", "cancelled_after_lease_expiry",
                    now, cancellationToken);
            }
            else if (attempts >= MaxAttempts)
            {
                await MarkReviewRequiredAsync(
                    connection, transaction, job,
                    "retry_exhausted", now, cancellationToken);
            }
            else
            {
                var next = job.Executor == "server_worker"
                    ? "queued"
                    : "waiting_for_worker";
                await SetJobStateAsync(
                    connection, transaction, job.JobId, job.Fence,
                    next, "lease_expired", now, cancellationToken);
            }
        }
    }

    private async Task MarkReviewRequiredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JobRow job,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var attempt = await ReadLatestAttemptAsync(
            connection, transaction, job.JobId, cancellationToken);
        if (attempt is not null && job.ReservationId is Guid reservationId)
        {
            await ledger.FinalizeInTransactionAsync(
                connection, transaction, job.AccountId,
                new FinalizeReservation(
                    reservationId, attempt.AttemptId, attempt.Fence,
                    "review_required", reason),
                cancellationToken);
        }
        await SetJobStateAsync(
            connection, transaction, job.JobId, job.Fence,
            "review_required", reason, now, cancellationToken);
    }

    private static async Task<JobRow?> ClaimCandidateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string executor,
        string state,
        Guid? accountId,
        Guid? deviceId,
        CancellationToken cancellationToken)
    {

        const string sql = """
            select job_id,account_id,intent_id,reservation_id,request_hash,kind,
              executor,device_id,source_id,quality,input_artifact_ids,
              trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason
            from licensing.jobs
            where state=@state and executor=@executor
              and (@account::uuid is null or account_id=@account)
              and (@device::uuid is null or device_id=@device)
            order by created_at,job_id
            for update skip locked limit 1
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("executor", executor);
        command.Parameters.AddWithValue("account", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("device", (object?)deviceId ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadJob(reader)
            : null;
    }

    private static async Task ValidateOwnedInputsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CreateJob request,
        CancellationToken cancellationToken)
    {
        if (request.InputArtifactIds.Length == 0) return;
        await using var command = new NpgsqlCommand("""
            select count(*)
            from licensing.artifacts a
            join licensing.jobs j on j.job_id=a.job_id
            where a.account_id=@account
              and a.artifact_id=any(@ids)
              and j.state='completed'
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("ids", request.InputArtifactIds);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (count != request.InputArtifactIds.Length)
            throw new JobUnavailableException();
    }
    private static async Task ValidateSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CreateJob request,
        DateTimeOffset now,
        CancellationToken cancellationToken)

    {
        await using var command = new NpgsqlCommand("""
            select expires_at,qualities
            from licensing.sources
            where source_id=@source and account_id=@account
            """, connection, transaction);
        command.Parameters.AddWithValue("source", request.SourceId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new JobUnavailableException();
        var expires = reader.GetFieldValue<DateTimeOffset>(0);
        var qualitiesJson = reader.GetFieldValue<string>(1);
        if (expires <= now) throw new JobUnavailableException();
        using var qualities = JsonDocument.Parse(qualitiesJson);
        if (qualities.RootElement.ValueKind != JsonValueKind.Array
            || !qualities.RootElement.EnumerateArray().Any(
                x => x.ValueKind == JsonValueKind.String
                     && string.Equals(
                         x.GetString(), request.Quality, StringComparison.Ordinal)))
            throw new JobUnavailableException();
    }

    private static async Task LockAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {

        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@account,20260917))",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ReservationRequest ReservationFor(CreateJob request)
        => new(
            request.IntentId,
            request.RequestHash,
            "download",
            request.Executor,
            request.DeviceId);

    private static async Task<JobRow?> ReadByIntentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select job_id,account_id,intent_id,reservation_id,request_hash,kind,
              executor,device_id,source_id,quality,input_artifact_ids,
              trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason
            from licensing.jobs
            where account_id=@account and intent_id=@intent
            for update
            """, connection, transaction);

        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("intent", intentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadJob(reader)
            : null;
    }

    private static async Task<JobRow?> ReadByIdForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        var sql = accountId is null
            ? "select job_id,account_id,intent_id,reservation_id,request_hash,kind,executor,device_id,source_id,quality,input_artifact_ids,trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason from licensing.jobs where job_id=@job for update"
            : "select job_id,account_id,intent_id,reservation_id,request_hash,kind,executor,device_id,source_id,quality,input_artifact_ids,trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason from licensing.jobs where job_id=@job and account_id=@account for update";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("job", jobId);
        if (accountId is Guid id)
            command.Parameters.AddWithValue("account", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadJob(reader)
            : null;
    }

    private static async Task<JobRow?> ReadByIdAsync(
        NpgsqlConnection connection,
        Guid accountId,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select job_id,account_id,intent_id,reservation_id,request_hash,kind,
              executor,device_id,source_id,quality,input_artifact_ids,
              trim_start_ms,trim_duration_ms,state,fence,artifact_id,reason
            from licensing.jobs
            where job_id=@job and account_id=@account
            """, connection);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadJob(reader)
            : null;
    }

    private static JobRow ReadJob(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetString(4),

            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetFieldValue<Guid[]>(10),
            reader.IsDBNull(11) ? null : reader.GetInt64(11),
            reader.IsDBNull(12) ? null : reader.GetInt64(12),
            reader.GetString(13),
            reader.GetInt64(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15),
            reader.GetString(16));

    private static async Task<AttemptRow?> ReadAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select attempt_id,job_id,fence,lease_until,runtime_deadline,state,evidence_id
            from licensing.job_attempts
            where attempt_id=@attempt
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("attempt", attemptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new AttemptRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private static async Task<AttemptRow?> ReadLatestAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select attempt_id,job_id,fence,lease_until,runtime_deadline,state,evidence_id
            from licensing.job_attempts
            where job_id=@job
            order by fence desc
            limit 1
            """, connection, transaction);
        command.Parameters.AddWithValue("job", jobId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new AttemptRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private static async Task<int> CountAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.job_attempts where job_id=@job",
            connection, transaction);
        command.Parameters.AddWithValue("job", jobId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JobRow job,
        AttemptRow attempt,
        ArtifactReceipt artifact,
        DateTimeOffset now,
        CancellationToken cancellationToken)

    {
        ValidateArtifact(artifact);
        await using var command = new NpgsqlCommand("""
            insert into licensing.artifacts(
              artifact_id,account_id,job_id,attempt_id,fence,sha256,bytes,
              media_type,verification_evidence_id,storage_path,created_at)
            values(@id,@account,@job,@attempt,@fence,@sha,@bytes,
              @media,@evidence,@storage,@now)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", artifact.ArtifactId);
        command.Parameters.AddWithValue("account", job.AccountId);
        command.Parameters.AddWithValue("job", job.JobId);
        command.Parameters.AddWithValue("attempt", attempt.AttemptId);
        command.Parameters.AddWithValue("fence", attempt.Fence);
        command.Parameters.AddWithValue("sha", artifact.Sha256);
        command.Parameters.AddWithValue("bytes", artifact.Bytes);
        command.Parameters.AddWithValue("media", artifact.MediaType);
        command.Parameters.AddWithValue(
            "evidence", artifact.VerificationEvidenceId);
        command.Parameters.AddWithValue("storage", (object?)artifact.StoragePath ?? DBNull.Value);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateAttemptTerminalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        string state,
        string outcome,

        string evidence,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            update licensing.job_attempts
            set state=@state,outcome=@outcome,evidence_id=@evidence,
                completed_at=@now
            where attempt_id=@attempt and state='running'
            """, connection, transaction);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("outcome", outcome);
        command.Parameters.AddWithValue("evidence", evidence);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("attempt", attemptId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new JobFenceConflictException();
    }

    private static async Task SetJobStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        long fence,
        string state,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {

        await using var command = new NpgsqlCommand("""
            update licensing.jobs
            set state=@state,reason=@reason,updated_at=@now
            where job_id=@job and fence=@fence
            """, connection, transaction);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("fence", fence);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new JobFenceConflictException();
    }

    private static async Task UpdateReasonAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "update licensing.jobs set reason=@reason,updated_at=@now where job_id=@job",
            connection, transaction);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("job", jobId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid jobId,
        string eventType,
        object payload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.job_outbox(
              event_id,account_id,job_id,event_type,payload,created_at)
            values(@event,@account,@job,@type,@payload::jsonb,@now)
            """, connection, transaction);
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("type", eventType);
        command.Parameters.AddWithValue(
            "payload", JsonSerializer.Serialize(payload));
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<JobView> GetRequiredAsync(
        Guid accountId,
        Guid jobId,
        CancellationToken cancellationToken)

        => await GetAsync(accountId, jobId, cancellationToken)
            ?? throw new KeyNotFoundException("Job was not found.");

    private static void ValidateCreate(CreateJob request)
    {
        if (request.IntentId == Guid.Empty)
            throw new ArgumentException("Intent id is required.");
        if (request.Kind is not ("download" or "mp3" or "trim" or "join" or "transcribe"))
            throw new ArgumentException("Unsupported media operation.");
        if (request.Executor is not ("server_worker" or "desktop_worker"))
            throw new ArgumentException("Unsupported executor.");
        if (request.Executor == "server_worker" && request.DeviceId is not null)
            throw new ArgumentException("Server worker jobs cannot carry a device id.");
        if (request.Executor == "desktop_worker" && request.DeviceId is null)
            throw new ArgumentException("Desktop worker jobs require a device id.");
        if (string.IsNullOrWhiteSpace(request.SourceId) || request.SourceId.Length > 128)
            throw new ArgumentException("Source id is invalid.");
        if (string.IsNullOrWhiteSpace(request.Quality) || request.Quality.Length > 32)
            throw new ArgumentException("Quality is invalid.");
        if (request.InputArtifactIds is null || request.InputArtifactIds.Length > 100
            || request.InputArtifactIds.Distinct().Count() != request.InputArtifactIds.Length)
            throw new ArgumentException("Input artifacts are invalid.");
        if (request.TrimStartMs is < 0 || request.TrimDurationMs is <= 0)
            throw new ArgumentException("Trim bounds are invalid.");
        if (request.Kind == "download" && request.InputArtifactIds.Length != 0)
            throw new ArgumentException("Download cannot use input artifacts.");
        if (request.Kind == "mp3" && request.InputArtifactIds.Length > 1)
            throw new ArgumentException("MP3 accepts at most one input artifact.");
        if (request.Kind is "trim" or "transcribe" && request.InputArtifactIds.Length != 1)
            throw new ArgumentException("Operation requires exactly one owned input artifact.");
        if (request.Kind == "join" && request.InputArtifactIds.Length < 2)
            throw new ArgumentException("Join requires at least two owned input artifacts.");
        if (request.Kind == "trim" && (request.TrimStartMs is null || request.TrimDurationMs is null))
            throw new ArgumentException("Trim requires start and duration.");
        if (request.Kind != "trim" && (request.TrimStartMs is not null || request.TrimDurationMs is not null))
            throw new ArgumentException("Trim bounds are only valid for trim jobs.");
    }
    private static void ValidateCompletion(AttemptCompletion completion)
    {
        if (completion.JobId == Guid.Empty
            || completion.AttemptId == Guid.Empty
            || completion.Fence <= 0
            || string.IsNullOrWhiteSpace(completion.EvidenceId))
            throw new ArgumentException("Completion is incomplete.");
        if (completion.Outcome is not (
            "success" or "failed" or "review_required" or "cancelled"))
            throw new ArgumentException("Unsupported completion outcome.");
        if (completion.Outcome == "success")
        {
            if (completion.Artifact is null)
                throw new ArgumentException(
                    "Successful completion requires artifact.");
            if (!string.Equals(
                    completion.Artifact.VerificationEvidenceId,
                    completion.EvidenceId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Artifact evidence does not match completion evidence.");
        }
        else if (completion.Artifact is not null)
        {
            throw new ArgumentException(
                "Non-success completion cannot register an artifact.");
        }
    }

    private static void ValidateArtifact(ArtifactReceipt artifact)
    {

        if (artifact.ArtifactId == Guid.Empty
            || artifact.Bytes <= 0
            || string.IsNullOrWhiteSpace(artifact.MediaType)
            || artifact.MediaType.Length > 128
            || string.IsNullOrWhiteSpace(
                artifact.VerificationEvidenceId)
            || artifact.Sha256.Length != 64
            || artifact.Sha256.Any(
                ch => !char.IsAsciiHexDigit(ch)))
            throw new ArgumentException("Artifact receipt is invalid.");
    }

    private static string Hash(string value)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record JobRow(
        Guid JobId,
        Guid AccountId,
        Guid IntentId,
        Guid? ReservationId,
        string RequestHash,
        string Kind,
        string Executor,
        Guid? DeviceId,

        string SourceId,
        string Quality,
        Guid[] InputArtifactIds,
        long? TrimStartMs,
        long? TrimDurationMs,
        string State,
        long Fence,
        Guid? ArtifactId,
        string Reason)
    {
        public CreateJob Work => new(
            IntentId, RequestHash, Kind, Executor, DeviceId,
            SourceId, Quality, InputArtifactIds,
            TrimStartMs, TrimDurationMs);

        public bool Matches(CreateJob request)
            => string.Equals(
                   RequestHash, request.RequestHash, StringComparison.Ordinal)
               && string.Equals(
                   Kind, request.Kind, StringComparison.Ordinal)
               && string.Equals(
                   Executor, request.Executor, StringComparison.Ordinal)
               && DeviceId == request.DeviceId
               && string.Equals(
                   SourceId, request.SourceId, StringComparison.Ordinal)
               && string.Equals(
                   Quality, request.Quality, StringComparison.Ordinal)
               && InputArtifactIds.SequenceEqual(
                   request.InputArtifactIds ?? [])
               && TrimStartMs == request.TrimStartMs
               && TrimDurationMs == request.TrimDurationMs;

        public JobView ToView()
            => new(
                JobId,
                AccountId,
                IntentId,
                State,
                Executor,
                ArtifactId,
                Reason);
    }

    private sealed record AttemptRow(
        Guid AttemptId,
        Guid JobId,
        long Fence,
        DateTimeOffset LeaseUntil,
        DateTimeOffset RuntimeDeadline,
        string State,
        string? EvidenceId);
}
