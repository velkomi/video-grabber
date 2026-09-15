namespace VideoGrabber.Infrastructure.Browser;

public sealed record MediaCandidateSelection(
    IReadOnlyList<MediaCandidate> Candidates, Uri? SelectedSource, string SelectedQuality);

public static class MediaCandidateSelectionReducer
{
    public static MediaCandidateSelection Reduce(
        IReadOnlyList<MediaCandidate> orderedCandidates,
        Uri? previousSelectedSource,
        IReadOnlyDictionary<string, string> qualityBySource)
    {
        var candidates = orderedCandidates.ToArray();
        var selected = candidates.FirstOrDefault(candidate => string.Equals(
            candidate.Source.AbsoluteUri, previousSelectedSource?.AbsoluteUri, StringComparison.Ordinal))?.Source
            ?? candidates.FirstOrDefault()?.Source;
        var quality = selected is not null && qualityBySource.TryGetValue(selected.AbsoluteUri, out var saved)
            ? saved : "best";
        return new(candidates, selected, quality);
    }
}
