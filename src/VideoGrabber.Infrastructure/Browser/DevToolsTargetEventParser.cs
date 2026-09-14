using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsTargetSession(string SessionId, string Type, string Host, string Path)
{
    public string SafeDisplay => string.IsNullOrWhiteSpace(Host) ? Type : Type + " " + Host;
}

public static class DevToolsTargetEventParser
{
    public static bool TryParseSessionId(string json, out string? sessionId)
    {
        sessionId = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            sessionId = Text(doc.RootElement, "sessionId");
            return !string.IsNullOrWhiteSpace(sessionId) && sessionId.Length <= 256;
        }
        catch (JsonException) { return false; }
    }

    public static bool TryParseAttached(string json, out DevToolsTargetSession? target)
    {
        target = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sessionId = Text(root, "sessionId");
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 256) return false;
            if (!root.TryGetProperty("targetInfo", out var info) || info.ValueKind != JsonValueKind.Object) return false;
            var type = Text(info, "type");
            var url = Text(info, "url");
            if (string.IsNullOrWhiteSpace(type)) return false;
            if (type.Equals("iframe", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(url) || url.Equals("about:blank", StringComparison.OrdinalIgnoreCase)))
            {
                target = new(sessionId, type, string.Empty, string.Empty);
                return true;
            }
            if (!UrlPolicy.TryValidate(url, out var uri, out _) || uri is null) return false;
            target = new(sessionId, type, uri.IdnHost, uri.AbsolutePath);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
