using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;

namespace VideoGrabber.Infrastructure.Downloads;

public static class DownloadOutputContract
{
    public static bool IsSatisfied(DownloadRequest request, MediaProbeResult media)
    {
        if (!media.IsValid) return false;
        if (request.AudioOnly)
        {
            if (!media.HasAudio || media.HasVideo || !string.Equals(media.AudioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        else if (!media.HasVideo || (request.ExpectedAudio is { } audio && media.HasAudio != audio)) return false;

        if (request.ExpectedDurationSeconds is not { } expected || !double.IsFinite(expected) || expected <= 0)
            return true;
        return double.IsFinite(media.DurationSeconds) && media.DurationSeconds > 0
            && Math.Abs(media.DurationSeconds - expected) <= Math.Max(1, expected * 0.02);
    }
}
