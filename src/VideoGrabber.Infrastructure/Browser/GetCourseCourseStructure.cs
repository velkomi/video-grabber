using System.Text.Json;
using System.Text.RegularExpressions;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record GetCourseCourseLink(
    string Kind,
    string Title,
    Uri Uri,
    int Order);

public sealed record GetCourseCoursePage(
    string Title,
    IReadOnlyList<GetCourseCourseLink> Links)
{
    public IReadOnlyList<GetCourseCourseLink> Lessons =>
        Links.Where(x => x.Kind == "lesson").ToArray();

    public IReadOnlyList<GetCourseCourseLink> Trainings =>
        Links.Where(x => x.Kind == "training").ToArray();
}

public static partial class GetCourseCourseStructure
{
    private sealed record LinkPayload(
        string? kind,
        string? title,
        string? url,
        int order);

    private sealed record PagePayload(
        string? pageTitle,
        LinkPayload[]? links);

    public static bool TryParsePage(
        string json,
        Uri currentPage,
        out GetCourseCoursePage? page)
    {
        page = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        PagePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PagePayload>(json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null) return false;
        var title = CleanTitle(payload.pageTitle, "Курс");
        var links = new List<GetCourseCourseLink>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in (payload.links ?? [])
                     .OrderBy(x => x.order)
                     .Take(2000))
        {
            if (!TryValidateLink(item, currentPage, out var link)
                || link is null)
                continue;

            var key = CanonicalKey(link.Uri);
            if (!seen.Add(key)) continue;
            links.Add(link);
        }

        page = new GetCourseCoursePage(title, links.AsReadOnly());
        return true;
    }

    public static bool IsLessonUri(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.Contains(
            "/teach/control/lesson/view",
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTrainingUri(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.Contains(
            "/teach/control/stream/view",
            StringComparison.OrdinalIgnoreCase);
    }

    public static string CanonicalKey(Uri uri)
    {
        var kind = IsLessonUri(uri)
            ? "lesson"
            : IsTrainingUri(uri) ? "training" : "page";

        var match = IdPathRegex().Match(uri.AbsolutePath);
        if (match.Success)
            return kind + ":" + match.Groups["id"].Value;

        var queryId = QueryId(uri.Query);
        if (queryId is not null)
            return kind + ":" + queryId;

        return kind + ":" + uri.GetLeftPart(UriPartial.Path)
            .TrimEnd('/')
            .ToLowerInvariant();
    }

    public static string CourseFolder(string? title) =>
        CleanFolder(title, "GetCourse курс", 80);

    public static string OrderedFolder(
        int ordinal,
        string? title,
        string fallback = "Модуль")
    {
        var clean = CleanFolder(title, fallback, 56);
        return $"{Math.Max(1, ordinal):00} - {clean}";
    }

    public static string VideoBaseName(
        int lessonOrdinal,
        string? lessonTitle,
        int videoOrdinal,
        int videoCount,
        string? quality)
    {
        var parts = new List<string>
        {
            $"{Math.Max(1, lessonOrdinal):00} - {CleanFolder(lessonTitle, "Урок", 72)}"
        };
        if (videoCount > 1)
            parts.Add($"Видео {Math.Max(1, videoOrdinal):00}");
        if (!string.IsNullOrWhiteSpace(quality))
            parts.Add(DownloadFileName.SanitizeBaseName(quality, 20));
        return DownloadFileName.SanitizeBaseName(string.Join(" - ", parts), 120);
    }
    private static bool TryValidateLink(
        LinkPayload payload,
        Uri currentPage,
        out GetCourseCourseLink? link)
    {
        link = null;
        if (!Uri.TryCreate(payload.url, UriKind.Absolute, out var uri)
            || !UrlPolicy.TryValidate(
                uri.AbsoluteUri, out var safe, out _)
            || safe is null
            || !string.IsNullOrEmpty(safe.UserInfo))
            return false;

        if (!string.Equals(
                safe.IdnHost,
                currentPage.IdnHost,
                StringComparison.OrdinalIgnoreCase))
            return false;

        var actualKind = IsLessonUri(safe)
            ? "lesson"
            : IsTrainingUri(safe) ? "training" : null;
        if (actualKind is null) return false;

        if (!string.IsNullOrWhiteSpace(payload.kind)
            && !string.Equals(
                payload.kind,
                actualKind,
                StringComparison.OrdinalIgnoreCase))
            return false;

        link = new GetCourseCourseLink(
            actualKind,
            CleanTitle(
                payload.title,
                actualKind == "lesson" ? "Урок" : "Модуль"),
            safe,
            Math.Max(0, payload.order));
        return true;
    }

    private static string? QueryId(string query)
    {
        foreach (var part in query.TrimStart('?').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2
                && string.Equals(
                    Uri.UnescapeDataString(pair[0]),
                    "id",
                    StringComparison.OrdinalIgnoreCase)
                && pair[1].All(char.IsAsciiDigit))
                return pair[1];
        }
        return null;
    }

    private static string CleanTitle(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = Regex.Replace(value, @"\s+", " ").Trim();
        if (text.Length > 180) text = text[..180];
        return text.Length == 0 ? fallback : text;
    }

    private static string CleanFolder(
        string? value,
        string fallback,
        int maxLength)
    {
        var clean = DownloadFileName.SanitizeBaseName(
            CleanTitle(value, fallback),
            maxLength);
        return string.IsNullOrWhiteSpace(clean)
            ? fallback
            : clean;
    }

    [GeneratedRegex(@"/id/(?<id>\d+)(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdPathRegex();
}
