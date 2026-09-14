using System.Text.Json;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsFrameInfo(string FrameId, Uri? Source, int TreeOrder);

public sealed record BrowserPlayerSlot(int Ordinal, string? Title, Uri? Source = null);

public static class DevToolsFrameTreeParser
{
    public static bool TryParse(string json, out IReadOnlyList<DevToolsFrameInfo>? frames)
    {
        frames = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("frameTree", out var tree)) return false;
            var result = new List<DevToolsFrameInfo>();
            Walk(tree, result);
            frames = result;
            return result.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Walk(JsonElement node, List<DevToolsFrameInfo> result)
    {
        if (node.TryGetProperty("frame", out var frame) && frame.ValueKind == JsonValueKind.Object)
        {
            var id = Text(frame, "id");
            var url = Text(frame, "url");
            if (!string.IsNullOrWhiteSpace(id) && id.Length <= 256)
            {
                Uri? source = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                    && parsed.Scheme is "http" or "https" && string.IsNullOrEmpty(parsed.UserInfo))
                    source = parsed;
                result.Add(new(id, source, result.Count));
            }
        }
        if (!node.TryGetProperty("childFrames", out var children) || children.ValueKind != JsonValueKind.Array) return;
        foreach (var child in children.EnumerateArray()) Walk(child, result);
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}

public static class BrowserFrameBindingResolver
{
    public static IReadOnlyList<MediaCandidate> BindAll(
        IReadOnlyList<MediaCandidate> candidates,
        IReadOnlyList<DevToolsFrameInfo> frames,
        BrowserPageMetadata metadata)
    {
        if (candidates.Count == 0) return [];
        var result = candidates.ToArray();
        var assigned = new bool[result.Length];
        var occupied = new HashSet<int>();
        var slots = metadata.PlayerSlots.OrderBy(slot => slot.Ordinal).ToArray();

        for (var i = 0; i < result.Length; i++)
        {
            var slot = slots.FirstOrDefault(item => MatchesReferer(item.Source, result[i].Referer));
            if (slot is null || !occupied.Add(slot.Ordinal)) continue;
            result[i] = result[i] with { PageOrdinal = slot.Ordinal, PageSectionTitle = slot.Title };
            assigned[i] = true;
        }

        var playerFrames = frames.Where(IsPlayerFrame).OrderBy(frame => frame.TreeOrder).ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            if (assigned[i]) continue;
            var ordinal = FrameOrdinal(result[i], playerFrames);
            if (ordinal <= 0 || occupied.Contains(ordinal)) continue;
            var slot = slots.FirstOrDefault(item => item.Ordinal == ordinal);
            if (slot is null) continue;
            occupied.Add(ordinal);
            result[i] = result[i] with { PageOrdinal = ordinal, PageSectionTitle = slot.Title };
            assigned[i] = true;
        }

        var freeSlots = new Queue<BrowserPlayerSlot>(slots.Where(slot => !occupied.Contains(slot.Ordinal)));
        for (var i = 0; i < result.Length; i++)
        {
            if (assigned[i] || freeSlots.Count == 0) continue;
            var slot = freeSlots.Dequeue();
            occupied.Add(slot.Ordinal);
            result[i] = result[i] with { PageOrdinal = slot.Ordinal, PageSectionTitle = slot.Title };
            assigned[i] = true;
        }
        return result;
    }

    public static MediaCandidate Bind(MediaCandidate candidate, IReadOnlyList<DevToolsFrameInfo> frames, BrowserPageMetadata metadata)
    {
        var domSlot = metadata.PlayerSlots.FirstOrDefault(slot => MatchesReferer(slot.Source, candidate.Referer));
        if (domSlot is not null)
            return candidate with { PageOrdinal = domSlot.Ordinal, PageSectionTitle = domSlot.Title };

        var playerFrames = frames.Where(IsPlayerFrame).OrderBy(frame => frame.TreeOrder).ToArray();
        if (playerFrames.Length == 0) return candidate;
        var frame = !string.IsNullOrWhiteSpace(candidate.FrameId)
            ? playerFrames.FirstOrDefault(item => string.Equals(item.FrameId, candidate.FrameId, StringComparison.Ordinal))
            : null;
        frame ??= playerFrames.FirstOrDefault(item => MatchesReferer(item.Source, candidate.Referer));
        if (frame is null) return candidate;
        var ordinal = Array.IndexOf(playerFrames, frame) + 1;
        var title = metadata.PlayerSlots.FirstOrDefault(slot => slot.Ordinal == ordinal)?.Title;
        return candidate with { PageOrdinal = ordinal, PageSectionTitle = title };
    }

    private static int FrameOrdinal(MediaCandidate candidate, IReadOnlyList<DevToolsFrameInfo> playerFrames)
    {
        var frame = !string.IsNullOrWhiteSpace(candidate.FrameId)
            ? playerFrames.FirstOrDefault(item => string.Equals(item.FrameId, candidate.FrameId, StringComparison.Ordinal))
            : null;
        frame ??= playerFrames.FirstOrDefault(item => MatchesReferer(item.Source, candidate.Referer));
        if (frame is null) return 0;
        for (var i = 0; i < playerFrames.Count; i++)
            if (ReferenceEquals(playerFrames[i], frame) || playerFrames[i] == frame) return i + 1;
        return 0;
    }

    private static bool IsPlayerFrame(DevToolsFrameInfo frame)
        => frame.Source is not null
            && MediaCandidate.TryCreate(frame.Source.AbsoluteUri, null, frame.Source, out var candidate)
            && candidate?.Kind == "GetCourse";

    private static bool MatchesReferer(Uri? frame, Uri referer)
        => frame is not null
            && string.Equals(frame.Scheme, referer.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(frame.IdnHost, referer.IdnHost, StringComparison.OrdinalIgnoreCase)
            && frame.Port == referer.Port
            && string.Equals(frame.PathAndQuery, referer.PathAndQuery, StringComparison.Ordinal);
}
