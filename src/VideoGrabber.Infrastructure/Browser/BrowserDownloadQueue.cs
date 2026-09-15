using VideoGrabber.Core.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserQueueContext(Uri Page, long SessionEpoch, BrowserPageMetadata Metadata)
{
    public BrowserPageMetadata Metadata { get; } = new(Metadata.PageTitle,
        Array.AsReadOnly(Metadata.SectionTitles.ToArray()), Array.AsReadOnly(Metadata.PlayerSlots.ToArray()));
}

public sealed record BrowserDownloadQueueItem(
    MediaCandidate Candidate,
    int Ordinal,
    string Quality,
    BrowserQueueContext? Context = null);

public sealed class BrowserDownloadQueue
{
    private readonly List<BrowserDownloadQueueItem> _items = [];
    public IReadOnlyList<BrowserDownloadQueueItem> Items => _items.AsReadOnly();

    public void AddOrUpdate(MediaCandidate candidate, int ordinal, string quality, BrowserQueueContext? context = null)
    {
        var index = _items.FindIndex(item =>
            string.Equals(item.Candidate.Source.AbsoluteUri, candidate.Source.AbsoluteUri, StringComparison.Ordinal));
        var snapshot = candidate with
        {
            HlsManifest = candidate.HlsManifest is { } manifest ? manifest with
            {
                Variants = Array.AsReadOnly(manifest.Variants.ToArray()),
                AudioRenditions = Array.AsReadOnly(manifest.AudioRenditions.ToArray())
            } : null
        };
        var value = new BrowserDownloadQueueItem(snapshot, Math.Max(1, ordinal), NormalizeQuality(quality), context);
        if (index >= 0) _items[index] = value;
        else _items.Add(value);
    }

    public bool Move(int index, int delta)
    {
        if (index < 0 || index >= _items.Count || delta == 0) return false;
        var target = index + delta;
        if (target < 0 || target >= _items.Count) return false;
        (_items[index], _items[target]) = (_items[target], _items[index]);
        return true;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        _items.RemoveAt(index);
        return true;
    }

    public bool RemoveBySource(Uri source)
    {
        var index = _items.FindIndex(item =>
            string.Equals(item.Candidate.Source.AbsoluteUri, source.AbsoluteUri, StringComparison.Ordinal));
        return RemoveAt(index);
    }

    public void Clear() => _items.Clear();

    private static string NormalizeQuality(string? quality)
        => DownloadQuality.TryParse(quality, out _) ? quality! : throw new ArgumentException("Invalid download quality.", nameof(quality));
}
