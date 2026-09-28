using System.Text.RegularExpressions;

namespace VideoGrabber.Infrastructure.Transcription;

public static partial class TranscriptTextValidator
{
    public const int MaxUtf8Bytes = 16 * 1024 * 1024;
    public const int MaxConsecutiveDuplicateLines = 10;

    public static bool TryValidate(string? text, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return Fail("empty-text", out error);

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(NormalizeLine)
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
            return Fail("empty-text", out error);

        var blankAudioCount = lines.Count(IsBlankAudioLine);
        if (blankAudioCount >= 10
            && blankAudioCount >= Math.Max(10, (int)Math.Ceiling(lines.Length * 0.35)))
            return Fail("blank-audio-dominant", out error);

        var longestRun = 1;
        var currentRun = 1;
        string? repeated = null;
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.Equals(lines[i], lines[i - 1], StringComparison.Ordinal)
                && lines[i].Length >= 4
                && !IsHarmlessShortInterjection(lines[i]))
            {
                currentRun++;
                if (currentRun > longestRun)
                {
                    longestRun = currentRun;
                    repeated = lines[i];
                }
            }
            else
            {
                currentRun = 1;
            }
        }

        if (longestRun > MaxConsecutiveDuplicateLines)
            return Fail("repetition-loop", out error);

        var dominant = lines
            .Where(line => line.Length >= 4 && !IsBlankAudioLine(line))
            .GroupBy(line => line, StringComparer.Ordinal)
            .Select(group => new { Line = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .FirstOrDefault();

        if (dominant is not null
            && dominant.Count >= 50
            && dominant.Count >= Math.Max(50, (int)Math.Ceiling(lines.Length * 0.20)))
            return Fail("repetition-dominant", out error);

        return true;
    }

    private static string NormalizeLine(string value)
    {
        var normalized = MultiWhitespaceRegex()
            .Replace(value.Trim(), " ")
            .Trim();
        return normalized.ToLowerInvariant();
    }

    private static bool IsBlankAudioLine(string value)
        => value.Equals("[blank_audio]", StringComparison.OrdinalIgnoreCase)
           || value.Equals("[blank audio]", StringComparison.OrdinalIgnoreCase)
           || value.Equals("[silence]", StringComparison.OrdinalIgnoreCase)
           || value.Equals("(silence)", StringComparison.OrdinalIgnoreCase);

    private static bool IsHarmlessShortInterjection(string value)
    {
        var trimmed = value.Trim(' ', '.', ',', '!', '?', '-', '—', '–', ':', ';');
        return trimmed.Length <= 3;
    }

    private static bool Fail(string kind, out string? error)
    {
        error = kind;
        return false;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MultiWhitespaceRegex();
}
