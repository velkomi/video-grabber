using System.Security.Cryptography;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Core.ClientUpdates;

public sealed class ClientReleaseRejectedException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class ClientReleaseVerifier(
    IReadOnlyDictionary<string, string> keys, string realm = "videograbber-main",
    string channel = "preview", TimeProvider? clock = null)
{
    public const int MaxEnvelopeBytes = 64 * 1024;
    public const int MaxPayloadBytes = 32 * 1024;
    public const long MaxInstallerBytes = 1024L * 1024 * 1024;

    public ClientReleaseManifest Verify(SignedClientRelease envelope, long minimumSequence = 0)
        => VerifyCore(envelope, minimumSequence, allowExpired: false);

    // Cache classification only: callers must block online use of an expired active directory.
    public ClientReleaseManifest VerifyCached(SignedClientRelease envelope, long minimumSequence = 0)
        => VerifyCore(envelope, minimumSequence, allowExpired: true);

    private ClientReleaseManifest VerifyCore(SignedClientRelease envelope, long minimumSequence, bool allowExpired)
    {
        _ = ClientReleaseSigner.SerializeEnvelope(envelope);
        if (!keys.TryGetValue(envelope.KeyId, out var publicKey)) throw new ClientReleaseRejectedException("key");
        var payload = Decode(envelope.Payload, MaxPayloadBytes);
        var signature = Decode(envelope.Signature, 1024);
        using var rsa = RSA.Create();
        try { rsa.ImportFromPem(publicKey); }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        { throw new ClientReleaseRejectedException("key"); }
        if (rsa.KeySize is < 2048 or > 8192) throw new ClientReleaseRejectedException("key");
        try
        {
            if (signature.Length != rsa.KeySize / 8 || !rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new ClientReleaseRejectedException("signature");
        }
        catch (CryptographicException) { throw new ClientReleaseRejectedException("signature"); }
        var manifest = ClientReleaseSigner.DeserializePayload(payload);
        ValidateManifestCore(manifest, realm, channel, (clock ?? TimeProvider.System).GetUtcNow(), allowExpired);
        if (manifest.Sequence < minimumSequence) throw new ClientReleaseRejectedException("sequence");
        return manifest;
    }

    public static void ValidateManifest(ClientReleaseManifest manifest,
        string realm, string channel, DateTimeOffset now)
        => ValidateManifestCore(manifest, realm, channel, now, allowExpired: false);

    private static void ValidateManifestCore(ClientReleaseManifest m,
        string realm, string channel, DateTimeOffset now, bool allowExpired)
    {
        if (m is null || m.SchemaVersion != 1 || m.Product != "videograbber" ||
            m.AccountRealm != realm || m.Channel != channel || m.Services is null ||
            m.Release is null || m.Release.Installer is null)
            throw new ClientReleaseRejectedException("contract");
        if (m.Sequence <= 0) throw new ClientReleaseRejectedException("sequence");
        if ((!allowExpired && m.ExpiresAt <= now) || m.IssuedAt - now > TimeSpan.FromMinutes(5) ||
            m.ExpiresAt <= m.IssuedAt || m.ExpiresAt - m.IssuedAt > TimeSpan.FromDays(366) ||
            m.Release.PublishedAt == default || m.Release.PublishedAt - now > TimeSpan.FromMinutes(5))
            throw new ClientReleaseRejectedException("time");
        if (m.Release.Notes is null || m.Release.Notes.Length > 1500) throw new ClientReleaseRejectedException("notes");
        ValidatePublicHttpsUrl(m.Services.ApiBase);
        ValidatePublicHttpsUrl(m.Services.WebsiteBase);
        if (m.Services.ApiBase.AbsolutePath != "/" || m.Services.ApiBase.Query.Length != 0 || m.Services.WebsiteBase.Query.Length != 0)
            throw new ClientReleaseRejectedException("url");
        _ = ParseVersion(m.Release.Version);
        ValidateArtifact(m.Release.Installer);
        if (!string.Equals(m.Release.Installer.Version, m.Release.Version, StringComparison.Ordinal))
            throw new ClientReleaseRejectedException("version");
        if (m.Release.RollbackInstaller is { } rollback)
        {
            ValidateArtifact(rollback);
            if (ClientReleaseVersion.CompareForVideoGrabber(rollback.Version, m.Release.Version) >= 0)
                throw new ClientReleaseRejectedException("version");
        }
    }

    public static void ValidatePublicHttpsUrl(Uri uri)
    {
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns)
            throw new ClientReleaseRejectedException("url");
        var host = uri.IdnHost.TrimEnd('.');
        var labels = host.Split('.');
        if (labels.Length < 2 || host.Length > 253 ||
            labels.Any(x => x.Length is < 1 or > 63 || x[0] == '-' || x[^1] == '-' ||
                x.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) ||
            labels[^1].ToLowerInvariant() is "localhost" or "local" or "internal" or "lan" or "home" or "invalid" or "test" or "onion")
            throw new ClientReleaseRejectedException("url");
    }

    private static void ValidateArtifact(ClientUpdateArtifact artifact)
    {
        _ = ParseVersion(artifact.Version);
        ValidatePublicHttpsUrl(artifact.Url);
        if (artifact.SizeBytes is <= 0 or > MaxInstallerBytes || artifact.Sha256 is null ||
            artifact.Sha256.Length != 64 || artifact.Sha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ClientReleaseRejectedException("artifact");
    }

    private static ClientReleaseVersion ParseVersion(string version)
    {
        try { return ClientReleaseVersion.Parse(version); }
        catch (FormatException) { throw new ClientReleaseRejectedException("version"); }
    }

    private static byte[] Decode(string value, int maxBytes)
    {
        if (value.Length % 4 != 0 || value.Any(char.IsWhiteSpace)) throw new ClientReleaseRejectedException("format");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { throw new ClientReleaseRejectedException("format"); }
        if (bytes.Length > maxBytes) throw new ClientReleaseRejectedException("size");
        return bytes;
    }
}
