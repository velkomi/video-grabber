using Npgsql;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportNotificationWorker(NpgsqlDataSource database, SupportDataProtector protector,
    SupportOptions options, IEnumerable<ISupportNotificationTransport> transports, TimeProvider clock,
    IConfiguration configuration, ILogger<SupportNotificationWorker> logger) : BackgroundService
{
    private const long DispatchLock = 0x564753555050444C;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.NotificationsEnabled || !options.Ready) return;
        var interval = int.TryParse(configuration["VG_SUPPORT_POLL_INTERVAL_SECONDS"], out var seconds)
            ? Math.Clamp(seconds, 1, 60) : 15;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Support dispatch iteration failed: {ExceptionType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (var transport in transports)
        {
            if (!transport.Ready) continue;
            var item = await ClaimAsync(transport.Channel, ct);
            if (item is null) continue;
            SupportSendResult result;
            try { result = await transport.SendAsync(item, ct); }
            catch (Exception) { result = new("unknown", "transport_ack_unknown"); }
            await FinishAsync(transport.Channel, item, result, CancellationToken.None);
        }
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var close = new NpgsqlCommand("""
            update support.requests r set closed_at=@now where closed_at is null and not exists
              (select 1 from support.notifications n where n.ticket_id=r.ticket_id and n.state<>'delivered')
            """, connection);
        close.Parameters.AddWithValue("now", clock.GetUtcNow());
        await close.ExecuteNonQueryAsync(ct);
    }

    private async Task<SupportNotification?> ClaimAsync(string channel, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var gate = new NpgsqlCommand("select pg_advisory_xact_lock(@key)", connection, tx))
        { gate.Parameters.AddWithValue("key", DispatchLock); await gate.ExecuteNonQueryAsync(ct); }
        await using (var recover = new NpgsqlCommand("""
            update support.notifications set state='review_required',error_code='dispatch_interrupted'
            where state='sending' and claim_expires_at<=@now
            """, connection, tx))
        { recover.Parameters.AddWithValue("now", now); await recover.ExecuteNonQueryAsync(ct); }
        bool exhausted;
        await using (var budget = new NpgsqlCommand("""
            select count(*) filter(where reserved_at>@hour),count(*) from support.notification_attempts
            where channel=@channel and reserved_at>@day
            """, connection, tx))
        {
            budget.Parameters.AddWithValue("channel", channel); budget.Parameters.AddWithValue("hour", now.AddHours(-1)); budget.Parameters.AddWithValue("day", now.AddDays(-1));
            await using var reader = await budget.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            exhausted = reader.GetInt64(0) >= 60 || reader.GetInt64(1) >= 200;
        }
        if (exhausted) { await tx.CommitAsync(ct); return null; }
        Guid ticket = default;
        int attempt = 0;
        string topic = "", source = "";
        DateTimeOffset created = default;
        byte[] encrypted = [];
        bool found;
        await using (var due = new NpgsqlCommand("""
            select n.ticket_id,n.attempts,r.topic,r.source,r.created_at,r.encrypted_payload
            from support.notifications n join support.requests r on r.ticket_id=n.ticket_id
            where n.channel=@channel and n.state='pending' and n.next_attempt_at<=@now and n.attempts<3
            order by n.next_attempt_at,n.ticket_id limit 1 for update of n
            """, connection, tx))
        {
            due.Parameters.AddWithValue("channel", channel); due.Parameters.AddWithValue("now", now);
            await using var reader = await due.ExecuteReaderAsync(ct);
            found = await reader.ReadAsync(ct);
            if (found)
            {
                ticket = reader.GetGuid(0); attempt = reader.GetInt32(1) + 1; topic = reader.GetString(2); source = reader.GetString(3);
                created = reader.GetFieldValue<DateTimeOffset>(4); encrypted = reader.GetFieldValue<byte[]>(5);
            }
        }
        if (!found) { await tx.CommitAsync(ct); return null; }
        var payload = protector.Unprotect(ticket, encrypted);
        var claimId = Guid.NewGuid();
        await using (var update = new NpgsqlCommand("""
            update support.notifications set state='sending',attempts=@attempt,claim_id=@claim,claim_expires_at=@expires
            where ticket_id=@ticket and channel=@channel;
            insert into support.notification_attempts(claim_id,ticket_id,channel,reserved_at) values(@claim,@ticket,@channel,@now)
            """, connection, tx))
        {
            update.Parameters.AddWithValue("attempt", attempt); update.Parameters.AddWithValue("claim", claimId);
            update.Parameters.AddWithValue("expires", now.AddMinutes(2)); update.Parameters.AddWithValue("ticket", ticket);
            update.Parameters.AddWithValue("channel", channel); update.Parameters.AddWithValue("now", now);
            await update.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new SupportNotification(ticket, claimId, attempt, topic, source, created, payload);
    }

    private async Task FinishAsync(string channel, SupportNotification item, SupportSendResult result, CancellationToken ct)
    {
        var state = result.State == "delivered" ? "delivered"
            : result.State == "confirmed_failure" ? result.Retryable && item.Attempt < 3 ? "pending" : "failed" : "review_required";
        var now = clock.GetUtcNow();
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var update = new NpgsqlCommand("""
            update support.notifications set state=@state,error_code=@code,sent_at=case when @state='delivered' then @now else sent_at end,
                next_attempt_at=@next,claim_expires_at=null
            where ticket_id=@ticket and channel=@channel and claim_id=@claim
              and (state='sending' or (state='review_required' and error_code='dispatch_interrupted' and @state='delivered'));
            update support.requests r set closed_at=@now where r.ticket_id=@ticket and not exists
                (select 1 from support.notifications n where n.ticket_id=r.ticket_id and n.state<>'delivered')
            """, connection);
        update.Parameters.AddWithValue("state", state); update.Parameters.AddWithValue("code", NpgsqlTypes.NpgsqlDbType.Text, (object?)result.Code ?? DBNull.Value);
        update.Parameters.AddWithValue("now", now); update.Parameters.AddWithValue("next", now.AddSeconds(Math.Max(30 * item.Attempt, result.RetryAfterSeconds)));
        update.Parameters.AddWithValue("ticket", item.TicketId); update.Parameters.AddWithValue("channel", channel); update.Parameters.AddWithValue("claim", item.ClaimId);
        await update.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Support notification {TicketId} {Channel} {State}", item.TicketId, channel, state);
    }
}
