using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VideoGrabber.Platform.Api.Auth;

namespace VideoGrabber.Platform.Api.Admin;

public sealed record AdminMfaStatus(bool IsAdmin, bool Enrolled);
public sealed record AdminMfaEnrollment(string Secret, string OtpAuthUri);
public sealed record AdminMfaVerifyRequest(string Code);
public sealed record AdminMfaToken(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class AdminMfaService : IAsyncDisposable
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockLifetime = TimeSpan.FromMinutes(5);

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly SessionJwtOptions _jwt;
    private readonly byte[] _encryptionKey;

    public AdminMfaService(
        string adminDsn,
        TimeProvider clock,
        SessionJwtOptions jwt,
        string encryptionKey)
    {
        _dataSource = NpgsqlDataSource.Create(adminDsn);
        _clock = clock;
        _jwt = jwt;
        _encryptionKey = ReadKey(encryptionKey);
    }

    public async Task<AdminMfaStatus> StatusAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var isAdmin = await IsAdminAsync(
            connection, null, accountId, cancellationToken);
        if (!isAdmin)
            return new(false, false);

        await using var command = new NpgsqlCommand("""
            select enabled
            from licensing.admin_mfa
            where account_id=@account
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        var enabled = await command.ExecuteScalarAsync(cancellationToken);
        return new(true, enabled is true);
    }

    public async Task<AdminMfaEnrollment> BeginEnrollAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var secret = RandomNumberGenerator.GetBytes(20);
        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);
            if (!await IsAdminAsync(
                    connection, transaction, accountId, cancellationToken))
                throw new UnauthorizedAccessException();

            await using (var read = new NpgsqlCommand("""
                select enabled
                from licensing.admin_mfa
                where account_id=@account
                """, connection, transaction))
            {
                read.Parameters.AddWithValue("account", accountId);
                if (await read.ExecuteScalarAsync(cancellationToken) is true)
                    throw new InvalidOperationException(
                        "Admin MFA is already enrolled.");
            }

            var cipher = Encrypt(secret);
            await using var command = new NpgsqlCommand("""
                insert into licensing.admin_mfa(
                  account_id,secret_cipher,enabled,failed_attempts,
                  locked_until,created_at,updated_at)
                values(@account,@secret,false,0,null,now(),now())
                on conflict(account_id) do update set
                  secret_cipher=excluded.secret_cipher,
                  enabled=false,
                  failed_attempts=0,
                  locked_until=null,
                  updated_at=now()
                """, connection, transaction);
            command.Parameters.AddWithValue("account", accountId);
            command.Parameters.AddWithValue("secret", cipher);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var encoded = Base32Encode(secret);
            var label = Uri.EscapeDataString(
                "VideoGrabber:" + accountId.ToString("N")[..8]);
            var uri =
                $"otpauth://totp/{label}?secret={encoded}"
                + "&issuer=VideoGrabber&algorithm=SHA1&digits=6&period=30";
            return new(encoded, uri);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public async Task<AdminMfaToken> ConfirmAsync(
        Guid accountId,
        string code,
        CancellationToken cancellationToken)
        => await VerifyCoreAsync(
            accountId,
            code,
            requireEnabled: false,
            enableOnSuccess: true,
            cancellationToken);

    public async Task<AdminMfaToken> VerifyAsync(
        Guid accountId,
        string code,
        CancellationToken cancellationToken)
        => await VerifyCoreAsync(
            accountId,
            code,
            requireEnabled: true,
            enableOnSuccess: false,
            cancellationToken);

    private async Task<AdminMfaToken> VerifyCoreAsync(
        Guid accountId,
        string code,
        bool requireEnabled,
        bool enableOnSuccess,
        CancellationToken cancellationToken)
    {
        ValidateCodeShape(code);
        var now = _clock.GetUtcNow();

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        if (!await IsAdminAsync(
                connection, transaction, accountId, cancellationToken))
            throw new UnauthorizedAccessException();

        byte[] cipher;
        bool enabled;
        int failedAttempts;
        DateTimeOffset? lockedUntil;

        await using (var select = new NpgsqlCommand("""
            select secret_cipher,enabled,failed_attempts,locked_until
            from licensing.admin_mfa
            where account_id=@account
            for update
            """, connection, transaction))
        {
            select.Parameters.AddWithValue("account", accountId);
            await using var reader =
                await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException(
                    "Admin MFA enrollment is required.");
            cipher = reader.GetFieldValue<byte[]>(0);
            enabled = reader.GetBoolean(1);
            failedAttempts = reader.GetInt32(2);
            lockedUntil = reader.IsDBNull(3)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(3);
        }

        if (requireEnabled && !enabled)
            throw new InvalidOperationException(
                "Admin MFA enrollment is not confirmed.");
        if (lockedUntil is { } locked && locked > now)
            throw new UnauthorizedAccessException(
                "Admin MFA is temporarily locked.");

        var secret = Decrypt(cipher);
        bool valid;
        try
        {
            valid = VerifyTotp(secret, code, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (!valid)
        {
            failedAttempts++;
            var lockUntil = failedAttempts >= MaxFailedAttempts
                ? now.Add(LockLifetime)
                : (DateTimeOffset?)null;

            await using var fail = new NpgsqlCommand("""
                update licensing.admin_mfa
                set failed_attempts=@attempts,
                    locked_until=@locked,
                    updated_at=now()
                where account_id=@account
                """, connection, transaction);
            fail.Parameters.AddWithValue("attempts", failedAttempts);
            fail.Parameters.AddWithValue(
                "locked",
                (object?)lockUntil ?? DBNull.Value);
            fail.Parameters.AddWithValue("account", accountId);
            await fail.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid MFA code.");
        }

        await using (var success = new NpgsqlCommand("""
            update licensing.admin_mfa
            set enabled=case when @enable then true else enabled end,
                failed_attempts=0,
                locked_until=null,
                updated_at=now()
            where account_id=@account
            """, connection, transaction))
        {
            success.Parameters.AddWithValue("enable", enableOnSuccess);
            success.Parameters.AddWithValue("account", accountId);
            await success.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return IssueAdminToken(accountId, now);
    }

    private AdminMfaToken IssueAdminToken(
        Guid accountId,
        DateTimeOffset issuedAt)
    {
        var expiresAt = issuedAt.Add(TokenLifetime);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_jwt.SigningKey),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                accountId.ToString("D")),
            new Claim(
                "account_id",
                accountId.ToString("D")),
            new Claim(
                "sid",
                Guid.NewGuid().ToString("D")),
            new Claim(
                "mfa_at",
                issuedAt.ToUnixTimeSeconds().ToString()),
            new Claim(
                "token_purpose",
                "admin_mfa")
        };

        var token = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    private static bool VerifyTotp(
        byte[] secret,
        string code,
        DateTimeOffset now)
    {
        var step = (long)(
            now.ToUnixTimeSeconds() / Step.TotalSeconds);
        for (var offset = -1; offset <= 1; offset++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Totp(secret, step + offset)),
                    Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    private static string Totp(byte[] secret, long counter)
    {
        Span<byte> message = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            message[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(message.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000)
            .ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private byte[] Encrypt(byte[] value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[value.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_encryptionKey, 16);
        aes.Encrypt(nonce, value, cipher, tag);
        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(
            cipher,
            0,
            result,
            nonce.Length + tag.Length,
            cipher.Length);
        return result;
    }

    private byte[] Decrypt(byte[] value)
    {
        if (value.Length < 29)
            throw new InvalidDataException("Admin MFA envelope is invalid.");
        var nonce = value.AsSpan(0, 12);
        var tag = value.AsSpan(12, 16);
        var cipher = value.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_encryptionKey, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static byte[] ReadKey(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException(
                "Admin MFA encryption key is required.");
        try
        {
            var key = Convert.FromBase64String(encoded);
            if (key.Length == 32)
                return key;
        }
        catch (FormatException)
        {
        }
        throw new InvalidOperationException(
            "Admin MFA encryption key must be 32 bytes of Base64.");
    }

    private static async Task<bool> IsAdminAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select base_role='owner_admin' and blocked_at is null
            from licensing.accounts
            where account_id=@account
            """, connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static void ValidateCodeShape(string code)
    {
        if (code.Length != 6 || code.Any(ch => !char.IsAsciiDigit(ch)))
            throw new ArgumentException(
                "MFA code must contain exactly six digits.");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(
                    alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0)
            output.Append(alphabet[(buffer << (5 - bitsLeft)) & 31]);
        return output.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(_encryptionKey);
        await _dataSource.DisposeAsync();
    }
}
