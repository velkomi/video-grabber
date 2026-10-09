using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

public sealed record ClientReleaseCacheSnapshot(
    long HighWater, ClientReleaseManifest? Latest, ClientReleaseManifest? Active,
    ClientReleaseManifest? Previous, SignedClientRelease? LatestEnvelope, bool ActiveBlocked);

public sealed class ClientReleaseCache(
    string path, ClientReleaseVerifier verifier, ClientServiceEndpoints bootstrap, TimeProvider? clock = null)
{
    private readonly string _path = Path.GetFullPath(path);
    private readonly object _gate = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private const int MaximumCacheBytes = 3 * ClientReleaseVerifier.MaxEnvelopeBytes + 2048;

    public ClientReleaseCacheSnapshot Read()
    {
        lock (_gate)
        {
            try { return Snapshot(ReadRecord()); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ClientReleaseRejectedException)
            { return new(0, null, null, null, null, ActiveBlocked: true); }
        }
    }

    public ClientReleaseManifest AcceptChecked(SignedClientRelease envelope)
    {
        lock (_gate)
        {
            using var writer = WriterLock();
            CacheRecord record;
            try { record = ReadRecord(); }
            catch (Exception e) when (e is IOException or JsonException or ClientReleaseRejectedException)
            {
                var highest = ReadHighWater() ?? throw new ClientReleaseRejectedException("cache_recovery_required");
                record = new(highest, null, null, RequiresApproval: true);
            }
            var old = Snapshot(record);
            var manifest = verifier.Verify(envelope, old.HighWater);
            if (record.Latest is not null && manifest.Sequence == old.HighWater && PayloadHash(record.Latest) != PayloadHash(envelope))
                throw new ClientReleaseRejectedException("sequence_substitution");
            var active = record.Active;
            var activeServices = old.Active?.Services ?? bootstrap;
            if (!record.RequiresApproval && SameServices(activeServices, manifest.Services)) active = envelope;
            Write(new(envelope, active, record.Previous, record.RequiresApproval));
            return manifest;
        }
    }

    public void ApproveLatestServices()
    {
        lock (_gate)
        {
            using var writer = WriterLock();
            var record = ReadRecord();
            if (record.Latest is null) throw new ClientReleaseRejectedException("cache_empty");
            _ = verifier.Verify(record.Latest, Snapshot(record).HighWater);
            var previous = record.Active;
            Write(record with { Active = record.Latest, Previous = previous, RequiresApproval = false });
        }
    }

    public void RestorePreviousServices()
    {
        lock (_gate)
        {
            using var writer = WriterLock();
            var record = ReadRecord();
            if (record.Previous is null) throw new ClientReleaseRejectedException("cache_previous_missing");
            _ = verifier.Verify(record.Previous);
            Write(record with { Active = record.Previous, Previous = record.Active });
        }
    }

    private ClientReleaseCacheSnapshot Snapshot(CacheRecord record)
    {
        var latest = record.Latest is null ? null : verifier.VerifyCached(record.Latest);
        var active = record.Active is null ? null : verifier.VerifyCached(record.Active);
        var previous = record.Previous is null ? null : verifier.VerifyCached(record.Previous);
        if ((active is not null && (latest is null || active.Sequence > latest.Sequence)) ||
            (previous is not null && (latest is null || previous.Sequence > latest.Sequence)))
            throw new ClientReleaseRejectedException("cache_sequence");
        foreach (var item in new[] { record.Active, record.Previous })
            if (item is not null && verifier.VerifyCached(item).Sequence == latest?.Sequence &&
                PayloadHash(item) != PayloadHash(record.Latest!))
                throw new ClientReleaseRejectedException("cache_sequence_substitution");
        return new(latest?.Sequence ?? 0, latest, active, previous, record.Latest,
            record.RequiresApproval || (active is not null && active.ExpiresAt <= _clock.GetUtcNow()));
    }

    private CacheRecord ReadRecord()
    {
        var highWater = ReadHighWater();
        if (!File.Exists(_path)) return highWater is null ? new(null, null, null, false) : new(highWater, null, null, true);
        var info = new FileInfo(_path);
        if (info.Length > MaximumCacheBytes || info.LinkTarget is not null) throw new ClientReleaseRejectedException("cache_size");
        var bytes = File.ReadAllBytes(_path);
        if (bytes.Length > MaximumCacheBytes) throw new ClientReleaseRejectedException("cache_size");
        using (var document = JsonDocument.Parse(bytes))
        {
            RequireUniqueMembers(document.RootElement);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !new HashSet<string>(document.RootElement.EnumerateObject().Select(x => x.Name), StringComparer.Ordinal)
                    .SetEquals(["latest", "active", "previous", "requiresApproval"]))
                throw new ClientReleaseRejectedException("cache_format");
        }
        var record = JsonSerializer.Deserialize<CacheRecord>(bytes, ClientReleaseSigner.JsonOptions)
            ?? throw new ClientReleaseRejectedException("cache_format");
        if (record.Latest is null) throw new ClientReleaseRejectedException("cache_format");
        _ = Snapshot(record);
        if (highWater is not null)
        {
            var highestManifest = verifier.VerifyCached(highWater);
            var latestManifest = verifier.VerifyCached(record.Latest);
            if (highestManifest.Sequence == latestManifest.Sequence && PayloadHash(highWater) != PayloadHash(record.Latest))
                throw new ClientReleaseRejectedException("cache_sequence_substitution");
            if (highestManifest.Sequence > latestManifest.Sequence) record = record with { Latest = highWater };
        }
        return record;
    }

    private FileStream WriterLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var lockPath = _path + ".lock";
        if (new FileInfo(lockPath).LinkTarget is not null) throw new ClientReleaseRejectedException("cache_link");
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private void Write(CacheRecord record)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, ClientReleaseSigner.JsonOptions);
        if (bytes.Length > MaximumCacheBytes) throw new ClientReleaseRejectedException("cache_size");
        if (record.Latest is null) throw new ClientReleaseRejectedException("cache_format");
        // Persist the signed high-water proof first: interrupted state writes cannot enable replay.
        AtomicWrite(_path + ".highwater.json", ClientReleaseSigner.SerializeEnvelope(record.Latest));
        AtomicWrite(_path, bytes);
    }

    private SignedClientRelease? ReadHighWater()
    {
        var path = _path + ".highwater.json";
        var info = new FileInfo(path);
        if (!info.Exists) return null;
        if (info.Length > ClientReleaseVerifier.MaxEnvelopeBytes || info.LinkTarget is not null)
            throw new ClientReleaseRejectedException("cache_highwater");
        var envelope = ClientReleaseSigner.DeserializeEnvelope(File.ReadAllBytes(path));
        _ = verifier.VerifyCached(envelope);
        return envelope;
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static bool SameServices(ClientServiceEndpoints a, ClientServiceEndpoints b)
        => a.ApiBase.Equals(b.ApiBase) && a.WebsiteBase.Equals(b.WebsiteBase);

    private static string PayloadHash(SignedClientRelease envelope)
        => Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(envelope.Payload)));

    private static void RequireUniqueMembers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ClientReleaseRejectedException("cache_format");
                RequireUniqueMembers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RequireUniqueMembers(item);
    }

    private sealed record CacheRecord(SignedClientRelease? Latest, SignedClientRelease? Active,
        SignedClientRelease? Previous, bool RequiresApproval);
}
