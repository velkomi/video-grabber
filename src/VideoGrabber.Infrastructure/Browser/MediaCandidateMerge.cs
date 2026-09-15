namespace VideoGrabber.Infrastructure.Browser;

public static class MediaCandidateMerge
{
    // Only a completed manifest probe may clear a conflict; discovery replays may not.
    public static MediaCandidate ConfirmDuration(MediaCandidate candidate, double duration)
    {
        if (KnownDuration(duration) is null) throw new ArgumentOutOfRangeException(nameof(duration));
        if (candidate.HlsManifest is null) throw new ArgumentException("An HLS manifest is required.", nameof(candidate));
        return candidate with
        {
            HlsManifest = candidate.HlsManifest with { DurationSeconds = duration },
            DurationNeedsRevalidation = false
        };
    }

    // The caller scopes candidates to the current page generation.
    public static MediaCandidate Merge(MediaCandidate previous, MediaCandidate incoming)
    {
        if (!string.Equals(previous.Source.AbsoluteUri, incoming.Source.AbsoluteUri, StringComparison.Ordinal))
            throw new ArgumentException("Candidates must have the same source within one page generation.", nameof(incoming));
        var frameConflict = previous.FrameEvidenceConflicted || incoming.FrameEvidenceConflicted
            || previous.FrameId is not null && incoming.FrameId is not null
                && !string.Equals(previous.FrameId, incoming.FrameId, StringComparison.Ordinal);
        var previousDuration = KnownDuration(previous.HlsManifest?.DurationSeconds);
        var incomingDuration = KnownDuration(incoming.HlsManifest?.DurationSeconds);
        var durationConflict = previous.DurationNeedsRevalidation || incoming.DurationNeedsRevalidation
            || previousDuration is not null && incomingDuration is not null && previousDuration != incomingDuration;
        var manifest = MergeManifest(previous.HlsManifest, incoming.HlsManifest);
        return incoming with
        {
            Details = incoming.Details ?? previous.Details,
            FrameId = frameConflict ? null : incoming.FrameId ?? previous.FrameId,
            PageOrdinal = frameConflict ? null : incoming.PageOrdinal ?? previous.PageOrdinal,
            PageSectionTitle = frameConflict ? null : incoming.PageSectionTitle ?? previous.PageSectionTitle,
            FrameEvidenceConflicted = frameConflict,
            HlsVideoSource = incoming.HlsVideoSource ?? previous.HlsVideoSource,
            HlsAudioSource = incoming.HlsAudioSource ?? previous.HlsAudioSource,
            HlsManifest = manifest is null ? null : manifest with
                { DurationSeconds = durationConflict ? null : incomingDuration ?? previousDuration },
            DurationNeedsRevalidation = durationConflict
        };
    }

    private static double? KnownDuration(double? value)
        => value is > 0 && double.IsFinite(value.Value) ? value : null;

    private static HlsManifestInfo? MergeManifest(HlsManifestInfo? previous, HlsManifestInfo? incoming)
    {
        if (previous is null) return incoming;
        if (incoming is null) return previous;
        // A supplied manifest is a track snapshot; only a missing manifest is sparse discovery.
        return incoming with
        {
            DurationSeconds = incoming.DurationSeconds ?? previous.DurationSeconds
        };
    }
}
