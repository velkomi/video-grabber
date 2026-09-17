using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;

namespace VideoGrabber.Platform.Persistence;

public sealed class GrantStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _apiDataSource;
    private readonly NpgsqlDataSource _adminDataSource;
    private readonly IAccountStore _accounts;
    private readonly TimeProvider _clock;
    private readonly bool _ownsAdminDataSource;

    public static GrantStore CreateOwned(NpgsqlDataSource apiDataSource, string adminDsn,
        IAccountStore accounts, TimeProvider clock)
        => new(apiDataSource, NpgsqlDataSource.Create(adminDsn), accounts, clock, true);
    private GrantStore(NpgsqlDataSource apiDataSource, NpgsqlDataSource adminDataSource,
        IAccountStore accounts, TimeProvider clock, bool ownsAdminDataSource)
    {
        _apiDataSource = apiDataSource;
        _adminDataSource = adminDataSource;
        _accounts = accounts;
        _clock = clock;
        _ownsAdminDataSource = ownsAdminDataSource;
    }

    public static GrantStore CreateForTesting(NpgsqlDataSource apiDataSource,
        NpgsqlDataSource adminDataSource, IAccountStore accounts, TimeProvider clock)
        => new(apiDataSource, adminDataSource, accounts, clock, false);

    public async Task<GrantReceipt> GiftAsync(Guid adminId, GrantRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var payloadHash = PayloadHash(request);
        var now = _clock.GetUtcNow();
        await using var connection = await _adminDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureOwnerAdminAsync(connection, transaction, adminId, cancellationToken);
        var prior = await ReadIdempotentAsync(connection, transaction, adminId,
            request.IdempotencyKey, cancellationToken);
        if (prior is { } existing)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(existing.PayloadHash),
                    Encoding.ASCII.GetBytes(payloadHash)))
                throw new GrantConflictException();
            await transaction.CommitAsync(cancellationToken);
            return new(existing.GrantId, request.AccountId, "admin_gift");
        }

        var (startsAt, endsAt, amount) = await ResolveWindowAsync(
            connection, transaction, request, now, cancellationToken);
        var grantId = Guid.NewGuid();
        const string insertSql = """
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,valid_from,valid_until,available,reserved,
              original_amount,admin_id,idempotency_key,payload_hash,reason,created_at)
            values(@id,@account,@kind,'admin_gift',@start,@end,@available,0,
              @original,@admin,@key,@hash,@reason,@created)
            on conflict (admin_id,idempotency_key) do nothing
            """;
        await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
        insert.Parameters.AddWithValue("id", grantId);
        insert.Parameters.AddWithValue("account", request.AccountId);
        insert.Parameters.AddWithValue("kind", request.Kind);
        insert.Parameters.AddWithValue("start", startsAt);
        insert.Parameters.AddWithValue("end", (object?)endsAt ?? DBNull.Value);
        insert.Parameters.AddWithValue("available",
            request.Kind is "credits" or "hybrid" ? amount : 0L);
        insert.Parameters.AddWithValue("original", amount);
        insert.Parameters.AddWithValue("admin", adminId);
        insert.Parameters.AddWithValue("key", request.IdempotencyKey);
        insert.Parameters.AddWithValue("hash", payloadHash);
        insert.Parameters.AddWithValue("reason", request.Reason);
        insert.Parameters.AddWithValue("created", now);
        var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
        if (inserted == 0)
        {
            var concurrent = await ReadIdempotentAsync(connection, transaction, adminId,
                request.IdempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("Idempotent grant disappeared after conflict.");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(concurrent.PayloadHash),
                    Encoding.ASCII.GetBytes(payloadHash)))
                throw new GrantConflictException();
            await transaction.CommitAsync(cancellationToken);
            return new(concurrent.GrantId, request.AccountId, "admin_gift");
        }

        await using var audit = new NpgsqlCommand("""
            insert into licensing.audit_events(
              event_id,account_id,actor_account_id,event_type,details,created_at)
            values(@event,@account,@admin,'grant_created',
              jsonb_build_object('grant_id',@grant,'kind',@kind,'source','admin_gift',
                'original_amount',@amount,'reason',@reason),@created)
            """, connection, transaction);
        audit.Parameters.AddWithValue("event", Guid.NewGuid());
        audit.Parameters.AddWithValue("account", request.AccountId);
        audit.Parameters.AddWithValue("admin", adminId);
        audit.Parameters.AddWithValue("grant", grantId.ToString("D"));
        audit.Parameters.AddWithValue("kind", request.Kind);
        audit.Parameters.AddWithValue("amount", amount);
        audit.Parameters.AddWithValue("reason", request.Reason);
        audit.Parameters.AddWithValue("created", now);
        await audit.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(grantId, request.AccountId, "admin_gift");
    }

    public async Task<AccessSnapshot> EvaluateAsync(Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.ReadAsync(accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found.");
        var grants = new List<Grant>();
        await using var connection = await _apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select grant_id,kind,source,valid_from,valid_until,available,reserved,
                   revoked_at is not null,created_at
            from licensing.entitlement_grants
            where account_id=@account order by created_at,grant_id
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            grants.Add(new Grant(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetInt64(5), reader.GetInt64(6), reader.GetBoolean(7),
                reader.GetFieldValue<DateTimeOffset>(8)));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return AccessEvaluator.Evaluate(account, grants, _clock.GetUtcNow());
    }

    private static async Task EnsureOwnerAdminAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid adminId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select base_role,blocked_at is not null
            from licensing.accounts where account_id=@admin
            """, connection, transaction);
        command.Parameters.AddWithValue("admin", adminId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetString(0) != "owner_admin" || reader.GetBoolean(1))
            throw new UnauthorizedAccessException("Owner admin authorization is required.");
    }
    private static async Task<ExistingGift?> ReadIdempotentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid adminId,
        Guid key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select grant_id,payload_hash from licensing.entitlement_grants
            where admin_id=@admin and idempotency_key=@key
            """, connection, transaction);
        command.Parameters.AddWithValue("admin", adminId);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetGuid(0), reader.GetString(1));
    }

    private static async Task<(DateTimeOffset Start, DateTimeOffset? End, long Amount)>
        ResolveWindowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            GrantRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (request.Kind == "time")
        {
            var start = await LatestContiguousTimeEndAsync(connection, transaction,
                request.AccountId, now, cancellationToken);
            return (start, start.AddDays(request.Days), request.Days);
        }
        if (request.Kind == "permanent") return (now, null, 0);
        var end = request.ExpiresAt;
        if (end is { } expiry && expiry <= now)
            throw new ArgumentException("Grant expiry must be in the future.");
        return (now, end, request.Credits);
    }
    private static async Task<DateTimeOffset> LatestContiguousTimeEndAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select valid_from,valid_until from licensing.entitlement_grants
            where account_id=@account and kind='time' and revoked_at is null
              and valid_until is not null and valid_until>@now
            order by valid_from,valid_until
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var cursor = now;
        while (await reader.ReadAsync(cancellationToken))
        {
            var start = reader.GetFieldValue<DateTimeOffset>(0);
            var end = reader.GetFieldValue<DateTimeOffset>(1);
            if (start > cursor) break;
            if (end > cursor) cursor = end;
        }
        return cursor;
    }

    private static void ValidateRequest(GrantRequest request)
    {
        if (request.AccountId == Guid.Empty || request.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("Account and idempotency key are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        if (request.Reason.Length > 500) throw new ArgumentException("Reason is too long.");
        switch (request.Kind)
        {
            case "time" when request.Days > 0 && request.Credits == 0 && request.ExpiresAt is null:
                break;
            case "credits" when request.Days == 0 && request.Credits > 0:
                break;
            case "hybrid" when request.Days == 0 && request.Credits > 0 && request.ExpiresAt is not null:
                break;
            case "permanent" when request.Days == 0 && request.Credits == 0 && request.ExpiresAt is null:
                break;
            default:
                throw new ArgumentException("Grant shape does not match its kind.");
        }
    }

    private static string PayloadHash(GrantRequest request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            request.AccountId,
            request.Kind,
            request.Days,
            request.Credits,
            request.ExpiresAt,
            request.Reason
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
    private static async Task SetAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select set_config('vg.account_id',@account,true)", connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsAdminDataSource) await _adminDataSource.DisposeAsync();
    }

    private sealed record ExistingGift(Guid GrantId, string PayloadHash);
}

public sealed class GrantConflictException : Exception;
