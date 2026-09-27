using System.Text.RegularExpressions;

namespace VideoGrabber.Infrastructure.Browser;

public static partial class CourseVideoBlockEvidence
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static int CountDeclaredVideoBlocks(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 0;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var anonymous = 0;
        foreach (Match match in VideoWrapperRegex().Matches(html))
        {
            var tag = match.Value;
            var id = BlockIdRegex().Match(tag);
            if (id.Success && !string.IsNullOrWhiteSpace(id.Groups[1].Value))
                ids.Add(id.Groups[1].Value);
            else
                anonymous++;
        }

        return ids.Count + anonymous;
    }

    [GeneratedRegex(
        "<div\\b[^>]*\\bclass\\s*=\\s*[\"'][^\"']*\\bo-lt-lesson-video\\b[^\"']*[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex VideoWrapperRegex();

    [GeneratedRegex(
        "\\bdata-block-id\\s*=\\s*[\"']([^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BlockIdRegex();
}
