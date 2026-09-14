using System.Globalization;
using System.Text.RegularExpressions;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record HlsVariant(Uri Uri, int? Width, int? Height, long? Bandwidth, double? FrameRate, string? AudioGroupId);
public sealed record HlsAudioRendition(Uri Uri, string? Language, string? Name, string? GroupId, bool IsDefault);

public sealed record HlsManifestInfo(
    bool IsMaster,
    IReadOnlyList<HlsVariant> Variants,
    IReadOnlyList<HlsAudioRendition> AudioRenditions,
    bool IsEncrypted,
    string? EncryptionMethod,
    bool UsesDrmLikeEncryption)
{
    public string SafeSummary
    {
        get
        {
            var parts = new List<string> { IsMaster ? "master" : "media" };
            var heights = Variants.Where(v => v.Height is > 0).Select(v => v.Height!.Value).Distinct().Order().ToArray();
            if (heights.Length > 0) parts.Add("video: " + string.Join(", ", heights.Select(h => h + "p")));
            var languages = AudioRenditions.Select(a => a.Language ?? a.Name).Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (languages.Length > 0) parts.Add("audio: " + string.Join(", ", languages));
            if (IsEncrypted && !string.IsNullOrWhiteSpace(EncryptionMethod)) parts.Add("encryption: " + EncryptionMethod);
            return string.Join("; ", parts);
        }
    }
}

public static partial class HlsManifestParser
{
    [GeneratedRegex("([A-Z0-9-]+)=(\\\"[^\\\"]*\\\"|[^,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();

    public static bool TryParse(string text, Uri source, out HlsManifestInfo? info)
    {
        info = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4_000_000) return false;
        var normalized = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!normalized.StartsWith("#EXTM3U", StringComparison.Ordinal)) return false;
        var lines = normalized.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var variants = new List<HlsVariant>();
        var audio = new List<HlsAudioRendition>();
        var encryptionMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMediaSegments = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase)) hasMediaSegments = true;
            if (line.StartsWith("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("#EXT-X-SESSION-KEY:", StringComparison.OrdinalIgnoreCase))
            {
                var attrs = ParseAttributes(line[(line.IndexOf(':') + 1)..]);
                if (!attrs.TryGetValue("METHOD", out var keyMethod) || string.IsNullOrWhiteSpace(keyMethod))
                    encryptionMethods.Add("UNKNOWN");
                else if (!string.Equals(keyMethod, "NONE", StringComparison.OrdinalIgnoreCase))
                    encryptionMethods.Add(keyMethod.ToUpperInvariant());
                continue;
            }
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.OrdinalIgnoreCase))
            {
                var attrs = ParseAttributes(line["#EXT-X-MEDIA:".Length..]);
                if (!attrs.TryGetValue("TYPE", out var type) || !type.Equals("AUDIO", StringComparison.OrdinalIgnoreCase)
                    || !attrs.TryGetValue("URI", out var audioUri) || !TryResolve(source, audioUri, out var resolvedAudio)) continue;
                audio.Add(new HlsAudioRendition(resolvedAudio!, Value(attrs, "LANGUAGE"), Value(attrs, "NAME"), Value(attrs, "GROUP-ID"),
                    string.Equals(Value(attrs, "DEFAULT"), "YES", StringComparison.OrdinalIgnoreCase)));
                continue;
            }
            if (!line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase)) continue;
            var streamAttrs = ParseAttributes(line["#EXT-X-STREAM-INF:".Length..]);
            string? next = null;
            for (var j = i + 1; j < lines.Length; j++)
            {
                var candidate = lines[j].Trim();
                if (candidate.Length == 0) continue;
                if (candidate.StartsWith('#')) break;
                next = candidate;
                i = j;
                break;
            }
            if (next is null || !TryResolve(source, next, out var variantUri)) continue;
            ParseResolution(Value(streamAttrs, "RESOLUTION"), out var width, out var height);
            variants.Add(new HlsVariant(variantUri!, width, height, ParseLong(Value(streamAttrs, "BANDWIDTH")),
                ParseDouble(Value(streamAttrs, "FRAME-RATE")), Value(streamAttrs, "AUDIO")));
        }

        var isMaster = variants.Count > 0 || audio.Count > 0 || lines.Any(l => l.TrimStart().StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase));
        if (!isMaster && !hasMediaSegments && !lines.Any(l => l.TrimStart().StartsWith("#EXT-X-TARGETDURATION:", StringComparison.OrdinalIgnoreCase))) return false;
        var method = encryptionMethods.Count == 0 ? null : string.Join("+", encryptionMethods.Order(StringComparer.OrdinalIgnoreCase));
        var drmLike = encryptionMethods.Any(m => m.Contains("SAMPLE-AES", StringComparison.OrdinalIgnoreCase));
        info = new HlsManifestInfo(isMaster, variants, audio, encryptionMethods.Count > 0, method, drmLike);
        return true;
    }

    private static Dictionary<string, string> ParseAttributes(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex().Matches(value))
        {
            var raw = match.Groups[2].Value.Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"') raw = raw[1..^1];
            result[match.Groups[1].Value] = raw;
        }
        return result;
    }

    private static string? Value(IReadOnlyDictionary<string, string> attrs, string key)
        => attrs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool TryResolve(Uri source, string value, out Uri? uri)
    {
        uri = null;
        try
        {
            var resolved = Uri.TryCreate(value, UriKind.Absolute, out var absolute) ? absolute : new Uri(source, value);
            if (!UrlPolicy.TryValidate(resolved.AbsoluteUri, out var safe, out _) || safe is null || !string.IsNullOrEmpty(safe.UserInfo)) return false;
            uri = safe;
            return true;
        }
        catch (UriFormatException) { return false; }
    }

    private static void ParseResolution(string? value, out int? width, out int? height)
    {
        width = null; height = null;
        if (string.IsNullOrWhiteSpace(value)) return;
        var x = value.IndexOf('x');
        if (x <= 0 || x >= value.Length - 1) return;
        if (int.TryParse(value[..x], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) && w > 0) width = w;
        if (int.TryParse(value[(x + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h > 0) height = h;
    }

    private static long? ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
    private static double? ParseDouble(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
}
