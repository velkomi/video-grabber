using System.Buffers.Binary;
using System.Security.Cryptography;
using VideoGrabber.Platform.Api.Auth;

namespace VideoGrabber.Platform.Api.Jobs;

public sealed class BrowserDownloadTicketService(
    SessionJwtOptions jwt,
    TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public string Create(Guid accountId, Guid jobId)
    {
        if (accountId == Guid.Empty || jobId == Guid.Empty)
            throw new ArgumentException("Account and job are required.");

        var payload = new byte[40];
        accountId.TryWriteBytes(payload.AsSpan(0, 16));
        jobId.TryWriteBytes(payload.AsSpan(16, 16));
        BinaryPrimitives.WriteInt64BigEndian(
            payload.AsSpan(32, 8),
            clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds());

        var signature = HMACSHA256.HashData(jwt.SigningKey, payload);
        var bytes = new byte[payload.Length + signature.Length];
        Buffer.BlockCopy(payload, 0, bytes, 0, payload.Length);
        Buffer.BlockCopy(signature, 0, bytes, payload.Length, signature.Length);
        return Base64Url(bytes);
    }

    public bool TryValidate(
        string ticket,
        out Guid accountId,
        out Guid jobId)
    {
        accountId = Guid.Empty;
        jobId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 256)
            return false;

        try
        {
            var bytes = FromBase64Url(ticket);
            if (bytes.Length != 72) return false;

            var payload = bytes.AsSpan(0, 40);
            var signature = bytes.AsSpan(40, 32);
            var expected = HMACSHA256.HashData(jwt.SigningKey, payload);
            if (!CryptographicOperations.FixedTimeEquals(signature, expected))
                return false;

            accountId = new Guid(payload[..16]);
            jobId = new Guid(payload.Slice(16, 16));
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
                BinaryPrimitives.ReadInt64BigEndian(payload.Slice(32, 8)));
            var now = clock.GetUtcNow();
            return accountId != Guid.Empty
                && jobId != Guid.Empty
                && expiresAt > now
                && expiresAt <= now.Add(Lifetime).AddSeconds(30);
        }
        catch
        {
            accountId = Guid.Empty;
            jobId = Guid.Empty;
            return false;
        }
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var raw = value.Replace('-', '+').Replace('_', '/');
        raw += new string('=', (4 - raw.Length % 4) % 4);
        return Convert.FromBase64String(raw);
    }
}
