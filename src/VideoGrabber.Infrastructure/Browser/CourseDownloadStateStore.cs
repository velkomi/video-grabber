using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record CourseDownloadLessonState(
    string Url,
    string Title,
    string[] ModuleFolders,
    int LessonOrdinal);

public sealed record CourseDownloadState(
    int SchemaVersion,
    string CourseTitle,
    string RootUrl,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    int NextLessonIndex,
    string[] CompletedLessonKeys,
    CourseDownloadLessonState[] Lessons)
{
    public string Quality { get; init; } = "best";
}

public static class CourseDownloadStateStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "VideoGrabber.course.json";
    private const int MaxStateBytes = 4_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static CourseDownloadState Create(
        GetCourseCoursePlan plan,
        IEnumerable<string>? completedLessonKeys = null,
        int nextLessonIndex = 0,
        DateTimeOffset? createdUtc = null,
        string? quality = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var completed = (completedLessonKeys ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new CourseDownloadState(
            CurrentSchemaVersion,
            plan.CourseTitle,
            plan.Root.AbsoluteUri,
            createdUtc ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Math.Clamp(nextLessonIndex, 0, plan.Lessons.Length),
            completed,
            plan.Lessons.Select(lesson => new CourseDownloadLessonState(
                lesson.Uri.AbsoluteUri,
                lesson.Title,
                lesson.ModuleFolders.ToArray(),
                lesson.LessonOrdinal)).ToArray())
        {
            Quality = string.IsNullOrWhiteSpace(quality) ? "best" : quality.Trim()
        };
    }

    public static string StatePath(string courseRoot)
        => Path.Combine(
            Path.GetFullPath(courseRoot),
            FileName);

    public static string StableMediaResumeKey(
        Uri lessonUri,
        int videoOrdinal,
        string quality)
    {
        ArgumentNullException.ThrowIfNull(lessonUri);
        var identity =
            GetCourseCourseStructure.CanonicalKey(lessonUri)
            + "|video:" + Math.Max(1, videoOrdinal)
            + "|quality:" + (quality ?? "best").Trim().ToLowerInvariant();
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    public static async Task SaveAtomicAsync(
        string courseRoot,
        CourseDownloadState state,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(state);
        var root = Path.GetFullPath(courseRoot);
        Directory.CreateDirectory(root);
        var path = StatePath(root);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var json = JsonSerializer.Serialize(state with
        {
            UpdatedUtc = DateTimeOffset.UtcNow
        }, JsonOptions);

        if (Encoding.UTF8.GetByteCount(json) > MaxStateBytes)
            throw new InvalidDataException(
                "Файл состояния курса превышает безопасный размер.");

        try
        {
            await File.WriteAllTextAsync(
                temp,
                json,
                new UTF8Encoding(false),
                token);
            token.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch { }
        }
    }

    public static async Task<CourseDownloadState> LoadAsync(
        string courseRoot,
        CancellationToken token)
    {
        var path = StatePath(courseRoot);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "В выбранной папке нет VideoGrabber.course.json.",
                path);

        var info = new FileInfo(path);
        if (info.Length is <= 0 or > MaxStateBytes)
            throw new InvalidDataException(
                "Некорректный размер файла состояния курса.");

        var json = await File.ReadAllTextAsync(path, token);
        CourseDownloadState? state;
        try
        {
            state = JsonSerializer.Deserialize<CourseDownloadState>(
                json,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Файл состояния курса повреждён.",
                ex);
        }

        return Validate(state);
    }

    public static GetCourseCoursePlan ToPlan(
        CourseDownloadState state)
    {
        state = Validate(state);
        var root = new Uri(state.RootUrl, UriKind.Absolute);
        var lessons = state.Lessons.Select(lesson =>
            new GetCourseLessonPlan(
                new Uri(lesson.Url, UriKind.Absolute),
                lesson.Title,
                lesson.ModuleFolders.ToArray(),
                lesson.LessonOrdinal)).ToArray();

        return new GetCourseCoursePlan(
            state.CourseTitle,
            root,
            lessons);
    }

    private static CourseDownloadState Validate(
        CourseDownloadState? state)
    {
        if (state is null
            || state.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                "Неподдерживаемая версия файла состояния курса.");

        if (!UrlPolicy.TryValidate(
                state.RootUrl,
                out var root,
                out _)
            || root is null)
            throw new InvalidDataException(
                "Некорректный адрес курса в файле состояния.");

        if (string.IsNullOrWhiteSpace(state.CourseTitle)
            || state.CourseTitle.Length > 240
            || state.Lessons is not { Length: > 0 and <= 5000 })
            throw new InvalidDataException(
                "Некорректный заголовок или количество уроков в файле состояния.");

        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lesson in state.Lessons)
        {
            if (lesson is null
                || lesson.ModuleFolders is null
                || lesson.ModuleFolders.Length > 10
                || string.IsNullOrWhiteSpace(lesson.Title)
                || lesson.Title.Length > 240
                || lesson.LessonOrdinal <= 0)
                throw new InvalidDataException(
                    "Некорректная запись урока в файле состояния.");

            if (!UrlPolicy.TryValidate(
                    lesson.Url,
                    out var uri,
                    out _)
                || uri is null
                || !string.Equals(
                    uri.IdnHost,
                    root.IdnHost,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Некорректный адрес урока в файле состояния.");

            known.Add(
                GetCourseCourseStructure.CanonicalKey(uri));
        }

        var completed = (state.CompletedLessonKeys ?? [])
            .Distinct(StringComparer.Ordinal)
            .Where(known.Contains)
            .ToArray();

        return state with
        {
            NextLessonIndex = Math.Clamp(
                state.NextLessonIndex,
                0,
                state.Lessons.Length),
            CompletedLessonKeys = completed
        };
    }
}
