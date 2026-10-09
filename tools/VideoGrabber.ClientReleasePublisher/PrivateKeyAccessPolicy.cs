using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace VideoGrabber.ClientReleasePublisher;

[SupportedOSPlatform("windows")]
internal static class PrivateKeyAccessPolicy
{
    public static bool Allows(SecurityIdentifier currentUser, SecurityIdentifier? owner, IEnumerable<FileSystemAccessRule> rules)
    {
        if (owner is null || !currentUser.Equals(owner)) return false;
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        return !rules.Any(rule => rule.AccessControlType == AccessControlType.Allow
            && !rule.IdentityReference.Equals(currentUser) && !rule.IdentityReference.Equals(system));
    }
}
