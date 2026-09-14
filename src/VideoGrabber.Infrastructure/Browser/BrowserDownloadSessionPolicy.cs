namespace VideoGrabber.Infrastructure.Browser;

public static class BrowserDownloadSessionPolicy
{
    public static bool UseEmbeddedSession(string? selectedCookies)
        => string.Equals(selectedCookies, "embedded", StringComparison.Ordinal);

    public static bool ShouldResetAfterUse(string? selectedCookies)
        => !string.IsNullOrEmpty(selectedCookies);
}
