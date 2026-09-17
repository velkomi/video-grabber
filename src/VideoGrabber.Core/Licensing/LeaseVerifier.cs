using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Core.Licensing;

public static class LeaseVerifier
{
    private const string ExpectedIssuer = "videograbber-licensing";
    private const string ExpectedAudience = "videograbber-managed";
    private static readonly TimeSpan RollbackTolerance = TimeSpan.FromMinutes(2);

    public static OfflineLeaseClaims Validate(
        SignedOfflineLease lease,
        IReadOnlyDictionary<string, string> publicKeys,
        Guid accountId,
        Guid deviceId,
        DateTimeOffset now,
        DateTimeOffset lastSeenUtc)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(publicKeys);
        if (now < lastSeenUtc - RollbackTolerance)
            throw Denied("clock_rollback");

        var parts = lease.Token.Split('.');
        if (parts.Length != 3) throw Denied("lease_format");
        using var header = Parse(parts[0]);
        using var payload = Parse(parts[1]);
        var h = header.RootElement;
        var p = payload.RootElement;

        var alg = RequiredString(h, "alg");
        var kid = RequiredString(h, "kid");
        if (!string.Equals(alg, "ES256", StringComparison.Ordinal) ||
            !string.Equals(kid, lease.KeyId, StringComparison.Ordinal) ||
            !publicKeys.TryGetValue(kid, out var publicKeyPem))
            throw Denied("lease_key");

        var signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        byte[] signature;
        try { signature = Decode(parts[2]); }
        catch (FormatException) { throw Denied("lease_signature"); }
        if (signature.Length != 64) throw Denied("lease_signature");
        using var key = ECDsa.Create();
        try { key.ImportFromPem(publicKeyPem); }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        { throw Denied("lease_key"); }
        if (key.KeySize != 256 || !key.VerifyData(signed, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw Denied("lease_signature");

        if (!string.Equals(RequiredString(p, "iss"), ExpectedIssuer, StringComparison.Ordinal) ||
            !AudienceMatches(p) || RequiredString(p, "lease_schema") != "1")
            throw Denied("lease_contract");

        var account = RequiredGuid(p, "account_id");
        var subject = RequiredGuid(p, "sub");
        var device = RequiredGuid(p, "device_id");
        var leaseId = RequiredGuid(p, "jti");
        if (account != accountId || subject != accountId || device != deviceId)
            throw Denied("lease_binding");
        var versionText = RequiredString(p, "lease_version");
        if (!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            throw Denied("lease_version");

        var issuedAt = UnixTime(p, "iat");
        var expiresAt = UnixTime(p, "exp");
        if (now >= expiresAt || issuedAt > now + RollbackTolerance || expiresAt <= issuedAt)
            throw Denied("lease_time");

        var role = RequiredString(p, "role_class");
        var features = ReadFeatures(p);
        if (features.Length == 0) throw Denied("lease_features");
        return new OfflineLeaseClaims(account, device, leaseId, version, role, features, issuedAt, expiresAt);
    }

    private static JsonDocument Parse(string encoded)
    {
        try { return JsonDocument.Parse(Decode(encoded)); }
        catch (Exception ex) when (ex is FormatException or JsonException)
        { throw Denied("lease_format"); }
    }

    private static string RequiredString(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property)) throw Denied("lease_claim");
        if (property.ValueKind == JsonValueKind.String)
            return property.GetString() ?? throw Denied("lease_claim");
        if (property.ValueKind == JsonValueKind.Number) return property.GetRawText();
        throw Denied("lease_claim");
    }

    private static Guid RequiredGuid(JsonElement value, string name)
        => Guid.TryParse(RequiredString(value, name), out var id) ? id : throw Denied("lease_claim");

    private static DateTimeOffset UnixTime(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || !property.TryGetInt64(out var seconds))
            throw Denied("lease_claim");
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { throw Denied("lease_claim"); }
    }

    private static bool AudienceMatches(JsonElement value)
    {
        if (!value.TryGetProperty("aud", out var aud)) return false;
        if (aud.ValueKind == JsonValueKind.String)
            return string.Equals(aud.GetString(), ExpectedAudience, StringComparison.Ordinal);
        if (aud.ValueKind == JsonValueKind.Array)
            return aud.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                string.Equals(x.GetString(), ExpectedAudience, StringComparison.Ordinal));
        return false;
    }

    private static string[] ReadFeatures(JsonElement value)
    {
        if (!value.TryGetProperty("feature", out var feature)) return [];
        if (feature.ValueKind == JsonValueKind.String)
            return [feature.GetString()!];
        if (feature.ValueKind == JsonValueKind.Array)
            return feature.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!).Distinct(StringComparer.Ordinal).ToArray();
        return [];
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static UnauthorizedAccessException Denied(string reason) => new(reason);
}