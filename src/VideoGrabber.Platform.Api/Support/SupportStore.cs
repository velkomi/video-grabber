using Npgsql;
using NpgsqlTypes;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportStore(NpgsqlDataSource database, SupportDataProtector protector, TimeProvider clock)
{
    private const long IntakeLock = 0x5647535550504F52;

    public async Task<SupportAccepted?> FindExistingAsync(SupportRequest request, Guid? accountId, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        return await ReadExistingAsync(connection, null, request, accountId, ct);
    }

    public Task<SupportAccepted> SubmitVerifiedAsync(SupportRequest request, Guid? accountId,
        string remoteAddress, string source, SupportChallengeClaim challenge, CancellationToken ct)
        => SubmitCoreAsync(SupportOptions.Validate(request), accountId, remoteAddress, source, challenge, ct);

    public Task<SupportAccepted> SubmitTrustedTelegramAsync(SupportRequest request, Guid accountId,
        long senderId, CancellationToken ct)
    {
        if (accountId == Guid.Empty || senderId <= 0) throw new SupportRequestException(401, "support_identity_required");
        return SubmitCoreAsync(SupportOptions.Validate(request), accountId, "telegram:" + senderId, "telegram", null, ct);
    }

    private async Task<SupportAccepted> SubmitCoreAsync(SupportRequest request, Guid? accountId,
        string remoteAddress, string source, SupportChallengeClaim? challenge, CancellationToken ct)
    {
        if (source is not ("form" or "windows" or "miniapp" or "telegram")) throw new ArgumentException("Invalid support source.");
        var now = clock.GetUtcNow();
        var caller = protector.CallerKey(accountId, request.Contact);
        var rateKey = protector.RateKey(accountId, request.Contact, remoteAddress);
        var ipKey = protector.Digest("ip:" + remoteAddress);
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var gate = new NpgsqlCommand("select pg_advisory_xact_lock(@key)", connection, transaction))
        { gate.Parameters.AddWithValue("key", IntakeLock); await gate.ExecuteNonQueryAsync(ct); }
        var existing = await ReadExistingAsync(connection, transaction, request, accountId, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing; }

        await using (var pending = new NpgsqlCommand("select count(*) from support.requests where closed_at is null", connection, transaction))
            if ((long)(await pending.ExecuteScalarAsync(ct))! >= 1000)
                throw new SupportRequestException(503, "support_temporarily_unavailable");
        await using (var limits = new NpgsqlCommand("""
            select count(*) filter(where rate_key=@caller and created_at>@minute),
                   count(*) filter(where rate_key=@caller and created_at>@quarter),
                   count(*) filter(where rate_key=@caller and created_at>@day),
                   count(*) filter(where ip_key=@ip and created_at>@hour)
            from support.requests where created_at>@day and (rate_key=@caller or ip_key=@ip)
            """, connection, transaction))
        {
            limits.Parameters.AddWithValue("caller", rateKey); limits.Parameters.AddWithValue("ip", ipKey);
            limits.Parameters.AddWithValue("minute", now.AddMinutes(-1)); limits.Parameters.AddWithValue("quarter", now.AddMinutes(-15));
            limits.Parameters.AddWithValue("day", now.AddDays(-1)); limits.Parameters.AddWithValue("hour", now.AddHours(-1));
            await using var reader = await limits.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            if (reader.GetInt64(0) >= 1) throw new SupportRequestException(429, "support_rate_limited", 60);
            if (reader.GetInt64(1) >= 3) throw new SupportRequestException(429, "support_rate_limited", 900);
            if (reader.GetInt64(2) >= 10) throw new SupportRequestException(429, "support_rate_limited", 86400);
            if (reader.GetInt64(3) >= 30) throw new SupportRequestException(429, "support_rate_limited", 3600);
        }
        if (challenge is not null)
        {
            if (challenge.ExpiresAt <= now) throw new SupportRequestException(403, "support_verification_required");
            await using var used = new NpgsqlCommand("select exists(select 1 from support.challenge_claims where challenge_key=@key)", connection, transaction);
            used.Parameters.AddWithValue("key", challenge.Key);
            if ((bool)(await used.ExecuteScalarAsync(ct))!) throw new SupportRequestException(403, "support_verification_required");
        }

        var ticketId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            insert into support.requests(request_id,ticket_id,account_id,caller_key,rate_key,ip_key,request_hash,topic,source,encrypted_payload,created_at)
            values(@request,@ticket,@account,@caller,@rate,@ip,@hash,@topic,@source,@payload,@now)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("request", request.RequestId); insert.Parameters.AddWithValue("ticket", ticketId);
            insert.Parameters.AddWithValue("account", NpgsqlDbType.Uuid, accountId is { } account ? account : DBNull.Value);
            insert.Parameters.AddWithValue("caller", caller); insert.Parameters.AddWithValue("ip", ipKey);
            insert.Parameters.AddWithValue("rate", rateKey);
            insert.Parameters.AddWithValue("hash", protector.RequestHash(request)); insert.Parameters.AddWithValue("topic", request.Topic);
            insert.Parameters.AddWithValue("source", source); insert.Parameters.AddWithValue("now", now);
            insert.Parameters.AddWithValue("payload", protector.Protect(ticketId, new SupportPayload(request.Contact, request.Message)));
            await insert.ExecuteNonQueryAsync(ct);
        }
        if (challenge is not null)
        {
            await using var claim = new NpgsqlCommand("insert into support.challenge_claims(challenge_key,ticket_id,expires_at) values(@key,@ticket,@expires)", connection, transaction);
            claim.Parameters.AddWithValue("key", challenge.Key); claim.Parameters.AddWithValue("ticket", ticketId);
            claim.Parameters.AddWithValue("expires", challenge.ExpiresAt); await claim.ExecuteNonQueryAsync(ct);
        }
        await using (var outbox = new NpgsqlCommand("""
            insert into support.notifications(ticket_id,channel,state,next_attempt_at)
            values(@ticket,'email','pending',@now),(@ticket,'telegram','pending',@now)
            """, connection, transaction))
        {
            outbox.Parameters.AddWithValue("ticket", ticketId); outbox.Parameters.AddWithValue("now", now);
            await outbox.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new SupportAccepted(ticketId);
    }

    private async Task<SupportAccepted?> ReadExistingAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        SupportRequest request, Guid? accountId, CancellationToken ct)
    {
        await using var lookup = new NpgsqlCommand("select ticket_id,caller_key,request_hash from support.requests where request_id=@request", connection, transaction);
        lookup.Parameters.AddWithValue("request", request.RequestId);
        await using var reader = await lookup.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetString(1) != protector.CallerKey(accountId, request.Contact)
            || reader.GetString(2) != protector.RequestHash(request)) throw new SupportRequestException(409, "support_request_conflict");
        return new SupportAccepted(reader.GetGuid(0));
    }
}
