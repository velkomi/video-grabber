using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class DeviceConflictException : Exception;
public sealed class DeviceLimitException : Exception;
public sealed class DeviceNotFoundException : Exception;
public sealed class DeviceProofException : Exception
{
    public DeviceProofException() { }
    public DeviceProofException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class DeviceStore : IAsyncDisposable
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromSeconds(60);
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly bool _ownsDataSource;

    private DeviceStore(NpgsqlDataSource dataSource, TimeProvider clock, bool ownsDataSource)
    {
        _dataSource = dataSource;
        _clock = clock;
        _ownsDataSource = ownsDataSource;
    }

    public static DeviceStore CreateOwned(string dsn, TimeProvider clock)
        => new(NpgsqlDataSource.Create(dsn), clock, true);

    public static DeviceStore CreateForTesting(NpgsqlDataSource dataSource, TimeProvider clock)
        => new(dataSource, clock, false);

    public async Task<DeviceReceipt> RegisterAsync(
        Guid accountId, DeviceRegistration request, CancellationToken cancellationToken)
    {
        ValidateRegistration(request);
        var hash = PayloadHash(request);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        var existing = await ReadByKeyAsync(connection, transaction, accountId,
            request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(existing.PayloadHash), Encoding.ASCII.GetBytes(hash)))
                throw new DeviceConflictException();
            await transaction.CommitAsync(cancellationToken);
            return new(existing.DeviceId, existing.Name, existing.Revoked);
        }

        var account = await ReadAccountAsync(connection, transaction, accountId, cancellationToken)
            ?? throw new DeviceNotFoundException();
        if (account.Blocked) throw new DeviceLimitException();
        var limit = account.Role == "owner_admin" ? 3 : 1;
        var active = await CountActiveAsync(connection, transaction, accountId, cancellationToken);
        if (active >= limit) throw new DeviceLimitException();

        var deviceId = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("""
            insert into licensing.devices(
              device_id,account_id,name,platform,public_key,idempotency_key,payload_hash,created_at)
            values(@id,@account,@name,'windows',@key,@idem,@hash,@now)
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", deviceId);
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("name", request.Name);
        insert.Parameters.AddWithValue("key", request.PublicKey);
        insert.Parameters.AddWithValue("idem", request.IdempotencyKey);
        insert.Parameters.AddWithValue("hash", hash);
        insert.Parameters.AddWithValue("now", _clock.GetUtcNow());
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(deviceId, request.Name, false);
    }

    public async Task<IReadOnlyList<DeviceReceipt>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand(
            "select device_id,name,revoked_at is not null,last_seen_at from licensing.devices where account_id=@account order by created_at,device_id",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<DeviceReceipt>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new DeviceReceipt(
                reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2))
            {
                LastSeenAt = reader.IsDBNull(3)
                    ? null
                    : reader.GetFieldValue<DateTimeOffset>(3)
            });
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    public async Task RevokeAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            update licensing.devices
            set revoked_at=coalesce(revoked_at,@now),lease_version=lease_version+1
            where account_id=@account and device_id=@device and revoked_at is null
            """, connection, transaction);
        command.Parameters.AddWithValue("now", _clock.GetUtcNow());
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new DeviceNotFoundException();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DeviceChallenge> CreateChallengeAsync(
        Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var expires = now.Add(ChallengeLifetime);
        var nonce = Base64Url(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(nonce);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        if (!await IsActiveAsync(connection, transaction, accountId, deviceId, cancellationToken))
            throw new DeviceNotFoundException();
        await using var insert = new NpgsqlCommand("""
            insert into licensing.device_nonces(
              nonce_hash,account_id,device_id,expires_at,created_at)
            values(@hash,@account,@device,@expires,@now)
            """, connection, transaction);
        insert.Parameters.AddWithValue("hash", hash);
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("device", deviceId);
        insert.Parameters.AddWithValue("expires", expires);
        insert.Parameters.AddWithValue("now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(deviceId, nonce, expires);
    }
    public async Task<int> ConsumeProofAsync(
        Guid accountId, Guid deviceId, DeviceLeaseProof proof, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(proof.Nonce) || string.IsNullOrWhiteSpace(proof.Signature))
            throw new DeviceProofException();
        byte[] signature;
        try { signature = Convert.FromBase64String(proof.Signature); }
        catch (FormatException ex) { throw new DeviceProofException("Malformed signature.", ex); }

        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select n.expires_at,n.consumed_at,d.public_key,d.lease_version,d.revoked_at is not null
            from licensing.device_nonces n
            join licensing.devices d on d.device_id=n.device_id and d.account_id=n.account_id
            where n.account_id=@account and n.device_id=@device and n.nonce_hash=@hash
            for update of n
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        command.Parameters.AddWithValue("hash", Hash(proof.Nonce));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new DeviceProofException();
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(0);
        var consumed = !reader.IsDBNull(1);
        var publicKey = reader.GetString(2);
        var version = reader.GetInt32(3);
        var revoked = reader.GetBoolean(4);
        await reader.DisposeAsync();
        if (consumed || revoked || expiresAt <= now) throw new DeviceProofException();

        using var ecdsa = ImportKey(publicKey);
        if (!ecdsa.VerifyData(Encoding.UTF8.GetBytes(proof.Nonce), signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new DeviceProofException();
        await using var consume = new NpgsqlCommand(
            "update licensing.device_nonces set consumed_at=@now where nonce_hash=@hash and consumed_at is null",
            connection, transaction);
        consume.Parameters.AddWithValue("now", now);
        consume.Parameters.AddWithValue("hash", Hash(proof.Nonce));
        if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1) throw new DeviceProofException();

        await using (var seen = new NpgsqlCommand("""
            update licensing.devices
            set last_seen_at=@now
            where account_id=@account and device_id=@device and revoked_at is null
            """, connection, transaction))
        {
            seen.Parameters.AddWithValue("now", now);
            seen.Parameters.AddWithValue("account", accountId);
            seen.Parameters.AddWithValue("device", deviceId);
            if (await seen.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new DeviceNotFoundException();
        }

        await transaction.CommitAsync(cancellationToken);
        return version;
    }
    public async Task<bool> IsActiveAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        var active = await IsActiveAsync(connection, transaction, accountId, deviceId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return active;
    }

    public async Task<int> GetActiveVersionAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        await using var command = new NpgsqlCommand(
            "select lease_version from licensing.devices where account_id=@account and device_id=@device and revoked_at is null",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return value is int version ? version : throw new DeviceNotFoundException();
    }

    public async Task RecordLeaseAsync(Guid accountId, Guid deviceId, Guid leaseId,
        string keyId, int version, string tokenHash, string featuresHash,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAccountAsync(connection, transaction, accountId, cancellationToken);
        if (!await IsActiveAsync(connection, transaction, accountId, deviceId, cancellationToken))
            throw new DeviceNotFoundException();
        await using var insert = new NpgsqlCommand("""
            insert into licensing.offline_leases(
              lease_id,account_id,device_id,key_id,lease_version,token_hash,features_hash,issued_at,expires_at)
            values(@lease,@account,@device,@key,@version,@token,@features,@issued,@expires)
            """, connection, transaction);
        insert.Parameters.AddWithValue("lease", leaseId);
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("device", deviceId);
        insert.Parameters.AddWithValue("key", keyId);
        insert.Parameters.AddWithValue("version", version);
        insert.Parameters.AddWithValue("token", tokenHash);
        insert.Parameters.AddWithValue("features", featuresHash);
        insert.Parameters.AddWithValue("issued", issuedAt);
        insert.Parameters.AddWithValue("expires", expiresAt);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> IsActiveAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, Guid deviceId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from licensing.devices where account_id=@account and device_id=@device and revoked_at is null)",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("device", deviceId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
    private static async Task<ExistingDevice?> ReadByKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        Guid idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select device_id,name,payload_hash,revoked_at is not null
            from licensing.devices where account_id=@account and idempotency_key=@key
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3));
    }

    private static async Task<AccountState?> ReadAccountAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select base_role,blocked_at is not null from licensing.accounts where account_id=@account",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetString(0), reader.GetBoolean(1));
    }

    private static async Task<int> CountActiveAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select count(*) from licensing.devices where account_id=@account and revoked_at is null",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        return checked((int)(long)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    private static async Task LockAccountAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@account,20260918))",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private static void ValidateRegistration(DeviceRegistration request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PublicKey);
        if (request.IdempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.");
        if (request.Name.Length > 80) throw new ArgumentException("Device name is too long.");
        if (!string.Equals(request.Platform, "windows", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only Windows managed devices are supported.");
        using var key = ImportKey(request.PublicKey);
        if (key.KeySize != 256) throw new ArgumentException("Device key must use P-256.");
    }

    private static string PayloadHash(DeviceRegistration request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            request.Name,
            Platform = request.Platform.ToLowerInvariant(),
            request.PublicKey
        });
        return Hash(canonical);
    }

    private static ECDsa ImportKey(string publicKey)
    {
        try
        {
            var bytes = Convert.FromBase64String(publicKey);
            var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(bytes, out var read);
            if (read != bytes.Length) throw new CryptographicException("Trailing key data.");
            return key;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new ArgumentException("Device public key is invalid.", nameof(publicKey), ex);
        }
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task SetAccountAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select set_config('vg.account_id',@account,true)", connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource) await _dataSource.DisposeAsync();
    }

    private sealed record ExistingDevice(Guid DeviceId, string Name, string PayloadHash, bool Revoked);
    private sealed record AccountState(string Role, bool Blocked);
}