using System.Text.RegularExpressions;

namespace VideoGrabber.Core.ClientUpdates;

public sealed class ClientReleaseVersion : IComparable<ClientReleaseVersion>
{
    private static readonly Regex Syntax = new(
        @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex LegacyPreview = new(
        @"\A([0-9]+\.[0-9]+\.[0-9]+)-preview\.(0|[1-9][0-9]*)-rc\.(0|[1-9][0-9]*)\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly string[] numbers;
    private readonly string[] prerelease;
    private readonly string normalized;

    private ClientReleaseVersion(string[] numbers, string[] prerelease, string normalized)
    {
        this.numbers = numbers;
        this.prerelease = prerelease;
        this.normalized = normalized;
    }

    public static ClientReleaseVersion Parse(string value)
    {
        if (string.IsNullOrEmpty(value)) throw new FormatException("version");
        var match = Syntax.Match(value);
        if (!match.Success) throw new FormatException("version");
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        if (pre.Any(x => IsNumeric(x) && x.Length > 1 && x[0] == '0')) throw new FormatException("version");
        return new([match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value], pre, value.Split('+')[0]);
    }

    public int CompareTo(ClientReleaseVersion? other)
    {
        if (other is null) return 1;
        for (var i = 0; i < 3; i++)
        {
            var result = CompareNumber(numbers[i], other.numbers[i]);
            if (result != 0) return result;
        }
        if (prerelease.Length == 0 || other.prerelease.Length == 0)
            return (prerelease.Length == 0 ? 1 : 0).CompareTo(other.prerelease.Length == 0 ? 1 : 0);
        for (var i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            var left = prerelease[i]; var right = other.prerelease[i];
            var leftNumeric = IsNumeric(left); var rightNumeric = IsNumeric(right);
            var result = leftNumeric && rightNumeric ? CompareNumber(left, right)
                : leftNumeric != rightNumeric ? (leftNumeric ? -1 : 1)
                : string.CompareOrdinal(left, right);
            if (result != 0) return result;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }

    public override string ToString() => normalized;

    public static int CompareForVideoGrabber(string left, string right)
        => ParseForVideoGrabber(left).CompareTo(ParseForVideoGrabber(right));

    private static ClientReleaseVersion ParseForVideoGrabber(string value)
    {
        var version = Parse(value);
        var match = LegacyPreview.Match(version.normalized);
        // Shipped releases use preview.N-rc.M, where N must advance numerically.
        return match.Success
            ? Parse($"{match.Groups[1].Value}-preview.{match.Groups[2].Value}.rc.{match.Groups[3].Value}")
            : version;
    }

    private static bool IsNumeric(string value) => value.All(char.IsAsciiDigit);
    private static int CompareNumber(string left, string right)
        => left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
}
