using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record MediaCandidate(
    Uri Source, Uri Referer, string Kind, string? Details = null,
    Uri? HlsVideoSource = null, Uri? HlsAudioSource = null, HlsManifestInfo? HlsManifest = null,
    string? FrameId = null, int? PageOrdinal = null, string? PageSectionTitle = null)
{
    public bool FrameEvidenceConflicted { get; init; }
    public bool DurationNeedsRevalidation { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Details)
        ? $"{Kind} — {Source.IdnHost}"
        : $"{Kind} {Details} — {Source.IdnHost}";
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
            : IsGetCoursePlayerHost(uri.IdnHost) && path.Contains("/sign-player/", StringComparison.Ordinal) ? "GetCourse"
            : null;
        if (kind is null) return false;
        candidate = new(uri, referer, kind);
        return true;
    }
    private static bool IsGetCoursePlayerHost(string host)
    {
        host = host.ToLowerInvariant();
        return HasNumberedPrefix(host, "api", ".gcvh.ru")
            || HasNumberedPrefix(host, "vhapi", ".gcfiles.net")
            || HasNumberedPrefix(host, "player", ".getcourse.ru")
            || HasNumberedPrefix(host, "cf-api-", ".vhcdn.com");
    }
    private static bool HasNumberedPrefix(string host, string prefix, string suffix)
    {
        if (!host.StartsWith(prefix, StringComparison.Ordinal) || !host.EndsWith(suffix, StringComparison.Ordinal)) return false;
        var middle = host[prefix.Length..^suffix.Length];
        return middle.Length is > 0 and <= 4 && middle.All(char.IsAsciiDigit);
    }
}
