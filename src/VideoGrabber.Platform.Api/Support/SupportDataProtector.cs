using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Support;

public sealed record SupportPayload(string Contact, string Message);

public sealed class SupportDataProtector
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _identityKey;
    public SupportDataProtector(IConfiguration configuration)
    {
        var encoded = configuration["VG_SOURCE_ENCRYPTION_KEY"]
            ?? throw new InvalidOperationException("Support encryption material is not configured.");
        var master = Convert.FromBase64String(encoded);
        if (master.Length != 32) throw new InvalidOperationException("Support encryption material must be 32 bytes.");
        try
        {
            _encryptionKey = HMACSHA256.HashData(master, Encoding.UTF8.GetBytes("VideoGrabber.support.encryption.v1"));
            _identityKey = HMACSHA256.HashData(master, Encoding.UTF8.GetBytes("VideoGrabber.support.identity.v1"));
        }
        finally { CryptographicOperations.ZeroMemory(master); }
    }

    public string Digest(string value) => Convert.ToHexString(HMACSHA256.HashData(_identityKey,
        Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public string CallerKey(Guid? account, string contact) => account is { } id
        ? "account:" + id.ToString("N") : "contact:" + Digest(contact.ToLowerInvariant());
    public string RateKey(Guid? account, string contact, string remoteAddress) => account is { } id
        ? "account:" + id.ToString("N") : "guest:" + Digest(contact.ToLowerInvariant() + "\n" + remoteAddress);
    public string RequestHash(SupportRequest request) => Digest(JsonSerializer.Serialize(new
    { request.Topic, request.Contact, request.Message }));

    public byte[] Protect(Guid ticketId, SupportPayload payload)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(payload);
        var result = new byte[1 + 12 + 16 + plain.Length];
        result[0] = 1;
        RandomNumberGenerator.Fill(result.AsSpan(1, 12));
        try
        {
            using var aes = new AesGcm(_encryptionKey, 16);
            aes.Encrypt(result.AsSpan(1, 12), plain, result.AsSpan(29), result.AsSpan(13, 16), ticketId.ToByteArray());
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public SupportPayload Unprotect(Guid ticketId, byte[] protectedPayload)
    {
        if (protectedPayload.Length < 29 || protectedPayload[0] != 1) throw new CryptographicException("Invalid support payload.");
        var plain = new byte[protectedPayload.Length - 29];
        try
        {
            using var aes = new AesGcm(_encryptionKey, 16);
            aes.Decrypt(protectedPayload.AsSpan(1, 12), protectedPayload.AsSpan(29), protectedPayload.AsSpan(13, 16), plain, ticketId.ToByteArray());
            return JsonSerializer.Deserialize<SupportPayload>(plain) ?? throw new CryptographicException("Invalid support payload.");
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
