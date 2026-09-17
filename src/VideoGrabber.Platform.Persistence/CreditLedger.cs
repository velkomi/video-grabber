using System.Data;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class ReservationConflictException : Exception;
public sealed class ReservationUnavailableException : Exception;
public sealed class LedgerBusyException : Exception;

public sealed class CreditLedger : IAsyncDisposable
{
    private static readonly TimeSpan HoldLifetime = TimeSpan.FromMinutes(10);
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly bool _ownsDataSource;

    private CreditLedger(NpgsqlDataSource dataSource, TimeProvider clock, bool ownsDataSource)
    {
        _dataSource = dataSource;
        _clock = clock;
        _ownsDataSource = ownsDataSource;
    }

    public static CreditLedger CreateOwned(string ledgerDsn, TimeProvider clock)
    {
        var builder = new NpgsqlConnectionStringBuilder(ledgerDsn);
        builder.MaxPoolSize = Math.Min(builder.MaxPoolSize, 32);
        return new(NpgsqlDataSource.Create(builder.ConnectionString), clock, true);
    }

    public static CreditLedger CreateForTesting(NpgsqlDataSource dataSource, TimeProvider clock)
        => new(dataSource, clock, false);
    public async Task<ReservationReceipt> ReserveAsync(
        Guid accountId,
        ReservationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateReservation(request);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await ReserveOnceAsync(accountId, request, cancellationToken); }
            catch (PostgresException ex) when (attempt < 3 &&
                ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(15 * attempt), cancellationToken);
            }
        }
        throw new LedgerBusyException();
    }

    private async Task<ReservationReceipt> ReserveOnceAsync(
        Guid accountId, ReservationRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);

        var existing = await ReadReservationByIntentAsync(
            connection, transaction, accountId, request.IntentId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(request)) throw new ReservationConflictException();
            await transaction.CommitAsync(cancellationToken);
            return existing.ToReceipt();
        }

        var account = await ReadAccountAsync(connection, transaction, accountId, cancellationToken)
            ?? throw new ReservationUnavailableException();
        if (account.Blocked) throw new ReservationUnavailableException();

        var expiresAt = now.Add(HoldLifetime);
        if (account.Role == "owner_admin" ||
            await HasTimedAccessAsync(connection, transaction, accountId, now, cancellationToken))
        {
            var receipt = await InsertReservationAsync(connection, transaction, accountId,
                request, null, false, expiresAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return receipt;
        }

        var grantId = await LockCreditGrantAsync(
            connection, transaction, accountId, now, cancellationToken)
            ?? throw new ReservationUnavailableException();
        await using (var decrement = new NpgsqlCommand("""
            update licensing.entitlement_grants
            set available=available-1,reserved=reserved+1
            where grant_id=@grant and available>0
            """, connection, transaction))
        {
            decrement.Parameters.AddWithValue("grant", grantId);
            if (await decrement.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ReservationUnavailableException();
        }

        var reserved = await InsertReservationAsync(connection, transaction, accountId,
            request, grantId, true, expiresAt, cancellationToken);
        await InsertLedgerEventAsync(connection, transaction, accountId, grantId,
            reserved.ReservationId, "reserve", -1, +1, 0, 0, null,
            "credit reserved", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return reserved;
    }

    public async Task<ReservationReceipt> FinalizeAsync(
        Guid accountId,
        FinalizeReservation finalize,
        CancellationToken cancellationToken)
    {
        if (finalize.ReservationId == Guid.Empty || finalize.AttemptId == Guid.Empty ||
            finalize.Fence <= 0 || string.IsNullOrWhiteSpace(finalize.EvidenceId))
            throw new ArgumentException("Finalize reservation payload is incomplete.");
        if (finalize.Outcome is not ("success" or "review_required"))
            throw new ArgumentException("Unsupported reservation outcome.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        var row = await ReadReservationByIdAsync(
            connection, transaction, accountId, finalize.ReservationId, cancellationToken)
            ?? throw new ReservationConflictException();

        if (row.State != "reserved")
        {
            if (row.State == "completed" && row.Matches(finalize))
            {
                await transaction.CommitAsync(cancellationToken);
                return row.ToReceipt();
            }
            throw new ReservationConflictException();
        }

        var nextState = finalize.Outcome == "success" ? "completed" : "review_required";
        if (finalize.Outcome == "success" && row.UsesCredit)
        {
            if (row.GrantId is not Guid grantId) throw new InvalidDataException("Credit reservation has no grant.");
            await LockGrantAsync(connection, transaction, grantId, cancellationToken);
            await using var spend = new NpgsqlCommand("""
                update licensing.entitlement_grants
                set reserved=reserved-1 where grant_id=@grant and reserved>0
                """, connection, transaction);
            spend.Parameters.AddWithValue("grant", grantId);
            if (await spend.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Reserved credit bucket is inconsistent.");
            await InsertLedgerEventAsync(connection, transaction, accountId, grantId,
                row.ReservationId, "commit", 0, -1, +1, 0, finalize.EvidenceId,
                "verified output committed", cancellationToken);
        }

        await using (var update = new NpgsqlCommand("""
            update licensing.reservations
            set state=@state,attempt_id=@attempt,fence=@fence,evidence_id=@evidence,finalized_at=@now
            where reservation_id=@reservation and account_id=@account and state='reserved'
            """, connection, transaction))
        {
            update.Parameters.AddWithValue("state", nextState);
            update.Parameters.AddWithValue("attempt", finalize.AttemptId);
            update.Parameters.AddWithValue("fence", finalize.Fence);
            update.Parameters.AddWithValue("evidence", finalize.EvidenceId);
            update.Parameters.AddWithValue("now", _clock.GetUtcNow());
            update.Parameters.AddWithValue("reservation", finalize.ReservationId);
            update.Parameters.AddWithValue("account", accountId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ReservationConflictException();
        }

        await transaction.CommitAsync(cancellationToken);
        return row.ToReceipt(nextState, finalize.AttemptId, finalize.Fence, finalize.EvidenceId);
    }

    public async Task<bool> ReleaseUnstartedAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty) return false;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var row = await ReadReservationByIdAsync(connection, transaction, null, reservationId, cancellationToken);
        if (row is null || row.State != "reserved") return false;
        await LockAccountAsync(connection, transaction, row.AccountId, cancellationToken);

        if (row.UsesCredit)
        {
            if (row.GrantId is not Guid grantId) throw new InvalidDataException("Credit reservation has no grant.");
            var grant = await LockGrantAsync(connection, transaction, grantId, cancellationToken);
            var now = _clock.GetUtcNow();
            var active = grant is not null && !grant.Revoked && grant.StartsAt <= now &&
                (grant.EndsAt is null || now < grant.EndsAt);
            await using var adjust = new NpgsqlCommand(active
                ? "update licensing.entitlement_grants set reserved=reserved-1,available=available+1 where grant_id=@grant and reserved>0"
                : "update licensing.entitlement_grants set reserved=reserved-1 where grant_id=@grant and reserved>0",
                connection, transaction);
            adjust.Parameters.AddWithValue("grant", grantId);
            if (await adjust.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Reserved credit bucket is inconsistent.");
            await InsertLedgerEventAsync(connection, transaction, row.AccountId, grantId,
                row.ReservationId, active ? "release" : "release_void",
                active ? +1 : 0, -1, 0, active ? 0 : +1, null,
                active ? "unstarted reservation released" : "expired or revoked reservation voided",
                cancellationToken);
        }

        await using (var update = new NpgsqlCommand(
            "update licensing.reservations set state='released',finalized_at=@now where reservation_id=@reservation and state='reserved'",
            connection, transaction))
        {
            update.Parameters.AddWithValue("now", _clock.GetUtcNow());
            update.Parameters.AddWithValue("reservation", reservationId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
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

    private static async Task<AccountState?> ReadAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select base_role,blocked_at is not null from licensing.accounts where account_id=@account",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AccountState(reader.GetString(0), reader.GetBoolean(1));
    }

    private static async Task<bool> HasTimedAccessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select exists(
              select 1 from licensing.entitlement_grants
              where account_id=@account and revoked_at is null and valid_from<=@now
                and (valid_until is null or valid_until>@now)
                and kind in ('permanent','time'))
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("now", now);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<Guid?> LockCreditGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select grant_id from licensing.entitlement_grants
            where account_id=@account and kind in ('credits','hybrid')
              and available>0 and revoked_at is null and valid_from<=@now
              and (valid_until is null or valid_until>@now)
            order by valid_until nulls last,created_at,grant_id
            for update limit 1
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("now", now);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task<ReservationReceipt> InsertReservationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        ReservationRequest request,
        Guid? grantId,
        bool usesCredit,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var reservationId = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            insert into licensing.reservations(
              reservation_id,account_id,intent_id,request_hash,operation,executor,device_id,
              grant_id,state,uses_credit,expires_at)
            values(@reservation,@account,@intent,@hash,@operation,@executor,@device,
              @grant,'reserved',@uses,@expires)
            returning expires_at
            """, connection, transaction);
        command.Parameters.AddWithValue("reservation", reservationId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("intent", request.IntentId);
        command.Parameters.AddWithValue("hash", request.RequestHash);
        command.Parameters.AddWithValue("operation", request.Operation);
        command.Parameters.AddWithValue("executor", request.Executor);
        command.Parameters.AddWithValue("device", (object?)request.DeviceId ?? DBNull.Value);
        command.Parameters.AddWithValue("grant", (object?)grantId ?? DBNull.Value);
        command.Parameters.AddWithValue("uses", usesCredit);
        command.Parameters.AddWithValue("expires", expiresAt);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Reservation insert did not return expiry.");
        var storedExpiry = reader.GetFieldValue<DateTimeOffset>(0);
        return new ReservationReceipt(reservationId, request.IntentId, "reserved", usesCredit, storedExpiry);
    }

    private static async Task<ReservationRow?> ReadReservationByIntentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid intentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select reservation_id,account_id,intent_id,request_hash,operation,executor,device_id,
                   grant_id,state,uses_credit,expires_at,attempt_id,fence,evidence_id
            from licensing.reservations
            where account_id=@account and intent_id=@intent
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("intent", intentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadReservationAsync(reader, cancellationToken);
    }

    private static async Task<ReservationRow?> ReadReservationByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? accountId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var sql = accountId is null
            ? "select reservation_id,account_id,intent_id,request_hash,operation,executor,device_id,grant_id,state,uses_credit,expires_at,attempt_id,fence,evidence_id from licensing.reservations where reservation_id=@reservation for update"
            : "select reservation_id,account_id,intent_id,request_hash,operation,executor,device_id,grant_id,state,uses_credit,expires_at,attempt_id,fence,evidence_id from licensing.reservations where reservation_id=@reservation and account_id=@account for update";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("reservation", reservationId);
        if (accountId is Guid account) command.Parameters.AddWithValue("account", account);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadReservationAsync(reader, cancellationToken);
    }

    private static async Task<ReservationRow?> ReadReservationAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ReservationRow(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetString(8), reader.GetBoolean(9),
            reader.GetFieldValue<DateTimeOffset>(10), reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetInt64(12), reader.IsDBNull(13) ? null : reader.GetString(13));
    }

    private static async Task<GrantState?> LockGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid grantId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select valid_from,valid_until,revoked_at is not null
            from licensing.entitlement_grants where grant_id=@grant for update
            """, connection, transaction);
        command.Parameters.AddWithValue("grant", grantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new GrantState(reader.GetFieldValue<DateTimeOffset>(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1), reader.GetBoolean(2));
    }

    private static async Task InsertLedgerEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid? grantId,
        Guid? reservationId,
        string eventKind,
        long availableDelta,
        long reservedDelta,
        long spentDelta,
        long voidDelta,
        string? evidenceId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.credit_ledger(
              ledger_id,account_id,grant_id,reservation_id,event_kind,
              available_delta,reserved_delta,spent_delta,void_delta,evidence_id,reason)
            values(@id,@account,@grant,@reservation,@kind,
              @available,@reserved,@spent,@void,@evidence,@reason)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("grant", (object?)grantId ?? DBNull.Value);
        command.Parameters.AddWithValue("reservation", (object?)reservationId ?? DBNull.Value);
        command.Parameters.AddWithValue("kind", eventKind);
        command.Parameters.AddWithValue("available", availableDelta);
        command.Parameters.AddWithValue("reserved", reservedDelta);
        command.Parameters.AddWithValue("spent", spentDelta);
        command.Parameters.AddWithValue("void", voidDelta);
        command.Parameters.AddWithValue("evidence", (object?)evidenceId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateReservation(ReservationRequest request)
    {
        if (request.IntentId == Guid.Empty) throw new ArgumentException("Intent id is required.");
        if (string.IsNullOrWhiteSpace(request.RequestHash) || request.RequestHash.Length > 256)
            throw new ArgumentException("Request hash is invalid.");
        if (request.Operation != "download") throw new ArgumentException("Unsupported reservation operation.");
        if (request.Executor is not ("server_worker" or "desktop_worker"))
            throw new ArgumentException("Unsupported executor.");
        if (request.Executor == "server_worker" && request.DeviceId is not null)
            throw new ArgumentException("Server reservations do not use a device id.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource) await _dataSource.DisposeAsync();
    }

    private sealed record AccountState(string Role, bool Blocked);
    private sealed record GrantState(DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool Revoked);

    private sealed record ReservationRow(
        Guid ReservationId, Guid AccountId, Guid IntentId, string RequestHash,
        string Operation, string Executor, Guid? DeviceId, Guid? GrantId,
        string State, bool UsesCredit, DateTimeOffset ExpiresAt,
        Guid? AttemptId, long? Fence, string? EvidenceId)
    {
        public bool Matches(ReservationRequest request)
            => RequestHash == request.RequestHash && Operation == request.Operation &&
               Executor == request.Executor && DeviceId == request.DeviceId;

        public bool Matches(FinalizeReservation finalize)
            => AttemptId == finalize.AttemptId && Fence == finalize.Fence &&
               EvidenceId == finalize.EvidenceId && State == "completed" && finalize.Outcome == "success";

        public ReservationReceipt ToReceipt(string? state = null, Guid? attemptId = null,
            long? fence = null, string? evidenceId = null)
            => new(ReservationId, IntentId, state ?? State, UsesCredit, ExpiresAt);
    }
}
