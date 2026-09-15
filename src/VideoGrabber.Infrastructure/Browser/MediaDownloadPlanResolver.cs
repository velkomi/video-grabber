namespace VideoGrabber.Infrastructure.Browser;

public sealed record MediaDownloadPlan(Uri Source, Uri? HlsVideoSource, Uri? HlsAudioSource, bool DirectManifest,
    bool IsResolved = true, string? Error = null, bool ResolvedHlsLeaf = false);

public static class MediaDownloadPlanResolver
{
    public static MediaDownloadPlan Resolve(MediaCandidate candidate, string quality, bool audioOnly)
    {
        var isHls = string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase);
        var directManifest = isHls || string.Equals(candidate.Kind, "DASH", StringComparison.OrdinalIgnoreCase);
        if (isHls && (candidate.HlsManifest is null || !HlsDownloadPolicy.IsAllowed(candidate.HlsManifest)))
            return Unresolved(candidate, "Не удалось подтвердить незашифрованный HLS. Обновите список видео.");
        if (candidate.HlsManifest is not { IsMaster: true } info)
            return new(candidate.Source, candidate.HlsVideoSource, candidate.HlsAudioSource, directManifest,
                ResolvedHlsLeaf: isHls);
        var plan = HlsTrackSelector.Select(info, quality);
        if (plan is null || !MatchesQuality(plan.Video, quality)
            || plan.Video.Uri == candidate.Source || plan.Audio?.Uri == candidate.Source)
            return Unresolved(candidate, "Выбранное качество HLS недоступно. Обновите список видео и выберите качество заново.");
        if (audioOnly)
            return new(plan.Audio?.Uri ?? plan.Video.Uri, null, null, true, ResolvedHlsLeaf: true);
        if (plan.Audio is not null)
            return new(candidate.Source, plan.Video.Uri, plan.Audio.Uri, true);
        return new(plan.Video.Uri, null, null, true, ResolvedHlsLeaf: true);
    }

    private static MediaDownloadPlan Unresolved(MediaCandidate candidate, string error)
        => new(candidate.Source, null, null, true, IsResolved: false, Error: error);

    private static bool MatchesQuality(HlsVariant video, string quality)
    {
        if (quality == "best") return true;
        if (quality == "4K") return video.Height == 2160;
        return quality.EndsWith('p') && int.TryParse(quality.AsSpan(0, quality.Length - 1),
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var height)
            && height > 0 && video.Height == height;
    }
}
