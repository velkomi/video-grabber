using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseDownloadStateStoreTests
{
    [Fact]
    public async Task State_round_trips_plan_and_completed_lessons()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-course-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = new GetCourseCoursePlan(
                "Курс",
                new Uri("https://school.example/teach/control/stream/view/id/1"),
                [
                    new GetCourseLessonPlan(
                        new Uri("https://school.example/teach/control/lesson/view/id/10"),
                        "Урок 1",
                        ["01 - Модуль"],
                        1),
                    new GetCourseLessonPlan(
                        new Uri("https://school.example/teach/control/lesson/view/id/11"),
                        "Урок 2",
                        ["01 - Модуль"],
                        2)
                ]);
            var key = GetCourseCourseStructure.CanonicalKey(
                plan.Lessons[0].Uri);
            var state = CourseDownloadStateStore.Create(
                plan,
                [key],
                nextLessonIndex: 1);

            await CourseDownloadStateStore.SaveAtomicAsync(
                root,
                state,
                CancellationToken.None);
            var loaded = await CourseDownloadStateStore.LoadAsync(
                root,
                CancellationToken.None);
            var restored = CourseDownloadStateStore.ToPlan(loaded);

            Assert.Equal(plan.CourseTitle, restored.CourseTitle);
            Assert.Equal(plan.Root, restored.Root);
            Assert.Equal(2, restored.Lessons.Length);
            Assert.Equal([key], loaded.CompletedLessonKeys);
            Assert.Equal(1, loaded.NextLessonIndex);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Stable_media_resume_key_is_deterministic_and_video_specific()
    {
        var lesson = new Uri(
            "https://school.example/teach/control/lesson/view/id/101");
        var first = CourseDownloadStateStore.StableMediaResumeKey(
            lesson,
            1,
            "720p");
        var again = CourseDownloadStateStore.StableMediaResumeKey(
            lesson,
            1,
            "720p");
        var secondVideo = CourseDownloadStateStore.StableMediaResumeKey(
            lesson,
            2,
            "720p");

        Assert.Equal(first, again);
        Assert.NotEqual(first, secondVideo);
        Assert.Equal(24, first.Length);
        Assert.All(first, ch =>
            Assert.True(char.IsAsciiLetterOrDigit(ch)));
    }

    [Fact]
    public async Task Load_rejects_cross_origin_lesson()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-course-state-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var json = """
            {
              "schemaVersion":1,
              "courseTitle":"Курс",
              "rootUrl":"https://school.example/teach/control/stream/view/id/1",
              "createdUtc":"2026-09-19T00:00:00+00:00",
              "updatedUtc":"2026-09-19T00:00:00+00:00",
              "nextLessonIndex":0,
              "completedLessonKeys":[],
              "lessons":[{
                "url":"https://evil.example/teach/control/lesson/view/id/10",
                "title":"Урок",
                "moduleFolders":["01 - Модуль"],
                "lessonOrdinal":1
              }]
            }
            """;
            await File.WriteAllTextAsync(
                CourseDownloadStateStore.StatePath(root),
                json);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => CourseDownloadStateStore.LoadAsync(
                    root,
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
