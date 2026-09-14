using System.Text;

namespace VideoGrabber.Infrastructure.Downloads;

public static class DownloadFileName
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeBaseName(string? value, int maxLength = 140)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "Видео" : value.Trim();
        var query = source.IndexOf('?');
        if (query >= 0) source = source[..query];
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new StringBuilder(source.Length);
        var previousSpace = false;
        foreach (var ch in source)
        {
            var replacement = invalid.Contains(ch) || char.IsControl(ch) ? ' ' : ch;
            if (char.IsWhiteSpace(replacement))
            {
                if (!previousSpace) result.Append(' ');
                previousSpace = true;
            }
            else
            {
                result.Append(replacement);
                previousSpace = false;
            }
        }
        var cleaned = result.ToString().Trim().TrimEnd('.');
        while (cleaned.Contains("  ", StringComparison.Ordinal)) cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        if (cleaned.Length == 0) cleaned = "Видео";
        if (Reserved.Contains(cleaned)) cleaned = "_" + cleaned;
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength].Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "Видео" : cleaned;
    }


    public static string DurationTag(double durationSeconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, Math.Round(durationSeconds)));
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours:00}h{span.Minutes:00}m{span.Seconds:00}s";
        return $"{span.Minutes:00}m{span.Seconds:00}s";
    }
    public static string BuildSuggested(string? title, int ordinal, string quality)
    {
        var safeTitle = SanitizeBaseName(title);
        var safeQuality = SanitizeBaseName(string.IsNullOrWhiteSpace(quality) ? "best" : quality, 24);
        return SanitizeBaseName($"{Math.Max(1, ordinal):00} - {safeTitle} - {safeQuality}");
    }
}
