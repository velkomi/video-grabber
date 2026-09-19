using VideoGrabber.Infrastructure.Browser;
using Xunit;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class GetCourseCoursePlannerTests
{
    [Fact]
    public async Task Planner_walks_nested_modules_preserves_order_and_deduplicates_lessons()
    {
        var root = new Uri("https://school.example/teach/control/stream/view/id/1");
        var moduleOne = new Uri("https://school.example/teach/control/stream/view/id/10");
        var subModule = new Uri("https://school.example/teach/control/stream/view/id/11");
        var moduleTwo = new Uri("https://school.example/teach/control/stream/view/id/20");
        var lessonRoot = new Uri("https://school.example/teach/control/lesson/view/id/100");
        var lessonOne = new Uri("https://school.example/teach/control/lesson/view/id/101");
        var lessonTwo = new Uri("https://school.example/teach/control/lesson/view/id/102");
        var lessonThree = new Uri("https://school.example/teach/control/lesson/view/id/103");

        var rootPage = Page("Мой курс",
            Training("Модуль 1", moduleOne, 1),
            Lesson("Вводный урок", lessonRoot, 2),
            Training("Модуль 2", moduleTwo, 3));

        var pages = new Dictionary<string, GetCourseCoursePage>
        {
            [GetCourseCourseStructure.CanonicalKey(moduleOne)] = Page("Модуль 1",
                Lesson("Урок 1", lessonOne, 1),
                Training("Подмодуль", subModule, 2)),
            [GetCourseCourseStructure.CanonicalKey(subModule)] = Page("Подмодуль",
                Lesson("Урок 2", lessonTwo, 1),
                Lesson("Дубликат урока 1", lessonOne, 2)),
            [GetCourseCourseStructure.CanonicalKey(moduleTwo)] = Page("Модуль 2",
                Lesson("Урок 3", lessonThree, 1))
        };

        var visited = new List<Uri>();
        var plan = await GetCourseCoursePlanner.BuildAsync(
            root,
            rootPage,
            (uri, _) =>
            {
                visited.Add(uri);
                pages.TryGetValue(GetCourseCourseStructure.CanonicalKey(uri), out var page);
                return Task.FromResult(page);
            },
            null,
            CancellationToken.None);

        Assert.Equal("Мой курс", plan.CourseTitle);
        Assert.Equal(root, plan.Root);
        Assert.Equal(4, plan.Lessons.Length);

        Assert.Equal(lessonOne, plan.Lessons[0].Uri);
        Assert.Equal(["01 - Модуль 1"], plan.Lessons[0].ModuleFolders);
        Assert.Equal(1, plan.Lessons[0].LessonOrdinal);

        Assert.Equal(lessonTwo, plan.Lessons[1].Uri);
        Assert.Equal(["01 - Модуль 1", "02 - Подмодуль"], plan.Lessons[1].ModuleFolders);
        Assert.Equal(1, plan.Lessons[1].LessonOrdinal);

        Assert.Equal(lessonRoot, plan.Lessons[2].Uri);
        Assert.Equal(["00 - Общая информация"], plan.Lessons[2].ModuleFolders);
        Assert.Equal(1, plan.Lessons[2].LessonOrdinal);

        Assert.Equal(lessonThree, plan.Lessons[3].Uri);
        Assert.Equal(["02 - Модуль 2"], plan.Lessons[3].ModuleFolders);
        Assert.Equal(1, plan.Lessons[3].LessonOrdinal);

        Assert.Equal([moduleOne, subModule, moduleTwo], visited);
    }

    [Fact]
    public async Task Planner_separates_general_information_and_numbers_modules_explicitly()
    {
        var root = new Uri("https://school.example/teach/control/stream/view/id/1");
        var org = new Uri("https://school.example/teach/control/stream/view/id/2");
        var moduleOne = new Uri("https://school.example/teach/control/stream/view/id/10");
        var moduleTwo = new Uri("https://school.example/teach/control/stream/view/id/20");
        var intro = new Uri("https://school.example/teach/control/lesson/view/id/100");
        var orgLesson = new Uri("https://school.example/teach/control/lesson/view/id/101");
        var method = new Uri("https://school.example/teach/control/lesson/view/id/110");
        var homework = new Uri("https://school.example/teach/control/stream/view/id/11");
        var homeworkLesson = new Uri("https://school.example/teach/control/lesson/view/id/111");
        var second = new Uri("https://school.example/teach/control/lesson/view/id/120");

        var rootPage = Page(
            "Курс",
            Lesson("Приветствие", intro, 1),
            Training("Запись организационного эфира", org, 2),
            Training("МОДУЛЬ №1", moduleOne, 3),
            Training("МОДУЛЬ №2", moduleTwo, 4));

        var pages = new Dictionary<string, GetCourseCoursePage>
        {
            [GetCourseCourseStructure.CanonicalKey(org)] = Page(
                "Орг",
                Lesson("Эфир", orgLesson, 1)),
            [GetCourseCourseStructure.CanonicalKey(moduleOne)] = Page(
                "МОДУЛЬ №1",
                Lesson("Методические материалы", method, 1),
                Training("Домашние задания", homework, 2)),
            [GetCourseCourseStructure.CanonicalKey(homework)] = Page(
                "Домашние задания",
                Lesson("Задание", homeworkLesson, 1)),
            [GetCourseCourseStructure.CanonicalKey(moduleTwo)] = Page(
                "МОДУЛЬ №2",
                Lesson("День 1", second, 1))
        };

        var plan = await GetCourseCoursePlanner.BuildAsync(
            root,
            rootPage,
            (uri, _) => Task.FromResult<GetCourseCoursePage?>(
                pages[GetCourseCourseStructure.CanonicalKey(uri)]),
            null,
            CancellationToken.None);

        Assert.Equal(
            ["00 - Общая информация"],
            plan.Lessons[0].ModuleFolders);
        Assert.Equal(1, plan.Lessons[0].LessonOrdinal);

        Assert.Equal(
            ["00 - Общая информация", "02 - Запись организационного эфира"],
            plan.Lessons[1].ModuleFolders);

        Assert.Equal(
            ["01 - МОДУЛЬ №1"],
            plan.Lessons[2].ModuleFolders);
        Assert.Equal(1, plan.Lessons[2].LessonOrdinal);

        Assert.Equal(
            ["01 - МОДУЛЬ №1", "02 - Домашние задания"],
            plan.Lessons[3].ModuleFolders);

        Assert.Equal(
            ["02 - МОДУЛЬ №2"],
            plan.Lessons[4].ModuleFolders);
    }

    [Fact]
    public async Task Planner_stops_training_cycles_and_does_not_refetch_parent()
    {
        var root = new Uri("https://school.example/teach/control/stream/view/id/1");
        var child = new Uri("https://school.example/teach/control/stream/view/id/2");
        var lesson = new Uri("https://school.example/teach/control/lesson/view/id/9");

        var rootPage = Page("Курс", Training("Модуль", child, 1));
        var fetches = 0;

        var plan = await GetCourseCoursePlanner.BuildAsync(
            root,
            rootPage,
            (uri, _) =>
            {
                fetches++;
                return Task.FromResult<GetCourseCoursePage?>(Page(
                    "Модуль",
                    Training("Назад", root, 1),
                    Lesson("Урок", lesson, 2)));
            },
            null,
            CancellationToken.None);

        Assert.Equal(1, fetches);
        Assert.Single(plan.Lessons);
        Assert.Equal(lesson, plan.Lessons[0].Uri);
    }

    [Fact]
    public async Task Planner_honors_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GetCourseCoursePlanner.BuildAsync(
                new Uri("https://school.example/teach/control/stream/view/id/1"),
                Page("Курс"),
                (_, _) => Task.FromResult<GetCourseCoursePage?>(null),
                null,
                cts.Token));
    }

    private static GetCourseCoursePage Page(
        string title,
        params GetCourseCourseLink[] links)
        => new(title, links);

    private static GetCourseCourseLink Lesson(
        string title,
        Uri uri,
        int order)
        => new("lesson", title, uri, order);

    private static GetCourseCourseLink Training(
        string title,
        Uri uri,
        int order)
        => new("training", title, uri, order);
}
