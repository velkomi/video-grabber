using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

public sealed record ClientUpdateDecision(bool UpdateAvailable, bool ServicesChanged)
{
    public static ClientUpdateDecision Evaluate(string currentVersion, ClientServiceEndpoints currentServices,
        ClientReleaseManifest verifiedManifest, DateTimeOffset now)
    {
        if (verifiedManifest.ExpiresAt <= now) return new(false, false);
        return new(ClientReleaseVersion.CompareForVideoGrabber(verifiedManifest.Release.Version, currentVersion) > 0,
            !ClientReleaseCache.SameServices(currentServices, verifiedManifest.Services));
    }
}
