using System.Data;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class DeliveryConflictException : Exception;

public sealed class ArtifactDeliveryService(
    NpgsqlDataSource dataSource,
    DestinationService destinations,
    IAccountStore accounts,
    IBotApiClient bot,
    IConfiguration configuration,
    TimeProvider clock)
{
    private readonly long _maxDocumentBytes =
        long.TryParse(configuration["VG_TELEGRAM_DOCUMENT_MAX_BYTES"], out var configured)
        && configured > 0
            ? configured
            : 50L * 1024 * 1024;

    public static bool MayAutomaticallyRetry(string state)
        => string.Equals(state, "failed_before_send", StringComparison.Ordinal);

    public async Task<DeliveryView> RequestAsync(
        Guid accountId,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var created = await CreateOrReadAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);
        if (created.State is "delivered" or "delivery_unknown")
            return created;
        if (!MayAutomaticallyRetry(created.State) && created.State != "pending")
            return created;
        return await TrySendAsync(accountId, created.DeliveryId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeliveryView> RetryUnknownAsync(
        Guid accountId,
        Guid deliveryId,
        string acknowledgedWarning,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                acknowledgedWarning,
                "I understand this may send a duplicate",
                StringComparison.Ordinal))
            throw new ArgumentException("duplicate_warning_not_acknowledged");
        var row = await ReadOwnedAsync(accountId, deliveryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Delivery was not found.");
        if (row.State != "delivery_unknown")
            throw new DeliveryConflictException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.delivery_attempts
            set state='pending',reason='manual_retry_after_unknown',
                retry_count=retry_count+1,updated_at=@now
            where delivery_id=@delivery and account_id=@account
              and state='delivery_unknown'
            """, connection);
        command.Parameters.AddWithValue("now", clock.GetUtcNow());
        command.Parameters.AddWithValue("delivery", deliveryId);
        command.Parameters.AddWithValue("account", accountId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new DeliveryConflictException();
        return await TrySendAsync(accountId, deliveryId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeliveryView?> ReadAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
        => await ReadOwnedAsync(accountId, deliveryId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DeliveryView>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select delivery_id,state,message_id,telegram_file_id,reason
            from licensing.delivery_attempts
            where account_id=@account
            order by created_at,delivery_id
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<DeliveryView>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadView(reader));
        return result;
    }

    public async Task<DeliveryView?> ProcessNextAsync(
        CancellationToken cancellationToken)
    {
        Guid accountId;
        Guid deliveryId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken))
        {
            await using var command = new NpgsqlCommand("""
                select delivery_id,account_id
                from licensing.delivery_attempts
                where state in ('pending','failed_before_send')
                order by updated_at,delivery_id
                for update skip locked
                limit 1
                """, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            deliveryId = reader.GetGuid(0);
            accountId = reader.GetGuid(1);
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        return await TrySendAsync(accountId, deliveryId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DeliveryView> CreateOrReadAsync(
        Guid accountId,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await using (var existing = new NpgsqlCommand("""
            select delivery_id,job_id,artifact_id,destination_id,state,
                   message_id,telegram_file_id,reason
            from licensing.delivery_attempts
            where account_id=@account and idempotency_key=@key
            for update
            """, connection, transaction))
        {
            existing.Parameters.AddWithValue("account", accountId);
            existing.Parameters.AddWithValue("key", request.IdempotencyKey);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var deliveryId = reader.GetGuid(0);
                var jobId = reader.GetGuid(1);
                var artifactId = reader.GetGuid(2);
                var destinationId = reader.GetGuid(3);
                var view = new DeliveryView(
                    deliveryId,
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetInt64(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7));
                await reader.DisposeAsync();
                if (jobId != request.JobId
                    || artifactId != request.ArtifactId
                    || destinationId != request.DestinationId)
                    throw new DeliveryConflictException();
                await transaction.CommitAsync(cancellationToken);
                return view;
            }
            await reader.DisposeAsync();
        }

        if (!await OwnedArtifactExistsAsync(
                connection, transaction, accountId, request.JobId,
                request.ArtifactId, cancellationToken))
            throw new KeyNotFoundException("Artifact was not found.");

        var delivery = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("""
            insert into licensing.delivery_attempts(
              delivery_id,account_id,job_id,artifact_id,destination_id,
              idempotency_key,state,reason,created_at,updated_at)
            values(@delivery,@account,@job,@artifact,@destination,
              @key,'pending','pending',@now,@now)
            """, connection, transaction);
        insert.Parameters.AddWithValue("delivery", delivery);
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("job", request.JobId);
        insert.Parameters.AddWithValue("artifact", request.ArtifactId);
        insert.Parameters.AddWithValue("destination", request.DestinationId);
        insert.Parameters.AddWithValue("key", request.IdempotencyKey);
        insert.Parameters.AddWithValue("now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryView(delivery, "pending", null, null, "pending");
    }

    private async Task<DeliveryView> TrySendAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        var account = await accounts.ReadAsync(accountId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Account was not found.");
        if (account.Blocked)
            return await FailBeforeSendAsync(
                accountId, deliveryId, "account_blocked", cancellationToken);

        var details = await ReadDetailsAsync(
            accountId, deliveryId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Delivery was not found.");
        if (details.State is "delivered" or "delivery_unknown")
            return details.ToView();
        if (details.State == "sending")
            return await MarkUnknownAsync(
                accountId, deliveryId, "restart_during_send", cancellationToken);

        DeliveryDestination destination;
        try
        {
            destination = await destinations.AuthorizeSendAsync(
                accountId, details.DestinationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException)
        {
            return await FailBeforeSendAsync(
                accountId, deliveryId, "destination_permission_required", cancellationToken);
        }

        if (details.Bytes > _maxDocumentBytes)
            return await FailBeforeSendAsync(
                accountId, deliveryId,
                "transport_limit_exceeded_choose_split_or_desktop",
                cancellationToken);
        if (string.IsNullOrWhiteSpace(details.StoragePath)
            || !Path.IsPathFullyQualified(details.StoragePath)
            || !File.Exists(details.StoragePath))
            return await FailBeforeSendAsync(
                accountId, deliveryId, "artifact_transport_unavailable", cancellationToken);

        var sending = await MarkSendingAsync(
            accountId, deliveryId, cancellationToken).ConfigureAwait(false);
        if (!sending)
            return await ReadRequiredAsync(accountId, deliveryId, cancellationToken);

        try
        {
            await using var stream = new FileStream(
                details.StoragePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != details.Bytes)
                return await MarkUnknownAsync(
                    accountId, deliveryId, "artifact_changed_after_send_intent", cancellationToken);
            var sent = await bot.SendDocumentAsync(
                destination.ChatId,
                stream,
                Path.GetFileName(details.StoragePath),
                cancellationToken).ConfigureAwait(false);
            return await MarkDeliveredAsync(
                accountId, deliveryId, sent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkUnknownAsync(
                accountId, deliveryId, "send_cancelled_after_start", CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or IOException
                                   or TaskCanceledException)
        {
            return await MarkUnknownAsync(
                accountId, deliveryId, "transport_ack_unknown", CancellationToken.None);
        }
    }

    private static async Task<bool> OwnedArtifactExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid jobId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select exists(
              select 1
              from licensing.jobs j
              join licensing.artifacts a on a.job_id=j.job_id
              where j.account_id=@account and j.job_id=@job
                and j.state='completed'
                and j.artifact_id=@artifact
                and a.artifact_id=@artifact
                and a.expired_at is null)
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("artifact", artifactId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<DeliveryDetails?> ReadDetailsAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select d.delivery_id,d.job_id,d.artifact_id,d.destination_id,d.state,
                   d.message_id,d.telegram_file_id,d.reason,
                   a.bytes,a.storage_path,a.media_type
            from licensing.delivery_attempts d
            join licensing.artifacts a on a.artifact_id=d.artifact_id
            join licensing.jobs j on j.job_id=d.job_id
            where d.delivery_id=@delivery and d.account_id=@account
              and j.account_id=@account and a.account_id=@account
              and a.expired_at is null
            """, connection);
        command.Parameters.AddWithValue("delivery", deliveryId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new DeliveryDetails(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
            reader.GetGuid(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7), reader.GetInt64(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetString(10));
    }

    private async Task<DeliveryView?> ReadOwnedAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select delivery_id,state,message_id,telegram_file_id,reason
            from licensing.delivery_attempts
            where delivery_id=@delivery and account_id=@account
            """, connection);
        command.Parameters.AddWithValue("delivery", deliveryId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadView(reader) : null;
    }

    private async Task<DeliveryView> ReadRequiredAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
        => await ReadOwnedAsync(accountId, deliveryId, cancellationToken)
            ?? throw new KeyNotFoundException("Delivery was not found.");

    private async Task<bool> MarkSendingAsync(
        Guid accountId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.delivery_attempts
            set state='sending',reason='send_started',updated_at=@now
            where delivery_id=@delivery and account_id=@account
              and state in ('pending','failed_before_send')
            """, connection);
        command.Parameters.AddWithValue("now", clock.GetUtcNow());
        command.Parameters.AddWithValue("delivery", deliveryId);
        command.Parameters.AddWithValue("account", accountId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private Task<DeliveryView> FailBeforeSendAsync(
        Guid accountId,
        Guid deliveryId,
        string reason,
        CancellationToken cancellationToken)
        => SetStateAsync(
            accountId, deliveryId, "failed_before_send", reason,
            null, null, cancellationToken);

    private Task<DeliveryView> MarkUnknownAsync(
        Guid accountId,
        Guid deliveryId,
        string reason,
        CancellationToken cancellationToken)
        => SetStateAsync(
            accountId, deliveryId, "delivery_unknown", reason,
            null, null, cancellationToken);

    private Task<DeliveryView> MarkDeliveredAsync(
        Guid accountId,
        Guid deliveryId,
        BotSentMessage sent,
        CancellationToken cancellationToken)
        => SetStateAsync(
            accountId, deliveryId, "delivered", "telegram_acknowledged",
            sent.MessageId, sent.FileId, cancellationToken);

    private async Task<DeliveryView> SetStateAsync(
        Guid accountId,
        Guid deliveryId,
        string state,
        string reason,
        long? messageId,
        string? fileId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.delivery_attempts
            set state=@state,reason=@reason,message_id=@message,
                telegram_file_id=@file,updated_at=@now
            where delivery_id=@delivery and account_id=@account
            returning delivery_id,state,message_id,telegram_file_id,reason
            """, connection);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("message", (object?)messageId ?? DBNull.Value);
        command.Parameters.AddWithValue("file", (object?)fileId ?? DBNull.Value);
        command.Parameters.AddWithValue("now", clock.GetUtcNow());
        command.Parameters.AddWithValue("delivery", deliveryId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new KeyNotFoundException("Delivery was not found.");
        return ReadView(reader);
    }

    private static DeliveryView ReadView(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4));

    private static void ValidateRequest(DeliveryRequest request)
    {
        if (request.JobId == Guid.Empty
            || request.ArtifactId == Guid.Empty
            || request.DestinationId == Guid.Empty
            || request.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("Delivery request is incomplete.");
    }

    private sealed record DeliveryDetails(
        Guid DeliveryId,
        Guid JobId,
        Guid ArtifactId,
        Guid DestinationId,
        string State,
        long? MessageId,
        string? FileId,
        string Reason,
        long Bytes,
        string? StoragePath,
        string MediaType)
    {
        public DeliveryView ToView()
            => new(DeliveryId, State, MessageId, FileId, Reason);
    }
}
