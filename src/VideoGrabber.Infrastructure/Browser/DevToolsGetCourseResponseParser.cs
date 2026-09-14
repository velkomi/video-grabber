using System.Text;
using System.Text.Json;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record DevToolsGetCourseResponse(string RequestId, Uri PlayerUri, Uri Referer, string? FrameId = null)
{
    public string SafeDisplay => "GetCourse player " + PlayerUri.IdnHost;
}

public static class DevToolsGetCourseResponseParser
{
    private const int MaxBodyChars = 8_000_000;

    public static bool TryParsePlayerResponse(string json, Uri page, out DevToolsGetCourseResponse? response)
    {
        response = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var requestId = Text(root, "requestId");
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 256) return false;
            if (!root.TryGetProperty("response", out var payload)) return false;
            if (!payload.TryGetProperty("status", out var status) || status.GetInt32() is not (200 or 206)) return false;
            var url = Text(payload, "url");
            var mime = Text(payload, "mimeType");
            if (!MediaCandidate.TryCreate(url ?? string.Empty, mime, page, out var candidate)
                || candidate is null || candidate.Kind != "GetCourse") return false;
            response = new(requestId, candidate.Source, page, Text(root, "frameId"));
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    public static bool TryDecodeBody(string json, out string? body)
    {
        body = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("body", out var value) || value.ValueKind != JsonValueKind.String) return false;
            var raw = value.GetString();
            if (raw is null || raw.Length > MaxBodyChars * 2) return false;
            var encoded = root.TryGetProperty("base64Encoded", out var flag)
                && flag.ValueKind is JsonValueKind.True or JsonValueKind.False && flag.GetBoolean();
            if (!encoded)
            {
                if (raw.Length > MaxBodyChars) return false;
                body = raw;
                return true;
            }
            var bytes = Convert.FromBase64String(raw);
            if (bytes.Length > MaxBodyChars * 4) return false;
            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.Length > MaxBodyChars) return false;
            body = decoded;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    public static bool TryParseLoadingFinished(string json, out string? requestId)
        => TryParseRequestId(json, out requestId);

    public static bool TryParseLoadingFailed(string json, out string? requestId)
        => TryParseRequestId(json, out requestId);

    private static bool TryParseRequestId(string json, out string? requestId)
    {
        requestId = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = Text(doc.RootElement, "requestId");
            return !string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 256;
        }
        catch (JsonException) { requestId = null; return false; }
    }
    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
