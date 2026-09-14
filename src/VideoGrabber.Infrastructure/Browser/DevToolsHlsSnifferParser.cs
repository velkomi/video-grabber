using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsRequestContext(string RequestId, Uri Source, Uri Referer, string? FrameId = null)
{
    public string SafeDisplay => Source.IdnHost + " <- " + Referer.IdnHost;
}

public sealed record DevToolsHlsResponse(string RequestId, Uri Source, string Mime, int Status, string? FrameId = null)
{
    public string SafeDisplay => "HLS " + Source.IdnHost + " " + Mime + " HTTP=" + Status;
}

public static class DevToolsHlsSnifferParser
{
    public static bool TryParseRequest(string json, Uri page, out DevToolsRequestContext? context)
    {
        context = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var requestId = Text(root, "requestId");
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 256 || !root.TryGetProperty("request", out var request)) return false;
            var rawUrl = Text(request, "url");
            if (!UrlPolicy.TryValidate(rawUrl, out var source, out _) || source is null || !string.IsNullOrEmpty(source.UserInfo)) return false;
            var referer = page;
            if (request.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in headers.EnumerateObject())
                {
                    if (!property.Name.Equals("Referer", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.String) continue;
                    if (UrlPolicy.TryValidate(property.Value.GetString(), out var headerRef, out _) && headerRef is not null && string.IsNullOrEmpty(headerRef.UserInfo))
                        referer = headerRef;
                    break;
                }
            }
            if (ReferenceEquals(referer, page))
            {
                var documentUrl = Text(root, "documentURL");
                if (UrlPolicy.TryValidate(documentUrl, out var docRef, out _) && docRef is not null && string.IsNullOrEmpty(docRef.UserInfo)) referer = docRef;
            }
            context = new DevToolsRequestContext(requestId, source, referer, Text(root, "frameId"));
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    public static bool TryParseHlsResponse(string json, out DevToolsHlsResponse? response)
    {
        response = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var requestId = Text(root, "requestId");
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 256 || !root.TryGetProperty("response", out var payload)) return false;
            if (!payload.TryGetProperty("status", out var statusNode) || !statusNode.TryGetInt32(out var status) || status < 200 || status >= 300) return false;
            var rawUrl = Text(payload, "url");
            if (!UrlPolicy.TryValidate(rawUrl, out var source, out _) || source is null || !string.IsNullOrEmpty(source.UserInfo)) return false;
            var mime = (Text(payload, "mimeType") ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
            var hlsByMime = mime is "application/vnd.apple.mpegurl" or "application/x-mpegurl";
            var hlsByPath = source.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
            if (!hlsByMime && !hlsByPath) return false;
            response = new DevToolsHlsResponse(requestId, source, mime, status, Text(root, "frameId"));
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
