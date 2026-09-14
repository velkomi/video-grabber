namespace VideoGrabber.Infrastructure.Browser;

public static class HlsDownloadPreflight
{
    public static bool TryVerify(string body, Uri source, out HlsManifestInfo? info)
    {
        info = null;
        if (!HlsManifestParser.TryParse(body, source, out var parsed) || parsed is null)
            return false;
        if (!HlsDownloadPolicy.IsAllowed(parsed))
            return false;
        info = parsed;
        return true;
    }
}
