using System.Diagnostics;
using System.Text.Json;
using Npgsql;

namespace VideoGrabber.Platform.Api.Admin;

public sealed class AdminConflictException : Exception;
public sealed record AdminAccountView(Guid AccountId, string Role, bool Blocked,
    DateTimeOffset? FirstPurchaseAt, Guid? MergedInto);
public sealed record AdminAuditEntry(Guid EventId, Guid? ActorAccountId,
    string EventType, string Details, DateTimeOffset CreatedAt);

public sealed class AdminService : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly bool _ownsDataSource;

    private AdminService(NpgsqlDataSource dataSource, TimeProvider clock, bool ownsDataSource)
    {
        _dataSource = dataSource;
        _clock = clock;
        _ownsDataSource = ownsDataSource;
    }

    public static AdminService CreateOwned(string dsn, TimeProvider clock)
        => new(NpgsqlDataSource.Create(dsn), clock, true);

    public static AdminService CreateForTesting(NpgsqlDataSource dataSource, TimeProvider clock)
        => new(dataSource, clock, false);
    public async Task<bool> IsAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "select base_role='owner_admin' and blocked_at is null from licensing.accounts where account_id=@id",
            connection);
        command.Parameters.AddWithValue("id", actorId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task BlockAsync(Guid actorId, Guid targetId, bool blocked, string reason,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        var before = await ReadAccountAsync(connection, transaction, targetId, true, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found.");
        await using (var update = new NpgsqlCommand(
            "update licensing.accounts set blocked_at=@blocked where account_id=@target", connection, transaction))
        {
            update.Parameters.AddWithValue("blocked", blocked ? now : DBNull.Value);
            update.Parameters.AddWithValue("target", targetId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        if (blocked)
        {
            await using var revoke = new NpgsqlCommand(
                "update licensing.api_sessions set revoked_at=coalesce(revoked_at,@now) where account_id=@target",
                connection, transaction);
            revoke.Parameters.AddWithValue("now", now);
            revoke.Parameters.AddWithValue("target", targetId);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }
        await AuditAsync(connection, transaction, actorId, targetId,
            blocked ? "account_blocked" : "account_unblocked",
            new { before = before.Blocked, after = blocked, role = before.Role, reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeGiftAsync(Guid actorId, Guid grantId, string reason,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        await using var select = new NpgsqlCommand(
            "select account_id,kind,source,available,reserved,revoked_at is not null from licensing.entitlement_grants where grant_id=@grant for update",
            connection, transaction);
        select.Parameters.AddWithValue("grant", grantId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("Grant was not found.");
        var accountId = reader.GetGuid(0);
        var kind = reader.GetString(1);
        var source = reader.GetString(2);
        var available = reader.GetInt64(3);
        var reserved = reader.GetInt64(4);
        var alreadyRevoked = reader.GetBoolean(5);
        await reader.DisposeAsync();
        if (source != "admin_gift") throw new AdminConflictException();
        if (alreadyRevoked) { await transaction.CommitAsync(cancellationToken); return; }

        await using (var update = new NpgsqlCommand(
            "update licensing.entitlement_grants set available=0,revoked_at=@now where grant_id=@grant",
            connection, transaction))
        {
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("grant", grantId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        if (available > 0 && kind is "credits" or "hybrid")
            await InsertLedgerAdjustmentAsync(connection, transaction, actorId, accountId, grantId,
                null, -available, 0, 0, available, null, reason, cancellationToken);

        await AuditAsync(connection, transaction, actorId, accountId, "grant_revoked",
            new { grantId, kind, availableBefore = available, reserved, reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task ResetDevicesAsync(Guid actorId, Guid targetId, string reason,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        if (await ReadAccountAsync(connection, transaction, targetId, false, cancellationToken) is null)
            throw new KeyNotFoundException("Account was not found.");
        await using var update = new NpgsqlCommand("""
            update licensing.devices
            set revoked_at=@now,lease_version=lease_version+1
            where account_id=@target and revoked_at is null
            """, connection, transaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("target", targetId);
        var count = await update.ExecuteNonQueryAsync(cancellationToken);
        await AuditAsync(connection, transaction, actorId, targetId, "devices_reset",
            new { count, reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AdjustAsync(Guid actorId, Guid reservationId, string evidenceId,
        string reason, CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        if (evidenceId.Length > 256) throw new ArgumentException("Evidence id is too long.");
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        await using var select = new NpgsqlCommand("""
            select account_id,grant_id,state,uses_credit
            from licensing.reservations where reservation_id=@id for update
            """, connection, transaction);
        select.Parameters.AddWithValue("id", reservationId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("Reservation was not found.");
        var accountId = reader.GetGuid(0);
        var grantId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        var state = reader.GetString(2);
        var usesCredit = reader.GetBoolean(3);
        await reader.DisposeAsync();
        if (state != "review_required") throw new AdminConflictException();

        if (usesCredit)
        {
            if (grantId is not Guid grant) throw new InvalidDataException("Credit reservation has no grant.");
            await using var grantSelect = new NpgsqlCommand(
                "select valid_from,valid_until,revoked_at is not null,reserved from licensing.entitlement_grants where grant_id=@grant for update",
                connection, transaction);
            grantSelect.Parameters.AddWithValue("grant", grant);
            await using var grantReader = await grantSelect.ExecuteReaderAsync(cancellationToken);
            if (!await grantReader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("Grant was not found.");
            var starts = grantReader.GetFieldValue<DateTimeOffset>(0);
            var ends = grantReader.IsDBNull(1) ? null : grantReader.GetFieldValue<DateTimeOffset?>(1);
            var revoked = grantReader.GetBoolean(2);
            var reserved = grantReader.GetInt64(3);
            await grantReader.DisposeAsync();
            if (reserved <= 0) throw new AdminConflictException();
            var active = !revoked && starts <= now && (ends is null || now < ends);
            await using var grantUpdate = new NpgsqlCommand(active
                ? "update licensing.entitlement_grants set reserved=reserved-1,available=available+1 where grant_id=@grant and reserved>0"
                : "update licensing.entitlement_grants set reserved=reserved-1 where grant_id=@grant and reserved>0",
                connection, transaction);
            grantUpdate.Parameters.AddWithValue("grant", grant);
            if (await grantUpdate.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new AdminConflictException();
            await InsertLedgerAdjustmentAsync(connection, transaction, actorId, accountId, grant,
                reservationId, active ? 1 : 0, -1, 0, active ? 0 : 1,
                evidenceId, reason, cancellationToken);
        }

        await using (var updateReservation = new NpgsqlCommand(
            "update licensing.reservations set state='released',finalized_at=@now where reservation_id=@id and state='review_required'",
            connection, transaction))
        {
            updateReservation.Parameters.AddWithValue("now", now);
            updateReservation.Parameters.AddWithValue("id", reservationId);
            if (await updateReservation.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new AdminConflictException();
        }
        await AuditAsync(connection, transaction, actorId, accountId, "reservation_adjusted",
            new { reservationId, evidenceId, reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<AdminAccountView>> SearchAsync(Guid actorId, string identity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select distinct a.account_id,a.base_role,a.blocked_at is not null,a.first_purchase_at,a.merged_into
            from licensing.accounts a
            join licensing.identities i on i.account_id=a.account_id
            where i.provider_subject=@identity or i.verified_email=@identity
            order by a.account_id limit 20
            """, connection, transaction);
        command.Parameters.AddWithValue("identity", identity);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AdminAccountView>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadAccount(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AdminAccountView?> ReadAsync(Guid actorId, Guid targetId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        var result = await ReadAccountAsync(connection, transaction, targetId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    public async Task<IReadOnlyList<AdminAuditEntry>> ReadAuditAsync(Guid actorId, Guid targetId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAdminAsync(connection, transaction, actorId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select event_id,actor_account_id,event_type,details::text,created_at
            from licensing.audit_events where account_id=@target
            order by created_at desc,event_id desc limit 200
            """, connection, transaction);
        command.Parameters.AddWithValue("target", targetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AdminAuditEntry>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task EnsureAdminAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select base_role,blocked_at is not null from licensing.accounts where account_id=@actor",
            connection, transaction);
        command.Parameters.AddWithValue("actor", actorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetString(0) != "owner_admin" || reader.GetBoolean(1))
            throw new UnauthorizedAccessException("Owner admin authorization is required.");
    }
    private static async Task<AdminAccountView?> ReadAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, bool forUpdate, CancellationToken cancellationToken)
    {
        var sql = "select account_id,base_role,blocked_at is not null,first_purchase_at,merged_into " +
                  "from licensing.accounts where account_id=@id" + (forUpdate ? " for update" : string.Empty);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return ReadAccount(reader);
    }

    private static AdminAccountView ReadAccount(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4));

    private static async Task AuditAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid actorId, Guid targetId, string eventType,
        object details, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(details);
        await using var command = new NpgsqlCommand("""
            insert into licensing.audit_events(
              event_id,account_id,actor_account_id,event_type,details,created_at)
            values(@id,@target,@actor,@event,@details::jsonb,now())
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("target", targetId);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("event", eventType);
        command.Parameters.AddWithValue("details", json);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private static async Task InsertLedgerAdjustmentAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid actorId, Guid accountId, Guid? grantId,
        Guid? reservationId, long available, long reserved, long spent, long voided,
        string? evidenceId, string reason, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.credit_ledger(
              ledger_id,account_id,grant_id,reservation_id,event_kind,
              available_delta,reserved_delta,spent_delta,void_delta,
              actor_account_id,evidence_id,reason)
            values(@id,@account,@grant,@reservation,'adjustment',
              @available,@reserved,@spent,@void,@actor,@evidence,@reason)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("grant", (object?)grantId ?? DBNull.Value);
        command.Parameters.AddWithValue("reservation", (object?)reservationId ?? DBNull.Value);
        command.Parameters.AddWithValue("available", available);
        command.Parameters.AddWithValue("reserved", reserved);
        command.Parameters.AddWithValue("spent", spent);
        command.Parameters.AddWithValue("void", voided);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("evidence", (object?)evidenceId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 500) throw new ArgumentException("Reason is too long.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource) await _dataSource.DisposeAsync();
    }
}
