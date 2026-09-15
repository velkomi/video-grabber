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
        var result = candidates.Select(candidate => Bind(candidate, frames, metadata)).ToArray();
        // Resolve all evidence before checking collisions so discovery order cannot choose a winner.
        var conflictingSources = result.GroupBy(candidate => candidate.Source.AbsoluteUri, StringComparer.Ordinal)
            .Where(group => group.Select(candidate => candidate.PageOrdinal).Distinct().Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var conflictingParts = result.Where(candidate => candidate.PageOrdinal is not null)
            .GroupBy(candidate => candidate.PageOrdinal!.Value)
            .Where(group => group.Select(candidate => candidate.Source.AbsoluteUri).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key).ToHashSet();
        return result.Select(candidate => conflictingSources.Contains(candidate.Source.AbsoluteUri)
                || candidate.PageOrdinal is int part && conflictingParts.Contains(part)
            ? Unknown(candidate) : candidate).ToArray();
    }

    public static MediaCandidate Bind(MediaCandidate candidate, IReadOnlyList<DevToolsFrameInfo> frames, BrowserPageMetadata metadata)
    {
        candidate = Unknown(candidate);
        var evidence = new List<Uri> { candidate.Source, candidate.Referer };
        if (!string.IsNullOrWhiteSpace(candidate.FrameId))
        {
            var matchingFrames = frames.Where(frame => string.Equals(frame.FrameId, candidate.FrameId, StringComparison.Ordinal)).ToArray();
            if (matchingFrames.Length > 1) return candidate;
            if (matchingFrames.Length == 1 && matchingFrames[0].Source is Uri frameSource)
                evidence.Add(frameSource);
        }

        var matches = metadata.PlayerSlots.Where(slot => evidence.Any(uri => MatchesUrl(slot.Source, uri))).ToArray();
        if (matches.Length != 1) return candidate;
        var match = matches[0];
        if (match.Ordinal <= 0 || metadata.PlayerSlots.Count(slot => slot.Ordinal == match.Ordinal) != 1) return candidate;
        return candidate with { PageOrdinal = match.Ordinal, PageSectionTitle = match.Title };
    }

    private static MediaCandidate Unknown(MediaCandidate candidate)
        => candidate with { PageOrdinal = null, PageSectionTitle = null };

    private static bool MatchesUrl(Uri? source, Uri target)
        => source is { IsAbsoluteUri: true } && target.IsAbsoluteUri
            && source.Scheme is "http" or "https" && target.Scheme is "http" or "https"
            && string.IsNullOrEmpty(source.UserInfo) && string.IsNullOrEmpty(target.UserInfo)
            && string.Equals(source.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase)
            && source.Port == target.Port
            && string.Equals(source.PathAndQuery, target.PathAndQuery, StringComparison.Ordinal)
            && string.Equals(source.Fragment, target.Fragment, StringComparison.Ordinal);
}
