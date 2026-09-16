using System.Text;

namespace VideoGrabber.Infrastructure.Transcription;

public static class SrtValidator
{
    public const int MaxUtf8Bytes = 16 * 1024 * 1024;
    public const int MaxCues = 100_000;
    public const int MaxCueCharacters = 65_536;

    public static bool TryValidate(string text, double? mediaDurationSeconds, out string? error)
    {
        error = null;
        if (mediaDurationSeconds is double duration
            && (!double.IsFinite(duration) || duration <= 0))
            return Fail("invalid-duration", out error);
        if (text is null || text.Length == 0)
            return Fail("empty", out error);
        if (Encoding.UTF8.GetByteCount(text) > MaxUtf8Bytes)
            return Fail("too-large", out error);

        var normalized = text.TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var block = new List<string>(8);
        var cueCount = 0;
        long previousStart = -1, previousEnd = -1;
        foreach (var line in lines.Append(string.Empty))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                block.Add(line);
                continue;
            }
            if (block.Count == 0) continue;
            cueCount++;
            if (cueCount > MaxCues) return Fail("too-many-cues", out error);
            if (!TryValidateCue(block, mediaDurationSeconds, ref previousStart, ref previousEnd, out error))
                return false;
            block.Clear();
        }

        if (cueCount == 0) return Fail("empty", out error);
        return true;
    }

    private static bool TryValidateCue(
        IReadOnlyList<string> block, double? mediaDurationSeconds,
        ref long previousStart, ref long previousEnd, out string? error)
    {
        error = null;
        if (block.Count < 3 || !int.TryParse(block[0], out var index) || index <= 0)
            return Fail("cue-format", out error);
        var arrow = block[1].IndexOf(" --> ", StringComparison.Ordinal);
        if (arrow <= 0 || block[1].IndexOf(" --> ", arrow + 5, StringComparison.Ordinal) >= 0)
            return Fail("cue-format", out error);
        var startText = block[1][..arrow].Trim();
        var endText = block[1][(arrow + 5)..].Trim();
        if (!TryParseTimestamp(startText, out var start) || !TryParseTimestamp(endText, out var end))
            return Fail("timestamp-format", out error);
        if (end <= start) return Fail("cue-range", out error);
        if (previousStart >= 0 && start <= previousStart)
            return Fail("cue-order", out error);
        if (previousEnd >= 0 && start < previousEnd)
            return Fail("cue-overlap", out error);
        if (mediaDurationSeconds is double duration
            && end > (duration + 1d) * 1000d)
            return Fail("duration-bound", out error);

        var textLength = 0;
        var nonEmpty = false;
        for (var i = 2; i < block.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(block[i])) nonEmpty = true;
            textLength += block[i].Length;
            if (i > 2) textLength++;
            if (textLength > MaxCueCharacters) return Fail("cue-too-large", out error);
        }
        if (!nonEmpty) return Fail("cue-text-empty", out error);
        previousStart = start;
        previousEnd = end;
        return true;
    }

    private static bool TryParseTimestamp(string value, out long milliseconds)
    {
        milliseconds = 0;
        if (value.Length != 12 || value[2] != ':' || value[5] != ':' || value[8] != ',')
            return false;
        if (!TryDigits(value.AsSpan(0, 2), out var hours)
            || !TryDigits(value.AsSpan(3, 2), out var minutes)
            || !TryDigits(value.AsSpan(6, 2), out var seconds)
            || !TryDigits(value.AsSpan(9, 3), out var millis)
            || minutes >= 60 || seconds >= 60)
            return false;
        milliseconds = checked((((long)hours * 60 + minutes) * 60 + seconds) * 1000 + millis);
        return true;
    }

    private static bool TryDigits(ReadOnlySpan<char> value, out int number)
    {
        number = 0;
        foreach (var c in value)
        {
            if (c is < '0' or > '9') return false;
            number = number * 10 + c - '0';
        }
        return true;
    }

    private static bool Fail(string kind, out string? error)
    {
        error = kind;
        return false;
    }
}
