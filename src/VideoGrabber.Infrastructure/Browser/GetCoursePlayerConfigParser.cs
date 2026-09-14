using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public static class GetCoursePlayerConfigParser
{
    public static bool TryExtractMasterPlaylist(string html, Uri playerUri, out Uri? playlist)
    {
        playlist = null;
        if (string.IsNullOrWhiteSpace(html) || html.Length > 8_000_000) return false;
        var marker = html.IndexOf("window.configs", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return false;
        var start = html.IndexOf('{', marker);
        if (start < 0) return false;
        var json = ExtractBalancedObject(html, start);
        if (json is null) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("masterPlaylistUrl", out var value)
                || value.ValueKind != JsonValueKind.String) return false;
            var raw = value.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var candidate = Uri.TryCreate(raw, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(playerUri, raw);
            if (!UrlPolicy.TryValidate(candidate.AbsoluteUri, out var safe, out _)
                || safe is null || !string.IsNullOrEmpty(safe.UserInfo)) return false;
            playlist = safe;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or UriFormatException)
        {
            return false;
        }
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
