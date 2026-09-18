using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class DestinationChallengeConflictException : Exception;

public sealed class DestinationService(
    NpgsqlDataSource dataSource,
    IBotApiClient bot,
    TimeProvider clock)
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    public async Task<DestinationChallenge> BeginAsync(
        Guid accountId,
        long chatId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account is required.", nameof(accountId));
        if (chatId == 0) throw new ArgumentException("Chat ID is required.", nameof(chatId));
        var proof = Base64Url(RandomNumberGenerator.GetBytes(16));
        var now = clock.GetUtcNow();
        var expires = now.Add(ChallengeLifetime);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.telegram_destination_challenges(
              proof_hash,account_id,chat_id,created_at,expires_at)
            values(@proof,@account,@chat,@created,@expires)
            """, connection, transaction);
        command.Parameters.AddWithValue("proof", Hash(proof));
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("chat", chatId);
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", expires);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DestinationChallenge(proof, expires);
    }

    public async Task<DeliveryDestination> LinkAsync(
        Guid accountId,
        DestinationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var now = clock.GetUtcNow();
        var challenge = await ReadChallengeAsync(accountId, request.Proof, cancellationToken);
        if (challenge is null || challenge.ChatId != request.ChatId
            || challenge.Consumed || challenge.ExpiresAt <= now)
            throw new DestinationChallengeConflictException();

        var telegramUserId = await ReadTelegramUserIdAsync(accountId, cancellationToken);
        var rights = await ReadRightsAsync(request.ChatId, telegramUserId, cancellationToken).ConfigureAwait(false);
        EnsureRights(request.ChatId, telegramUserId, rights);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using (var consume = new NpgsqlCommand("""
            update licensing.telegram_destination_challenges
            set consumed_at=@now
            where proof_hash=@proof and account_id=@account and chat_id=@chat
              and consumed_at is null and expires_at>@now
            returning proof_hash
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue("now", now);
            consume.Parameters.AddWithValue("proof", Hash(request.Proof));
            consume.Parameters.AddWithValue("account", accountId);
            consume.Parameters.AddWithValue("chat", request.ChatId);
            if (await consume.ExecuteScalarAsync(cancellationToken) is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new DestinationChallengeConflictException();
            }
        }

        await using var insert = new NpgsqlCommand("""
            insert into licensing.delivery_destinations(
              destination_id,account_id,chat_id,title,kind,telegram_user_id,linked_at)
            values(@id,@account,@chat,@title,@kind,@user,@now)
            on conflict (account_id,chat_id) where revoked_at is null
            do update set title=excluded.title,kind=excluded.kind,telegram_user_id=excluded.telegram_user_id
            returning destination_id,account_id,chat_id,kind,revoked_at is not null
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("chat", request.ChatId);
        insert.Parameters.AddWithValue("title", request.Title.Trim());
        insert.Parameters.AddWithValue("kind", rights.Kind);
        insert.Parameters.AddWithValue("user", telegramUserId);
        insert.Parameters.AddWithValue("now", now);
        await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidDataException("Destination insert returned no row.");
        var result = new DeliveryDestination(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetString(3), reader.GetBoolean(4));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<DeliveryDestination>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select destination_id,account_id,chat_id,kind,revoked_at is not null
            from licensing.delivery_destinations
            where account_id=@account
            order by linked_at,destination_id
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<DeliveryDestination>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new DeliveryDestination(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2),
                reader.GetString(3), reader.GetBoolean(4)));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<DeliveryDestination> AuthorizeSendAsync(
        Guid accountId,
        Guid destinationId,
        CancellationToken cancellationToken)
    {
        var stored = await ReadActiveAsync(accountId, destinationId, cancellationToken)
            ?? throw new KeyNotFoundException("Destination was not found.");
        var telegramUserId = await ReadTelegramUserIdAsync(accountId, cancellationToken);
        if (stored.TelegramUserId != telegramUserId)
            throw new UnauthorizedAccessException("destination_permission_required");
        var rights = await bot.GetRightsAsync(stored.Destination.ChatId, telegramUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(rights.Kind, stored.Destination.Kind, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("destination_permission_required");
        EnsureRights(stored.Destination.ChatId, telegramUserId, rights);
        return stored.Destination;
    }

    public async Task RevokeAsync(
        Guid accountId,
        Guid destinationId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.delivery_destinations
            set revoked_at=coalesce(revoked_at,@now)
            where destination_id=@id and account_id=@account
            returning destination_id
            """, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("id", destinationId);
        command.Parameters.AddWithValue("account", accountId);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new KeyNotFoundException("Destination was not found.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<ChallengeState?> ReadChallengeAsync(
        Guid accountId,
        string proof,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select chat_id,expires_at,consumed_at is not null
            from licensing.telegram_destination_challenges
            where proof_hash=@proof and account_id=@account
            """, connection, transaction);
        command.Parameters.AddWithValue("proof", Hash(proof));
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        ChallengeState? state = null;
        if (await reader.ReadAsync(cancellationToken))
            state = new ChallengeState(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetBoolean(2));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return state;
    }

    private async Task<long> ReadTelegramUserIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select provider_subject
            from licensing.identities
            where account_id=@account and provider='telegram'
            order by linked_at
            limit 2
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var subjects = new List<string>(2);
        while (await reader.ReadAsync(cancellationToken)) subjects.Add(reader.GetString(0));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        if (subjects.Count != 1
            || !long.TryParse(subjects[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || userId <= 0)
            throw new UnauthorizedAccessException("destination_telegram_identity_required");
        return userId;
    }

    private async Task<StoredDestination?> ReadActiveAsync(
        Guid accountId,
        Guid destinationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select destination_id,account_id,chat_id,kind,telegram_user_id
            from licensing.delivery_destinations
            where destination_id=@id and account_id=@account and revoked_at is null
            """, connection, transaction);
        command.Parameters.AddWithValue("id", destinationId);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        StoredDestination? stored = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            var destination = new DeliveryDestination(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetString(3), false);
            stored = new StoredDestination(destination, reader.GetInt64(4));
        }
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    private async Task<TelegramChatRights> ReadRightsAsync(
        long chatId,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await bot.GetRightsAsync(chatId, telegramUserId, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new UnauthorizedAccessException("destination_permission_required");
        }
        catch (InvalidDataException)
        {
            throw new UnauthorizedAccessException("destination_permission_required");
        }
    }
    private static void EnsureRights(long chatId, long telegramUserId, TelegramChatRights rights)
    {
        if (rights.Kind is not ("private" or "channel" or "group" or "supergroup"))
            throw new UnauthorizedAccessException("destination_permission_required");
        if (rights.Kind == "private" && chatId != telegramUserId)
            throw new UnauthorizedAccessException("destination_permission_required");
        if (!rights.UserCanPublish || !rights.BotCanPublish)
            throw new UnauthorizedAccessException("destination_permission_required");
    }

    private static void ValidateRequest(DestinationRequest request)
    {
        if (request.ChatId == 0) throw new ArgumentException("Chat ID is required.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Proof);
        if (request.Title.Trim().Length > 80 || request.Title.Any(char.IsControl))
            throw new ArgumentException("Destination title is invalid.", nameof(request));
        if (request.Proof.Length > 128)
            throw new ArgumentException("Destination proof is invalid.", nameof(request));
    }

    private static Task SetAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            "select set_config('vg.account_id', @account, true)", connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        return ExecuteAndDisposeAsync(command, cancellationToken);
    }

    private static async Task ExecuteAndDisposeAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using (command) await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record ChallengeState(long ChatId, DateTimeOffset ExpiresAt, bool Consumed);
    private sealed record StoredDestination(DeliveryDestination Destination, long TelegramUserId);
}