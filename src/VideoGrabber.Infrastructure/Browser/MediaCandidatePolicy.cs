namespace VideoGrabber.Infrastructure.Browser;

public static class MediaCandidatePolicy
{
    public static bool CanQueue(MediaCandidate candidate)
    {
        if (!string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase)) return true;
        return candidate.HlsManifest is not null && HlsDownloadPolicy.IsAllowed(candidate.HlsManifest);
    }
}
