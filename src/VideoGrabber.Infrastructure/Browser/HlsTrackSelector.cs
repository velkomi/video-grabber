namespace VideoGrabber.Infrastructure.Browser;

public sealed record HlsTrackPlan(HlsVariant Video, HlsAudioRendition? Audio);

public static class HlsTrackSelector
{
    public static HlsTrackPlan? Select(HlsManifestInfo info, string quality)
    {
        if (info.Variants.Count == 0) return null;
        var maxHeight = quality switch
        {
            "360p" => 360,
            "480p" => 480,
            "720p" => 720,
            "1080p" => 1080,
            "4K" => 2160,
            _ => int.MaxValue
        };
        var candidates = maxHeight == int.MaxValue
            ? info.Variants
            : info.Variants.Where(v => v.Height is not null && v.Height <= maxHeight).ToArray();
        var video = candidates
            .OrderByDescending(v => v.Height ?? 0)
            .ThenByDescending(v => v.Bandwidth ?? 0)
            .FirstOrDefault();
        if (video is null) return null;
        var audio = video.AudioGroupId is null ? null : info.AudioRenditions
            .Where(a => string.Equals(a.GroupId, video.AudioGroupId, StringComparison.Ordinal))
            .OrderByDescending(a => a.IsDefault)
            .FirstOrDefault();
        return new HlsTrackPlan(video, audio);
    }
}
