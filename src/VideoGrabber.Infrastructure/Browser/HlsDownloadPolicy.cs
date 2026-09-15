namespace VideoGrabber.Infrastructure.Browser;

public static class HlsDownloadPolicy
{
    public static async Task<T> RunVerifiedAsync<T>(MediaCandidate candidate, MediaDownloadPlan plan,
        Func<Task<bool>> verify, Func<Task<T>> download, Func<string?, T> reject)
    {
        if (!plan.IsResolved) return reject(plan.Error);
        if (string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase) && !await verify())
            return reject(null);
        return await download();
    }

    public static bool IsVerifiedClearLeaf(HlsPreflightFetchResult result)
        => result.Success && !result.Blocked && IsClearMediaLeaf(result.Info);

    public static bool IsClearMediaLeaf(HlsManifestInfo? info) => info is { IsMaster: false } && IsAllowed(info);

    public static void UpdateVerifiedClearLeafCache(System.Collections.Concurrent.ConcurrentDictionary<string, byte> cache,
        Uri source, HlsManifestInfo? info)
    {
        if (IsClearMediaLeaf(info)) cache[source.AbsoluteUri] = 0;
        else cache.TryRemove(source.AbsoluteUri, out _);
    }

    public static bool IsAllowed(HlsManifestInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return !info.IsEncrypted;
    }

    public static bool AreSelectedTracksVerified(MediaCandidate candidate, MediaDownloadPlan plan, IReadOnlySet<string> verifiedClear)
    {
        if (!plan.IsResolved) return false;
        if (!string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase)) return true;
        if (candidate.HlsManifest is null || !IsAllowed(candidate.HlsManifest)) return false;
        if (!candidate.HlsManifest.IsMaster) return true;
        var selected = new List<Uri>();
        if (plan.HlsVideoSource is not null) selected.Add(plan.HlsVideoSource);
        if (plan.HlsAudioSource is not null) selected.Add(plan.HlsAudioSource);
        if (selected.Count == 0 && plan.ResolvedHlsLeaf && plan.Source != candidate.Source) selected.Add(plan.Source);
        return selected.Count > 0 && selected.All(uri => uri != candidate.Source && verifiedClear.Contains(uri.AbsoluteUri));
    }
}
