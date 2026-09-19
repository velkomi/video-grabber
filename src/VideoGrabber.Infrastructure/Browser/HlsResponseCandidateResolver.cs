namespace VideoGrabber.Infrastructure.Browser;

public static class HlsResponseCandidateResolver
{
    private static readonly string[] ProviderFamilies =
    [
        "getcourse.ru", "gcvh.ru", "gcfiles.net", "vhcdn.com", "trbcdn.net",
        "kinescope.io", "kinescopecdn.net", "servicecdn.ru"
    ];

    public static bool ShouldInspectBody(Uri source, string? mime, long contentLength)
    {
        if (contentLength > 4_000_000) return false;
        var host = source.IdnHost.ToLowerInvariant();
        if (!ProviderFamilies.Any(f => host == f || host.EndsWith("." + f, StringComparison.Ordinal))) return false;
        var path = source.AbsolutePath.ToLowerInvariant();
        var type = (mime ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        return path.EndsWith(".m3u8", StringComparison.Ordinal)
            || path.Contains("playlist", StringComparison.Ordinal)
            || path.Contains("manifest", StringComparison.Ordinal)
            || type is "application/vnd.apple.mpegurl" or "application/x-mpegurl" or "application/octet-stream" or "text/plain";
    }

    public static bool TryResolve(
        string body,
        Uri source,
        Uri referer,
        out MediaCandidate? candidate,
        out bool blocked)
    {
        candidate = null;
        blocked = false;
        if (!HlsManifestParser.TryParse(body, source, out var info) || info is null) return false;
        if (!HlsDownloadPolicy.IsAllowed(info))
        {
            blocked = true;
            return true;
        }
        candidate = new MediaCandidate(source, referer, "HLS", info.SafeSummary, HlsManifest: info);
        return true;
    }
}