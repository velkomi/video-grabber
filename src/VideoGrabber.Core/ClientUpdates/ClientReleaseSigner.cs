using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Core.ClientUpdates;

public static class ClientReleaseSigner
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    public static SignedClientRelease Sign(ClientReleaseManifest manifest, RSA key, string keyId, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ClientReleaseVerifier.ValidateManifest(manifest, "videograbber-main", "preview", (clock ?? TimeProvider.System).GetUtcNow());
        ValidateKeyId(keyId);
        if (key.KeySize is < 2048 or > 8192) throw new ClientReleaseRejectedException("key");
        var payload = SerializePayload(manifest);
        var envelope = new SignedClientRelease(keyId, Convert.ToBase64String(payload),
            Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        _ = SerializeEnvelope(envelope);
        return envelope;
    }

    public static byte[] SerializePayload(ClientReleaseManifest manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (bytes.Length > ClientReleaseVerifier.MaxPayloadBytes) throw new ClientReleaseRejectedException("size");
        return bytes;
    }

    public static byte[] SerializeEnvelope(SignedClientRelease envelope)
    {
        ValidateEnvelopeFields(envelope);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        if (bytes.Length > ClientReleaseVerifier.MaxEnvelopeBytes) throw new ClientReleaseRejectedException("size");
        return bytes;
    }

    public static SignedClientRelease DeserializeEnvelope(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > ClientReleaseVerifier.MaxEnvelopeBytes) throw new ClientReleaseRejectedException("size");
        var envelope = Deserialize<SignedClientRelease>(bytes);
        ValidateEnvelopeFields(envelope);
        return envelope;
    }

    public static ClientReleaseManifest DeserializePayload(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > ClientReleaseVerifier.MaxPayloadBytes) throw new ClientReleaseRejectedException("size");
        return Deserialize<ClientReleaseManifest>(bytes);
    }

    internal static void ValidateEnvelopeFields(SignedClientRelease envelope)
    {
        if (envelope is null) throw new ClientReleaseRejectedException("format");
        ValidateKeyId(envelope.KeyId);
        if (string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.Signature))
            throw new ClientReleaseRejectedException("format");
        if (envelope.Payload.Length > ((ClientReleaseVerifier.MaxPayloadBytes + 2) / 3) * 4 || envelope.Signature.Length > 1368)
            throw new ClientReleaseRejectedException("size");
    }

    private static void ValidateKeyId(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 128 || keyId.Any(char.IsControl))
            throw new ClientReleaseRejectedException("key");
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions) ?? throw new ClientReleaseRejectedException("format");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new ClientReleaseRejectedException("format");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ClientReleaseRejectedException("format");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
