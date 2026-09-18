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

        foreach (var link in page.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (link.Kind == "lesson")
            {
                var lessonKey =
                    GetCourseCourseStructure.CanonicalKey(link.Uri);
                if (!visitedLessons.Add(lessonKey)) continue;
                lessonOrdinal++;
                lessons.Add(new GetCourseLessonPlan(
                    link.Uri,
                    link.Title,
                    moduleFolders.ToArray(),
                    lessonOrdinal));
                continue;
            }

            if (link.Kind != "training") continue;
            var childKey =
                GetCourseCourseStructure.CanonicalKey(link.Uri);
            if (visitedTrainings.Contains(childKey)) continue;

            moduleOrdinal++;
            var childFolder =
                GetCourseCourseStructure.OrderedFolder(
                    moduleOrdinal,
                    link.Title);
            var childFolders = moduleFolders
                .Append(childFolder)
                .ToArray();

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
}
