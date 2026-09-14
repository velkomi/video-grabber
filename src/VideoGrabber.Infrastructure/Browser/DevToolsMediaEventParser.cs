using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsNetworkSummary(string ResourceType, string Host, string Path, string Mime, int Status, bool IsMediaLike)
{
    public string SafeDisplay => $"{ResourceType} {Host} {Mime} HTTP={Status}";
}

public static class DevToolsMediaEventParser
{
    public static bool TryParseResponse(string json, Uri page, out MediaCandidate? candidate)
    {
        candidate = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("response", out var response)) return false;
            if (!response.TryGetProperty("status", out var status) || status.GetInt32() is not (200 or 206)) return false;
            var type = Text(root, "type");
            var url = Text(response, "url");
            var mime = Text(response, "mimeType");
            return TryCreate(url, mime, type, page, out candidate);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    public static bool TryParseRequest(string json, Uri page, out MediaCandidate? candidate)
    {
        candidate = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("request", out var request)) return false;
            var type = Text(root, "type");
            var url = Text(request, "url");
            var referer = page;
            if (request.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in headers.EnumerateObject())
                    if (property.Name.Equals("Referer", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String
                        && UrlPolicy.TryValidate(property.Value.GetString(), out var parsed, out _)
                        && parsed is not null && string.IsNullOrEmpty(parsed.UserInfo)) referer = parsed;
            }
            return TryCreate(url, "", type, referer, out candidate);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    public static bool TrySummarizeResponse(string json, out DevToolsNetworkSummary? summary)
    {
        summary = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("response", out var response)) return false;
            var url = Text(response, "url");
            if (!UrlPolicy.TryValidate(url, out var uri, out _) || uri is null) return false;
            var type = Text(root, "type") ?? "Unknown";
            var mime = Text(response, "mimeType") ?? "";
            var status = response.TryGetProperty("status", out var s) && s.TryGetInt32(out var n) ? n : 0;
            var path = uri.AbsolutePath;
            var mediaLike = type.Equals("Media", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || mime.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)
                || mime.Contains("dash", StringComparison.OrdinalIgnoreCase)
                || mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
            summary = new(type, uri.IdnHost, path, mime, status, mediaLike);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    private static bool TryCreate(string? url, string? mime, string? resourceType, Uri referer, out MediaCandidate? candidate)
    {
        candidate = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 16384) return false;
        if (MediaCandidate.TryCreate(url, mime, referer, out candidate) && candidate is not null) return true;
        if (!string.Equals(resourceType, "Media", StringComparison.OrdinalIgnoreCase)) return false;
        if (!UrlPolicy.TryValidate(url, out var uri, out _) || uri is null || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        candidate = new MediaCandidate(uri, referer, "Media");
        return true;
    }

    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
