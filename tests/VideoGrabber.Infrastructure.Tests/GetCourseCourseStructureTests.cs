using VideoGrabber.Infrastructure.Browser;
using Xunit;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class GetCourseCourseStructureTests
{
    [Fact]
    public void Parse_keeps_same_origin_lessons_and_modules_in_dom_order()
    {
        const string json = """
        {
          "pageTitle":" Большой   курс ",
          "links":[
            {
              "kind":"training",
              "title":"Модуль 1: Старт",
              "url":"https://school.example/teach/control/stream/view/id/20",
              "order":1
            },
            {
              "kind":"lesson",
              "title":"Урок 1",
              "url":"https://school.example/teach/control/lesson/view/id/101",
              "order":2
            },
            {
              "kind":"lesson",
              "title":"Дубликат",
              "url":"https://school.example/teach/control/lesson/view/id/101?editMode=0",
              "order":3
            },
            {
              "kind":"lesson",
              "title":"Чужой",
              "url":"https://evil.example/teach/control/lesson/view/id/999",
              "order":4
            }
          ]
        }
        """;

        var ok = GetCourseCourseStructure.TryParsePage(
            json,
            new Uri("https://school.example/teach/control/stream/view/id/10"),
            out var page);

        Assert.True(ok);
        Assert.NotNull(page);
        Assert.Equal("Большой курс", page!.Title);
        Assert.Equal(2, page.Links.Count);
        Assert.Equal("training", page.Links[0].Kind);
        Assert.Equal("lesson", page.Links[1].Kind);
    }

    [Theory]
    [InlineData(
        "https://school.example/teach/control/lesson/view/id/123",
        "lesson:123")]
    [InlineData(
        "https://school.example/pl/teach/control/lesson/view?id=456&editMode=0",
        "lesson:456")]
    [InlineData(
        "https://school.example/teach/control/stream/view/id/789",
        "training:789")]
    public void Canonical_key_ignores_non_identity_query(
        string url,
        string expected)
    {
        Assert.Equal(
            expected,
            GetCourseCourseStructure.CanonicalKey(new Uri(url)));
    }

    [Fact]
    public void Folder_names_are_ordered_and_windows_safe()
    {
        Assert.Equal(
            "03 - Модуль Тема",
            GetCourseCourseStructure.OrderedFolder(
                3,
                "Модуль: Тема?"));
        Assert.Equal(
            "Курс 2026",
            GetCourseCourseStructure.CourseFolder(
                "Курс: 2026"));
        Assert.Equal(
            "Курс 2026 - Полный архив",
            GetCourseCourseStructure.CourseArchiveFolder(
                "Курс: 2026"));
    }

    [Fact]
    public void Mismatched_kind_is_rejected()
    {
        const string json = """
        {
          "pageTitle":"Курс",
          "links":[{
            "kind":"lesson",
            "title":"Не урок",
            "url":"https://school.example/teach/control/stream/view/id/20",
            "order":1
          }]
        }
        """;

        Assert.True(GetCourseCourseStructure.TryParsePage(
            json,
            new Uri("https://school.example/teach/control/stream/view/id/10"),
            out var page));
        Assert.Empty(page!.Links);
    }
    [Fact]
    public void Course_video_name_preserves_lesson_order_topic_and_video_order()
    {
        Assert.Equal(
            "07 - Тема урока - Видео 02 - 720p",
            GetCourseCourseStructure.VideoBaseName(
                7,
                "Тема: урока?",
                2,
                3,
                "720p"));
    }
    [Fact]
    public void Parse_accepts_WebView_JSON_encoded_string_result()
    {
        var inner = "{\"pageTitle\":\"Курс\",\"links\":[{\"kind\":\"training\",\"title\":\"Модуль 1\",\"url\":\"https://school.example/teach/control/stream/view/id/20\",\"order\":1}]}";
        var wrapped = System.Text.Json.JsonSerializer.Serialize(inner);

        Assert.True(GetCourseCourseStructure.TryParsePage(
            wrapped,
            new Uri("https://school.example/teach/control/stream/view/id/10"),
            out var page));
        Assert.Single(page!.Trainings);
        Assert.Equal("Модуль 1", page.Trainings[0].Title);
    }

    [Fact]
    public void Parse_cleans_GetCourse_status_and_training_metadata_from_titles()
    {
        const string json = """
        {
          "pageTitle":"Курс",
          "links":[
            {
              "kind":"training",
              "title":"МОДУЛЬ №1 14 уроков. Тамара Хестанова",
              "url":"https://school.example/teach/control/stream/view/id/20",
              "order":1
            },
            {
              "kind":"lesson",
              "title":"День 2 Просмотрено Для служебного описания",
              "url":"https://school.example/teach/control/lesson/view/id/101",
              "order":2
            }
          ]
        }
        """;

        Assert.True(GetCourseCourseStructure.TryParsePage(
            json,
            new Uri("https://school.example/teach/control/stream/view/id/10"),
            out var page));
        Assert.Equal("МОДУЛЬ №1", page!.Trainings[0].Title);
        Assert.Equal("День 2", page.Lessons[0].Title);
    }
}
