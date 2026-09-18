using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record BotCallbackGrant(string Action, Guid? ResourceId);

public sealed class BotCallbackStore(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<string> CreateAsync(
        Guid accountId,
        string action,
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account is required.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        if (action.Length > 64) throw new ArgumentException("Callback action is too long.", nameof(action));
        var token = Base64Url(RandomNumberGenerator.GetBytes(16));
        var hash = Hash(token);
        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into licensing.bot_callbacks(
              token_hash,account_id,action,resource_id,created_at,expires_at)
            values(@hash,@account,@action,@resource,@created,@expires)
            """, connection);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("resource", (object?)resourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", now.Add(Lifetime));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    public async Task<BotCallbackGrant?> ConsumeAsync(
        Guid accountId,
        string token,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(token)) return null;
        var now = timeProvider.GetUtcNow();
        var hash = Hash(token);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.bot_callbacks
            set consumed_at=@now
            where token_hash=@hash
              and account_id=@account
              and consumed_at is null
              and expires_at>@now
            returning action,resource_id
            """, connection);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new BotCallbackGrant(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1));
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            delete from licensing.bot_callbacks
            where expires_at < @cutoff or consumed_at < @cutoff
            """, connection);
        command.Parameters.AddWithValue("cutoff", timeProvider.GetUtcNow().AddHours(-1));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}