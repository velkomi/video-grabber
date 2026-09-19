using System.Text.Json;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseLessonVerificationManifestTests
{
    [Fact]
    public async Task Manifest_roundtrips_under_short_vg_filename()
    {
        var root = TempRoot("roundtrip");
        try
        {
            var manifest = Manifest(123, 21, 4, "480p");
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(root, manifest, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "VG.lesson.json")));
            Assert.False(File.Exists(Path.Combine(root, "VideoGrabber.lesson.json")));
            Assert.True(CourseLessonVerificationManifestStore.TryLoad(root, out var loaded));
            Assert.NotNull(loaded);
            Assert.Equal(21, loaded!.ExpectedVideoCount);
            Assert.Equal(4, loaded.ExpectedAssetCount);
            Assert.Equal("480p", loaded.Quality);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Save_migrates_legacy_verbose_filename()
    {
        var root = TempRoot("legacy");
        try
        {
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(root, Manifest(12, 2, 1, "best"), CancellationToken.None);
            var current = Path.Combine(root, CourseLessonVerificationManifestStore.FileName);
            var legacy = Path.Combine(root, CourseLessonVerificationManifestStore.LegacyFileName);
            File.Move(current, legacy);
            Assert.True(CourseLessonVerificationManifestStore.TryLoad(root, out _));

            await CourseLessonVerificationManifestStore.SaveAtomicAsync(root, Manifest(12, 2, 1, "best"), CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(root, CourseLessonVerificationManifestStore.FileName)));
            Assert.False(File.Exists(legacy));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Final_consolidation_writes_one_root_index_and_removes_lesson_files()
    {
        var root = TempRoot("consolidate");
        var lesson1 = Path.Combine(root, "01 - lesson");
        var lesson2 = Path.Combine(root, "02 - lesson");
        try
        {
            var m1 = Manifest(101, 3, 2, "360p");
            var m2 = Manifest(102, 1, 0, "360p");
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(lesson1, m1, CancellationToken.None);
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(lesson2, m2, CancellationToken.None);

            var deleted = await CourseLessonVerificationManifestStore.ConsolidateCourseAsync(
                root,
                [
                    (lesson1, new Uri(m1.LessonUrl)),
                    (lesson2, new Uri(m2.LessonUrl))
                ],
                CancellationToken.None);

            Assert.Equal(2, deleted);
            Assert.False(File.Exists(Path.Combine(lesson1, CourseLessonVerificationManifestStore.FileName)));
            Assert.False(File.Exists(Path.Combine(lesson2, CourseLessonVerificationManifestStore.FileName)));
            Assert.True(File.Exists(Path.Combine(root, CourseLessonVerificationManifestStore.CourseIndexFileName)));
            Assert.True(CourseLessonVerificationManifestStore.TryLoadCourseIndex(root, out var index));
            Assert.NotNull(index);
            Assert.Equal(2, index!.Lessons.Length);
            Assert.True(CourseLessonVerificationManifestStore.TryLoadForLesson(root, lesson1, new Uri(m1.LessonUrl), out var restored));
            Assert.Equal(3, restored!.ExpectedVideoCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void Missing_manifest_and_course_index_are_not_treated_as_verified()
    {
        var root = TempRoot("missing");
        var lesson = Path.Combine(root, "lesson");
        try
        {
            Assert.False(CourseLessonVerificationManifestStore.TryLoad(lesson, out var loaded));
            Assert.Null(loaded);
            Assert.False(CourseLessonVerificationManifestStore.TryLoadForLesson(
                root,
                lesson,
                new Uri("https://academy.example/teach/control/lesson/view/id/999"),
                out _));
        }
        finally { Delete(root); }
    }

    private static CourseLessonVerificationManifest Manifest(int id, int videos, int assets, string quality)
        => new(
            CourseLessonVerificationManifestStore.CurrentSchemaVersion,
            $"https://academy.example/teach/control/lesson/view/id/{id}",
            videos,
            assets,
            quality,
            DateTimeOffset.UtcNow);

    private static string TempRoot(string suffix)
        => Path.Combine(Path.GetTempPath(), "vg-lesson-manifest-" + suffix + "-" + Guid.NewGuid().ToString("N"));

    private static void Delete(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); }
        catch (IOException) { }
    }
}
