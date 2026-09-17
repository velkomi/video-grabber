using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Access;

public sealed class TimeAccessRequiredException : Exception;

public sealed record LeaseSigningKeyOptions(string KeyId, byte[] PrivateKeyPkcs8)
{
    public static LeaseSigningKeyOptions FromConfiguration(IConfiguration configuration)
    {
        var keyId = configuration["VG_PLATFORM_LEASE_KEY_ID"]
            ?? throw new InvalidOperationException("Offline lease key id is required.");
        var encoded = configuration["VG_PLATFORM_LEASE_SIGNING_KEY_PKCS8"]
            ?? throw new InvalidOperationException("Offline lease signing key is required.");
        return new(keyId, Convert.FromBase64String(encoded));
    }
}

public sealed class OfflineLeaseService : IDisposable
{
    private const string Issuer = "videograbber-licensing";
    private const string Audience = "videograbber-managed";
    private readonly DeviceStore _devices;
    private readonly GrantStore _grants;
    private readonly IAccountStore _accounts;
    private readonly TimeProvider _clock;
    private readonly LeaseSigningKeyOptions _options;
    private readonly ECDsa _signingKey;

    public OfflineLeaseService(DeviceStore devices, GrantStore grants, IAccountStore accounts,
        TimeProvider clock, LeaseSigningKeyOptions options)
    {
        _devices = devices;
        _grants = grants;
        _accounts = accounts;
        _clock = clock;
        _options = options;
        _signingKey = ECDsa.Create();
        _signingKey.ImportPkcs8PrivateKey(options.PrivateKeyPkcs8, out var read);
        if (read != options.PrivateKeyPkcs8.Length || _signingKey.KeySize != 256)
            throw new InvalidOperationException("Offline lease key must be a P-256 PKCS#8 key.");
    }
    public async Task<SignedOfflineLease> IssueAsync(
        Guid accountId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var profile = await _accounts.ReadAsync(accountId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Account was not found.");
        var access = await _grants.EvaluateAsync(accountId, cancellationToken);
        if (profile.Blocked || !access.CanEdit)
            throw new TimeAccessRequiredException();

        var version = await _devices.GetActiveVersionAsync(accountId, deviceId, cancellationToken);
        var now = _clock.GetUtcNow();
        var expiresAt = BoundExpiry(now, access.ValidUntil, profile.Role == "owner_admin");
        if (expiresAt <= now) throw new TimeAccessRequiredException();
        var leaseId = Guid.NewGuid();
        var features = new[] { "download", "edit" };
        var token = CreateToken(accountId, deviceId, leaseId, version,
            profile.Role, features, now, expiresAt);
        var tokenHash = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var featuresHash = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", features))));
        await _devices.RecordLeaseAsync(accountId, deviceId, leaseId, _options.KeyId,
            version, tokenHash, featuresHash, now, expiresAt, cancellationToken);
        return new(token, _options.KeyId);
    }

    public static DateTimeOffset BoundExpiry(
        DateTimeOffset now,
        DateTimeOffset? grantEnd,
        bool admin)
    {
        var maximum = now.AddHours(admin ? 72 : 24);
        return grantEnd is { } end && end < maximum ? end : maximum;
    }

    public IReadOnlyList<LeasePublicKey> PublicKeys()
    {
        var parameters = _signingKey.ExportParameters(false);
        return [new LeasePublicKey(_options.KeyId, "ES256",
            Base64Url(parameters.Q.X!), Base64Url(parameters.Q.Y!))];
    }
    private string CreateToken(Guid accountId, Guid deviceId, Guid leaseId, int version,
        string roleClass, string[] features, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, accountId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, leaseId.ToString("D")),
            new("account_id", accountId.ToString("D")),
            new("device_id", deviceId.ToString("D")),
            new("lease_version", version.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("role_class", roleClass),
            new("lease_schema", "1")
        };
        claims.AddRange(features.Select(feature => new Claim("feature", feature)));
        var key = new ECDsaSecurityKey(_signingKey)
        {
            KeyId = _options.KeyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims,
            issuedAt.UtcDateTime, expiresAt.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256));
        token.Header[JwtHeaderParameterNames.Kid] = _options.KeyId;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();

    public void Dispose() => _signingKey.Dispose();
}