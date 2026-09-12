using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record MediaCandidate(Uri Source, Uri Referer, string Kind)
{
    public string DisplayName => $"{Kind} — {Source.IdnHost}";
    public static bool TryCreate(string url, string? mime, Uri referer, out MediaCandidate? candidate)
    {
        candidate = null;
        if (!UrlPolicy.TryValidate(url, out var uri, out _) || uri is null || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        var type = (mime ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();
        var kind = path.EndsWith(".m3u8") || type is "application/vnd.apple.mpegurl" or "application/x-mpegurl" ? "HLS"
            : path.EndsWith(".mpd") || type == "application/dash+xml" ? "DASH"
            : path.EndsWith(".mp4") || type == "video/mp4" ? "MP4"
            : path.EndsWith(".webm") || type == "video/webm" ? "WebM"
            : uri.Host is "player02.getcourse.ru" or "cf-api-2.vhcdn.com" && path.Contains("/sign-player/") ? "GetCourse"
            : null;
        if (kind is null) return false;
        candidate = new(uri, referer, kind);
        return true;
    }
}
