using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Core.Tests;

public sealed class LeaseVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_signed_lease_returns_expected_claims()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid();
        var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now.AddHours(2));

        var claims = LeaseVerifier.Validate(lease, Keys(("key-1", key)), account, device, Now, Now.AddMinutes(-1));

        Assert.Equal(account, claims.AccountId);
        Assert.Equal(device, claims.DeviceId);
        Assert.Equal(1, claims.Version);
        Assert.Contains("edit", claims.Features);
        Assert.Equal(Now.AddHours(2), claims.ExpiresAt);
    }

    [Fact]
    public void Clock_rollback_cannot_extend_an_offline_lease()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now.AddHours(2));
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease, Keys(("key-1", key)), account, device,
                Now.AddMinutes(-3), Now));
    }

    [Fact]
    public void Exactly_expired_lease_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now);
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease, Keys(("key-1", key)), account, device, Now, Now));
    }

    [Fact]
    public void Lease_is_bound_to_account_and_device()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now.AddHours(2));
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease, Keys(("key-1", key)), Guid.NewGuid(), device, Now, Now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease, Keys(("key-1", key)), account, Guid.NewGuid(), Now, Now));
    }

    [Theory]
    [InlineData("alg")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("schema")]
    [InlineData("version")]
    public void Changed_security_contract_is_rejected(string mutation)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now.AddHours(2), mutation);
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease, Keys(("key-1", key)), account, device, Now, Now));
    }

    [Fact]
    public void Changed_signature_and_unknown_kid_are_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var lease = Sign(key, "key-1", account, device, Now.AddHours(-1), Now.AddHours(2));
        var parts = lease.Token.Split('.');
        var sig = Base64UrlDecode(parts[2]); sig[0] ^= 0x01;
        var changed = new SignedOfflineLease(parts[0] + "." + parts[1] + "." + Base64Url(sig), "key-1");
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(changed, Keys(("key-1", key)), account, device, Now, Now));
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(lease with { KeyId = "missing" }, Keys(("key-1", key)), account, device, Now, Now));
    }

    [Fact]
    public void Fresh_key_rotation_accepts_both_known_keys_but_not_mismatched_kid()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var account = Guid.NewGuid(); var device = Guid.NewGuid();
        var oldLease = Sign(oldKey, "old", account, device, Now.AddHours(-1), Now.AddHours(1));
        var newLease = Sign(newKey, "new", account, device, Now.AddHours(-1), Now.AddHours(2));
        var keys = Keys(("old", oldKey), ("new", newKey));
        Assert.Equal("guest", LeaseVerifier.Validate(oldLease, keys, account, device, Now, Now).RoleClass);
        Assert.Equal("guest", LeaseVerifier.Validate(newLease, keys, account, device, Now, Now).RoleClass);
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseVerifier.Validate(newLease with { KeyId = "old" }, keys, account, device, Now, Now));
    }

    private static IReadOnlyDictionary<string, string> Keys(params (string Kid, ECDsa Key)[] keys)
        => keys.ToDictionary(x => x.Kid, x => x.Key.ExportSubjectPublicKeyInfoPem(), StringComparer.Ordinal);

    private static SignedOfflineLease Sign(ECDsa key, string kid, Guid account, Guid device,
        DateTimeOffset issued, DateTimeOffset expires, string? mutation = null)
    {
        var header = new Dictionary<string, object?> { ["alg"] = mutation == "alg" ? "HS256" : "ES256", ["kid"] = kid, ["typ"] = "JWT" };
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = mutation == "issuer" ? "evil" : "videograbber-licensing",
            ["aud"] = mutation == "audience" ? "other" : "videograbber-managed",
            ["sub"] = account.ToString("D"), ["jti"] = Guid.NewGuid().ToString("D"),
            ["account_id"] = account.ToString("D"), ["device_id"] = device.ToString("D"),
            ["lease_version"] = mutation == "version" ? "0" : "1",
            ["role_class"] = "guest", ["lease_schema"] = mutation == "schema" ? "2" : "1",
            ["feature"] = new[] { "download", "edit" },
            ["iat"] = issued.ToUnixTimeSeconds(), ["nbf"] = issued.ToUnixTimeSeconds(), ["exp"] = expires.ToUnixTimeSeconds()
        };
        var h = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var p = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var input = Encoding.ASCII.GetBytes(h + "." + p);
        var signature = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new SignedOfflineLease(h + "." + p + "." + Base64Url(signature), kid);
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}