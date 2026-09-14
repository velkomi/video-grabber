namespace VideoGrabber.Infrastructure.Browser;

public sealed record MediaDownloadPlan(Uri Source, Uri? HlsVideoSource, Uri? HlsAudioSource, bool DirectManifest);

public static class MediaDownloadPlanResolver
{
    public static MediaDownloadPlan Resolve(MediaCandidate candidate, string quality, bool audioOnly)
    {
        var directManifest = candidate.Kind is "HLS" or "DASH";
        if (candidate.HlsManifest is not { IsMaster: true } info)
            return new(candidate.Source, candidate.HlsVideoSource, candidate.HlsAudioSource, directManifest);
        var plan = HlsTrackSelector.Select(info, quality);
        if (plan is null) return new(candidate.Source, null, null, directManifest);
        if (audioOnly)
            return new(plan.Audio?.Uri ?? plan.Video.Uri, null, null, true);
        if (plan.Audio is not null)
            return new(candidate.Source, plan.Video.Uri, plan.Audio.Uri, true);
        return new(plan.Video.Uri, null, null, true);
    }
}
