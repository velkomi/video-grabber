namespace VideoGrabber.Infrastructure.Browser;

public sealed record GetCourseLessonPlan(
    Uri Uri,
    string Title,
    string[] ModuleFolders,
    int LessonOrdinal);

public sealed record GetCourseCoursePlan(
    string CourseTitle,
    Uri Root,
    GetCourseLessonPlan[] Lessons);

public static class GetCourseCoursePlanner
{
    public static async Task<GetCourseCoursePlan> BuildAsync(
        Uri root,
        GetCourseCoursePage rootPage,
        Func<Uri, CancellationToken, Task<GetCourseCoursePage?>> fetchTraining,
        Action<string>? onModule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(rootPage);
        ArgumentNullException.ThrowIfNull(fetchTraining);

        var lessons = new List<GetCourseLessonPlan>();
        var visitedTrainings = new HashSet<string>(StringComparer.Ordinal);
        var visitedLessons = new HashSet<string>(StringComparer.Ordinal);

        await TraverseAsync(
            root,
            [],
            rootPage,
            0,
            visitedTrainings,
            visitedLessons,
            lessons,
            fetchTraining,
            onModule,
            cancellationToken);

        return new GetCourseCoursePlan(
            rootPage.Title,
            root,
            lessons.ToArray());
    }

    private static async Task TraverseAsync(
        Uri trainingUri,
        string[] moduleFolders,
        GetCourseCoursePage page,
        int depth,
        HashSet<string> visitedTrainings,
        HashSet<string> visitedLessons,
        List<GetCourseLessonPlan> lessons,
        Func<Uri, CancellationToken, Task<GetCourseCoursePage?>> fetchTraining,
        Action<string>? onModule,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth > 10)
            throw new InvalidDataException(
                "Слишком глубокая структура подтренингов.");
        if (visitedTrainings.Count >= 500
            || visitedLessons.Count >= 5000)
            throw new InvalidDataException(
                "Структура курса превышает безопасный лимит.");

        var trainingKey = GetCourseCourseStructure.CanonicalKey(trainingUri);
        if (!visitedTrainings.Add(trainingKey)) return;

        var lessonOrdinal = 0;
        var moduleOrdinal = 0;
        var itemOrdinal = 0;
        var generalOrdinal = 0;
        var moduleLayout =
            depth == 0
            && page.Trainings.Any(link =>
                TryGetTopLevelModuleNumber(
                    link.Title,
                    out _));

        foreach (var link in page.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (link.Kind == "lesson")
            {
                var lessonKey =
                    GetCourseCourseStructure.CanonicalKey(link.Uri);
                if (!visitedLessons.Add(lessonKey)) continue;
                lessonOrdinal++;
                itemOrdinal++;
                if (moduleLayout
                    && depth == 0
                    && moduleFolders.Length == 0)
                    generalOrdinal++;

                var lessonFolders =
                    moduleLayout
                    && depth == 0
                    && moduleFolders.Length == 0
                        ? new[] { "00 - Общая информация" }
                        : depth == 0
                            && moduleFolders.Length == 0
                                ? new[] { "00 - Вводные материалы" }
                                : moduleFolders.ToArray();

                var ordinal =
                    moduleLayout
                    && depth == 0
                    && moduleFolders.Length == 0
                        ? generalOrdinal
                        : itemOrdinal;

                lessons.Add(new GetCourseLessonPlan(
                    link.Uri,
                    link.Title,
                    lessonFolders,
                    ordinal));
                continue;
            }

            if (link.Kind != "training") continue;
            var childKey =
                GetCourseCourseStructure.CanonicalKey(link.Uri);
            if (visitedTrainings.Contains(childKey)) continue;

            moduleOrdinal++;
            itemOrdinal++;

            string[] childFolders;
            if (moduleLayout
                && depth == 0
                && moduleFolders.Length == 0)
            {
                if (TryGetTopLevelModuleNumber(
                        link.Title,
                        out var explicitModuleNumber))
                {
                    var childFolder =
                        GetCourseCourseStructure.OrderedFolder(
                            explicitModuleNumber,
                            link.Title);
                    childFolders = [childFolder];
                }
                else
                {
                    generalOrdinal++;
                    var infoFolder =
                        GetCourseCourseStructure.OrderedFolder(
                            generalOrdinal,
                            link.Title);
                    childFolders =
                    [
                        "00 - Общая информация",
                        infoFolder
                    ];
                }
            }
            else
            {
                var childFolder =
                    GetCourseCourseStructure.OrderedFolder(
                        itemOrdinal,
                        link.Title);
                childFolders = moduleFolders
                    .Append(childFolder)
                    .ToArray();
            }

            onModule?.Invoke(link.Title);
            var childPage =
                await fetchTraining(link.Uri, cancellationToken);
            if (childPage is null) continue;

            await TraverseAsync(
                link.Uri,
                childFolders,
                childPage,
                depth + 1,
                visitedTrainings,
                visitedLessons,
                lessons,
                fetchTraining,
                onModule,
                cancellationToken);
        }
    }

    private static bool TryGetTopLevelModuleNumber(
        string title,
        out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            title,
            @"^ *МОДУЛЬ *№? *(?<n>[0-9]+)(?: |$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success
            && int.TryParse(
                match.Groups["n"].Value,
                out number)
            && number is > 0 and < 100;
    }
}
