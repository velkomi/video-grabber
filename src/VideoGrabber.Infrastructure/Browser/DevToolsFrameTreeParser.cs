using System.Text.Json;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsFrameInfo(string FrameId, Uri? Source, int TreeOrder);

public sealed record BrowserPlayerSlot(int Ordinal, string? Title);

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
    public static MediaCandidate Bind(MediaCandidate candidate, IReadOnlyList<DevToolsFrameInfo> frames, BrowserPageMetadata metadata)
    {
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

    private static bool IsPlayerFrame(DevToolsFrameInfo frame)
        => frame.Source is not null
            && MediaCandidate.TryCreate(frame.Source.AbsoluteUri, null, frame.Source, out var candidate)
            && candidate?.Kind == "GetCourse";

    private static bool MatchesReferer(Uri? frame, Uri referer)
        => frame is not null
            && string.Equals(frame.IdnHost, referer.IdnHost, StringComparison.OrdinalIgnoreCase)
            && string.Equals(frame.AbsolutePath, referer.AbsolutePath, StringComparison.OrdinalIgnoreCase);
}
