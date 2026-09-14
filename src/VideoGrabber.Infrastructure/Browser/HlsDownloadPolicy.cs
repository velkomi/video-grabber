namespace VideoGrabber.Infrastructure.Browser;

public static class HlsDownloadPolicy
{
    public static bool IsAllowed(HlsManifestInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return !info.IsEncrypted;
    }

    public static bool AreSelectedTracksVerified(MediaCandidate candidate, MediaDownloadPlan plan, IReadOnlySet<string> verifiedClear)
    {
        if (!string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase)) return true;
        if (candidate.HlsManifest is null || !IsAllowed(candidate.HlsManifest)) return false;
        if (!candidate.HlsManifest.IsMaster) return true;
        var selected = new List<Uri>();
        if (plan.HlsVideoSource is not null) selected.Add(plan.HlsVideoSource);
        if (plan.HlsAudioSource is not null) selected.Add(plan.HlsAudioSource);
        if (selected.Count == 0 && plan.Source != candidate.Source) selected.Add(plan.Source);
        return selected.All(uri => verifiedClear.Contains(uri.AbsoluteUri));
    }
}