using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserPageMetadata(
    string? PageTitle, IReadOnlyList<string> SectionTitles, IReadOnlyList<BrowserPlayerSlot>? Slots = null)
{
    public IReadOnlyList<BrowserPlayerSlot> PlayerSlots { get; } = Slots ?? [];
    public static BrowserPageMetadata Empty { get; } = new(null, []);
}

public static class MediaCandidatePresentation
{
    public static string DisplayName(MediaCandidate candidate, int ordinal, BrowserPageMetadata metadata)
    {
        var effectiveOrdinal = candidate.PageOrdinal ?? ordinal;
        var title = HumanTitle(candidate, metadata, effectiveOrdinal);
        if (candidate.HlsManifest is { IsMaster: true } manifest)
        {
            var qualities = manifest.Variants
                .Where(v => v.Height is > 0)
                .Select(v => v.Height!.Value)
                .Distinct()
                .Order()
                .Select(height => height + "p")
                .ToArray();
            var qualityText = qualities.Length == 0 ? "HLS" : string.Join(" / ", qualities);
            return $"Видео {Math.Max(1, effectiveOrdinal):00} — {title} — {qualityText}";
        }
        return $"Видео {Math.Max(1, effectiveOrdinal):00} — {title} — {candidate.Kind}";
    }

    public static string SuggestedBaseName(MediaCandidate candidate, int ordinal, string quality, BrowserPageMetadata metadata)
    {
        var effectiveOrdinal = candidate.PageOrdinal ?? ordinal;
        return DownloadFileName.BuildSuggested(HumanTitle(candidate, metadata, effectiveOrdinal), effectiveOrdinal, quality);
    }

    public static int GetInsertIndex(MediaCandidate candidate, IReadOnlyList<MediaCandidate> existing)
    {
        if (candidate.PageOrdinal is null) return existing.Count;
        for (var i = 0; i < existing.Count; i++)
            if (existing[i].PageOrdinal is null || existing[i].PageOrdinal > candidate.PageOrdinal) return i;
        return existing.Count;
    }

    public static bool IsTechnicalChild(MediaCandidate candidate, IEnumerable<MediaCandidate> knownCandidates)
    {
        if (!string.Equals(candidate.Kind, "HLS", StringComparison.OrdinalIgnoreCase)
            || candidate.HlsManifest is not { IsMaster: false }) return false;
        return knownCandidates.Any(master => master.HlsManifest is { IsMaster: true } info
            && (info.Variants.Any(v => v.Uri == candidate.Source)
                || info.AudioRenditions.Any(a => a.Uri == candidate.Source)));
    }

    public static string HumanTitle(BrowserPageMetadata metadata, int ordinal)
        => HumanTitle(null, metadata, ordinal);

    private static string HumanTitle(MediaCandidate? candidate, BrowserPageMetadata metadata, int ordinal)
    {
        var page = Clean(metadata.PageTitle);
        var section = Clean(candidate?.PageSectionTitle);
        if (string.IsNullOrWhiteSpace(section))
            section = ordinal > 0 && ordinal <= metadata.SectionTitles.Count
                ? Clean(metadata.SectionTitles[ordinal - 1])
                : null;
        if (!string.IsNullOrWhiteSpace(page) && !string.IsNullOrWhiteSpace(section)
            && !string.Equals(page, section, StringComparison.OrdinalIgnoreCase))
            return page + " - " + section;
        if (!string.IsNullOrWhiteSpace(section)) return section;
        if (!string.IsNullOrWhiteSpace(page)) return page;
        return $"Видео {Math.Max(1, ordinal):00}";
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var safe = DownloadFileName.SanitizeBaseName(value, 100);
        return string.IsNullOrWhiteSpace(safe) ? null : safe;
    }
}
