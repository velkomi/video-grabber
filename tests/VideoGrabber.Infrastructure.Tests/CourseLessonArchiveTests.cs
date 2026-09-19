using System.IO.Compression;
using System.Text.Json;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseLessonArchiveTests
{
    [Fact]
    public void Parses_WebView_snapshot_and_filters_unsafe_assets()
    {
        var inner = JsonSerializer.Serialize(new
        {
            title = " Урок 1 ",
            text = "Строка 1\nСтрока 2",
            html = "<main>Текст</main>",
            assets = new object[]
            {
                new { url = "https://school.example/files/a.pdf", kind = "file", name = "Методичка.pdf" },
                new { url = "https://cdn.example/image.png", kind = "image", name = "Схема" },
                new { url = "http://127.0.0.1/private.txt", kind = "file", name = "bad" }
            }
        });
        var wrapped = JsonSerializer.Serialize(inner);

        Assert.True(CourseLessonArchive.TryParseWebViewResult(
            wrapped,
            new Uri("https://school.example/lesson"),
            out var snapshot));
        Assert.Equal("Урок 1", snapshot!.Title);
        Assert.Equal(2, snapshot.Assets.Count);
    }

    [Fact]
    public void Asset_name_preserves_safe_extension()
    {
        var name = CourseLessonArchive.AssetFileName(
            new Uri("https://school.example/files/42/report.pdf?x=1"),
            "Методичка",
            "Файл");
        Assert.Equal("Методичка.pdf", name);
    }

    [Fact]
    public void Asset_name_repairs_utf8_filename_misread_as_latin1()
    {
        var name = CourseLessonArchive.AssetFileName(
            new Uri("https://school.example/files/42/file.pdf"),
            null,
            "Файл",
            "Ð¢ÐµÑÑ.pdf");
        Assert.Equal("Тест.pdf", name);
    }

    [Fact]
    public void Lesson_folder_is_ordered_and_windows_safe()
    {
        Assert.Equal(
            "07 - Тема урока",
            CourseLessonArchive.LessonFolder(
                7,
                "Тема: урока?"));
    }

    [Fact]
    public void Docx_generator_drops_blank_dom_spacing()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-docx-blank-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "lesson.docx");
        try
        {
            CourseLessonArchive.WriteDocx(
                path,
                "Урок",
                new Uri("https://school.example/lesson/1"),
                "\n\n  Первый блок  \n\n\nВторой блок\n\n");

            using var archive = ZipFile.OpenRead(path);
            var document = archive.GetEntry("word/document.xml");
            Assert.NotNull(document);
            using var reader = new StreamReader(document!.Open());
            var xml = reader.ReadToEnd();

            Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(
                xml,
                "<w:p(?:>| )").Count);
            Assert.DoesNotContain("<w:t></w:t>", xml);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Writes_openable_minimal_docx_package()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-docx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "lesson.docx");
        try
        {

            CourseLessonArchive.WriteDocx(
                path,
                "Тема <1>",
                new Uri("https://school.example/lesson/1"),
                "Первый абзац\nВторой & абзац");

            Assert.True(File.Exists(path));
            using var archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            Assert.NotNull(archive.GetEntry("_rels/.rels"));
            var document = archive.GetEntry("word/document.xml");
            Assert.NotNull(document);
            using var reader = new StreamReader(document!.Open());
            var xml = reader.ReadToEnd();
            Assert.Contains("Тема &lt;1&gt;", xml);
            Assert.Contains("Второй &amp; абзац", xml);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
