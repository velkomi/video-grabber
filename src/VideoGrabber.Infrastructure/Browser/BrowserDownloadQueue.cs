using VideoGrabber.Core.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserDownloadQueueItem(
    MediaCandidate Candidate,
    int Ordinal,
    string Quality);

public sealed class BrowserDownloadQueue
{
    private readonly List<BrowserDownloadQueueItem> _items = [];
    public IReadOnlyList<BrowserDownloadQueueItem> Items => _items;

    public void AddOrUpdate(MediaCandidate candidate, int ordinal, string quality)
    {
        var index = _items.FindIndex(item =>
            string.Equals(item.Candidate.Source.AbsoluteUri, candidate.Source.AbsoluteUri, StringComparison.Ordinal));
        var value = new BrowserDownloadQueueItem(candidate, Math.Max(1, ordinal), NormalizeQuality(quality));
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
