using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record TelegramUpdate(long UpdateId, JsonElement Body);

public interface ITelegramUpdateInbox
{
    Task<bool> AcceptAsync(TelegramUpdate update, CancellationToken cancellationToken);
}

public sealed class TelegramUpdateInbox(
    NpgsqlDataSource dataSource,
    byte[] encryptionKey,
    TimeProvider? timeProvider = null) : ITelegramUpdateInbox
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token", "refresh_token", "authorization", "cookie", "cookies",
        "initdata", "init_data", "hash", "webhook_secret", "bot_token"
    };
    private readonly byte[] _key = ValidateKey(encryptionKey);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _acceptConcurrency = new(24, 24);

    public async Task<bool> AcceptAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.UpdateId < 0) throw new ArgumentOutOfRangeException(nameof(update));
        var sanitized = Sanitize(update.Body);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[sanitized.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, sanitized, cipher, tag, AssociatedData(update.UpdateId));
        await _acceptConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                insert into licensing.telegram_updates(
                  update_id,payload_cipher,payload_nonce,payload_tag,received_at,state)
                values(@id,@cipher,@nonce,@tag,@received,'pending')
                on conflict(update_id) do nothing
                """, connection);
            command.Parameters.AddWithValue("id", update.UpdateId);
            command.Parameters.AddWithValue("cipher", cipher);
            command.Parameters.AddWithValue("nonce", nonce);
            command.Parameters.AddWithValue("tag", tag);
            command.Parameters.AddWithValue("received", _clock.GetUtcNow());
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sanitized);
            CryptographicOperations.ZeroMemory(cipher);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            _acceptConcurrency.Release();
        }
    }

    public async Task<TelegramUpdate?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var select = new NpgsqlCommand("""
            select update_id,payload_cipher,payload_nonce,payload_tag
            from licensing.telegram_updates
            where state='pending'
            order by received_at,update_id
            for update skip locked
            limit 1
            """, connection, transaction);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var updateId = reader.GetInt64(0);
        var cipher = reader.GetFieldValue<byte[]>(1);
        var nonce = reader.GetFieldValue<byte[]>(2);
        var tag = reader.GetFieldValue<byte[]>(3);
        await reader.DisposeAsync();

        var plaintext = new byte[cipher.Length];
        try
        {
            using (var aes = new AesGcm(_key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plaintext, AssociatedData(updateId));
            using var json = JsonDocument.Parse(plaintext);
            var body = json.RootElement.Clone();
            await using var update = new NpgsqlCommand("""
                update licensing.telegram_updates
                set state='processing'
                where update_id=@id and state='pending'
                """, connection, transaction);
            update.Parameters.AddWithValue("id", updateId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
            await transaction.CommitAsync(cancellationToken);
            return new TelegramUpdate(updateId, body);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(cipher);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public async Task MarkHandledAsync(long updateId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.telegram_updates
            set state='handled',handled_at=@handled
            where update_id=@id and state='processing'
            """, connection);
        command.Parameters.AddWithValue("id", updateId);
        command.Parameters.AddWithValue("handled", _clock.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CleanupHandledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            delete from licensing.telegram_updates
            where state='handled' and handled_at < @cutoff
            """, connection);
        command.Parameters.AddWithValue("cutoff", _clock.GetUtcNow().AddHours(-24));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static byte[] Sanitize(JsonElement body)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory))
            WriteSanitized(writer, body);
        return memory.ToArray();
    }

    private static void WriteSanitized(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (SensitiveFields.Contains(property.Name)) continue;
                    writer.WritePropertyName(property.Name);
                    WriteSanitized(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteSanitized(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static byte[] AssociatedData(long updateId)
        => Encoding.ASCII.GetBytes(updateId.ToString(CultureInfo.InvariantCulture));

    private static byte[] ValidateKey(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32) throw new ArgumentException("Telegram inbox key must be 32 bytes.", nameof(value));
        return value.ToArray();
    }
}