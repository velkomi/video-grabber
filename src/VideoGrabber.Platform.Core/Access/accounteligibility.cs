using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Core.Access;

public static class AccountEligibility
{
    public static bool CanUseProtectedDownloads(AccountProfile account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Blocked) return false;
        if (account.Role == "owner_admin") return true;
        return account.PrimaryAuthProvider is "google" or "email";
    }
}
