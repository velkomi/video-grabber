using System.Text.Json;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class OfflineAccessCache(WindowsSessionStore store, Guid accountId, Guid deviceId)
{
    private CachedLease? _cached;

    public async Task SaveAsync(
        SignedOfflineLease lease,
        IReadOnlyDictionary<string, string> publicKeys,
        DateTimeOffset serverUtc,
        CancellationToken cancellationToken)
    {
        var claims = LeaseVerifier.Validate(lease, publicKeys, accountId, deviceId, serverUtc, serverUtc);
        var state = new CachedLease(lease, new Dictionary<string, string>(publicKeys, StringComparer.Ordinal),
            serverUtc, Environment.TickCount64, claims.ExpiresAt);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        try { await store.SaveAsync(bytes, cancellationToken).ConfigureAwait(false); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
        _cached = state;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var bytes = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (bytes is null) { _cached = null; return; }
        try
        {
            _cached = JsonSerializer.Deserialize<CachedLease>(bytes)
                ?? throw new InvalidDataException("Offline lease cache was empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("Offline lease cache was invalid.", ex); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }

    public bool TryAuthorize(ManagedOperation operation, DateTimeOffset now, out OperationPermit? permit)
    {
        permit = null;
        var state = _cached;
        if (state is null) return false;
        if (operation.DeviceId is { } requested && requested != deviceId) return false;
        if (Environment.TickCount64 < state.MonotonicMilliseconds && now < state.LastSeenUtc.AddMinutes(-2)) return false;
        try
        {
            var claims = LeaseVerifier.Validate(state.Lease, state.PublicKeys, accountId, deviceId, now, state.LastSeenUtc);
            var feature = RequiredFeature(operation.Kind);
            if (feature is null || !claims.Features.Contains(feature, StringComparer.Ordinal)) return false;
            _cached = state with { LastSeenUtc = now > state.LastSeenUtc ? now : state.LastSeenUtc,
                MonotonicMilliseconds = Environment.TickCount64 };
            permit = new OperationPermit(operation.IntentId, null, true);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        _cached = null;
        await store.DeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? RequiredFeature(string kind) => kind switch
    {
        "edit" or "mp3" or "transcription" => "edit",
        "direct_download" or "browser_candidate" or "queue_selected" or "queue_all" or "download" => "download",
        _ => null
    };

    private sealed record CachedLease(
        SignedOfflineLease Lease,
        Dictionary<string, string> PublicKeys,
        DateTimeOffset LastSeenUtc,
        long MonotonicMilliseconds,
        DateTimeOffset ExpiresAt);
}