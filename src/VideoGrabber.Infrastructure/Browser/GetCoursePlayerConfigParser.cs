using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public static class GetCoursePlayerConfigParser
{
    private const int MaxBodyChars = 8_000_000;
    private static readonly Regex MasterPropertyRegex = new(
        "(?is)(?:[\\\"']?masterPlaylistUrl[\\\"']?)\\s*[:=]\\s*[\\\"'](?<url>[^\\\"'<>]+?\\.m3u8[^\\\"'<>]*)[\\\"']",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PlaylistUrlRegex = new(
        "(?i)(?<url>(?:https?:)?(?:\\\\?/){2}[^\\s\\\"'<>]+?\\.m3u8(?:\\?[^\\s\\\"'<>]*)?|/?[A-Za-z0-9._~!$&()*+,;=:@%\\\\/-]+\\.m3u8(?:\\?[^\\s\\\"'<>]*)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UnicodeEscapeRegex = new(
        "\\\\u(?<hex>[0-9a-fA-F]{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryExtractMasterPlaylist(string body, Uri playerUri, out Uri? playlist)
    {
        playlist = null;
        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyChars) return false;

        var candidates = new List<string>();
        CollectJsonCandidates(body, candidates);
        CollectWindowConfigs(body, candidates);
        foreach (Match match in MasterPropertyRegex.Matches(body))
            candidates.Add(match.Groups["url"].Value);
        foreach (Match match in PlaylistUrlRegex.Matches(body))
            candidates.Add(match.Groups["url"].Value);

        foreach (var raw in candidates)
            if (TryBuildPlaylist(raw, playerUri, out playlist)) return true;
        return false;
    }

    private static void CollectJsonCandidates(string body, List<string> candidates)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            CollectJsonElement(document.RootElement, candidates, 0);
        }
        catch (JsonException) { }
    }

    private static void CollectJsonElement(JsonElement element, List<string> candidates, int depth)
    {
        if (depth > 16 || candidates.Count >= 256) return;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value)
                            && (property.Name.Contains("playlist", StringComparison.OrdinalIgnoreCase)
                                || value.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)))
                            candidates.Add(value);
                    }
                    CollectJsonElement(property.Value, candidates, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectJsonElement(item, candidates, depth + 1);
                break;
            case JsonValueKind.String:
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text)
                    && text.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
                    candidates.Add(text);
                break;
        }
    }

    private static void CollectWindowConfigs(string body, List<string> candidates)
    {
        var search = 0;
        while (search < body.Length && candidates.Count < 256)
        {
            var marker = body.IndexOf("window.configs", search, StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return;
            var start = body.IndexOf('{', marker);
            if (start < 0) return;
            var json = ExtractBalancedObject(body, start);
            if (json is not null) CollectJsonCandidates(json, candidates);
            search = start + 1;
        }
    }

    private static bool TryBuildPlaylist(string raw, Uri playerUri, out Uri? playlist)
    {
        playlist = null;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 8192) return false;
        var decoded = WebUtility.HtmlDecode(raw.Trim());
        decoded = decoded.Replace("\\/", "/", StringComparison.Ordinal);
        decoded = UnicodeEscapeRegex.Replace(decoded, match =>
            ((char)Convert.ToInt32(match.Groups["hex"].Value, 16)).ToString());
        // Values reach this method only from playlist-named JSON properties or an explicit .m3u8 match.
        // GetCourse master endpoints commonly use /api/playlist/master/... without a .m3u8 suffix.

        Uri candidate;
        try
        {
            candidate = Uri.TryCreate(decoded, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(playerUri, decoded);
        }
        catch (UriFormatException) { return false; }
        if (!UrlPolicy.TryValidate(candidate.AbsoluteUri, out var safe, out _)
            || safe is null || !string.IsNullOrEmpty(safe.UserInfo)) return false;
        playlist = safe;
        return true;
    }

    private static string? ExtractBalancedObject(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return text[start..(i + 1)];
            if (depth < 0) return null;
        }
        return null;
    }
}