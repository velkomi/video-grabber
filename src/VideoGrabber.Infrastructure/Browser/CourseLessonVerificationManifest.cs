﻿using System.Text.Json;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record CourseLessonVerificationManifest(
    int SchemaVersion,
    string LessonUrl,
    int ExpectedVideoCount,
    int ExpectedAssetCount,
    string Quality,
    DateTimeOffset UpdatedUtc);

public sealed record CourseVerificationIndex(
    int SchemaVersion,
    DateTimeOffset UpdatedUtc,
    CourseLessonVerificationManifest[] Lessons);

public static class CourseLessonVerificationManifestStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "VG.lesson.json";
    public const string LegacyFileName = "VideoGrabber.lesson.json";
    public const string CourseIndexFileName = "VG.verify.json";
    private const int MaxLessonBytes = 262_144;
    private const int MaxCourseIndexBytes = 8_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool TryLoad(string lessonFolder, out CourseLessonVerificationManifest? manifest)
    {
        manifest = null;
        try
        {
            var folder = Path.GetFullPath(lessonFolder);
            foreach (var name in new[] { FileName, LegacyFileName })
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                if (new FileInfo(path).Length is <= 0 or > MaxLessonBytes) continue;
                var parsed = JsonSerializer.Deserialize<CourseLessonVerificationManifest>(File.ReadAllText(path), JsonOptions);
                if (!IsValid(parsed)) continue;
                manifest = parsed;
                return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            manifest = null;
            return false;
        }
    }

    public static bool TryLoadForLesson(
        string courseRoot,
        string lessonFolder,
        Uri lessonUri,
        out CourseLessonVerificationManifest? manifest)
    {
        if (TryLoad(lessonFolder, out manifest) && manifest is not null)
            return SameLesson(manifest.LessonUrl, lessonUri);

        manifest = null;
        if (!TryLoadCourseIndex(courseRoot, out var index) || index is null)
            return false;

        var expectedKey = GetCourseCourseStructure.CanonicalKey(lessonUri);
        manifest = index.Lessons.FirstOrDefault(candidate =>
            Uri.TryCreate(candidate.LessonUrl, UriKind.Absolute, out var uri)
            && string.Equals(
                GetCourseCourseStructure.CanonicalKey(uri),
                expectedKey,
                StringComparison.Ordinal));
        return manifest is not null;
    }

    public static async Task SaveAtomicAsync(
        string lessonFolder,
        CourseLessonVerificationManifest manifest,
        CancellationToken token)
    {
        var folder = Path.GetFullPath(lessonFolder);
        Directory.CreateDirectory(folder);
        var normalized = Normalize(manifest);
        var path = Path.Combine(folder, FileName);
        await WriteAtomicAsync(path, normalized, MaxLessonBytes, token);

        // Migrate the old verbose name only after the new file is safely persisted.
        var legacy = Path.Combine(folder, LegacyFileName);
        try
        {
            if (File.Exists(legacy)) File.Delete(legacy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static async Task<int> ConsolidateCourseAsync(
        string courseRoot,
        IReadOnlyList<(string LessonFolder, Uri LessonUri)> lessons,
        CancellationToken token)
    {
        var root = Path.GetFullPath(courseRoot);
        var manifests = new List<CourseLessonVerificationManifest>(lessons.Count);
        foreach (var lesson in lessons)
        {
            token.ThrowIfCancellationRequested();
            if (!TryLoadForLesson(root, lesson.LessonFolder, lesson.LessonUri, out var manifest)
                || manifest is null)
                throw new InvalidDataException(
                    "Не найден служебный manifest финальной проверки урока.");
            manifests.Add(Normalize(manifest));
        }

        var distinct = manifests
            .GroupBy(manifest =>
            {
                var uri = new Uri(manifest.LessonUrl, UriKind.Absolute);
                return GetCourseCourseStructure.CanonicalKey(uri);
            }, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.UpdatedUtc).First())
            .ToArray();

        if (distinct.Length != lessons.Count)
            throw new InvalidDataException(
                "Итоговый индекс проверки курса содержит неполный набор уроков.");

        var index = new CourseVerificationIndex(
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            distinct);
        var indexPath = Path.Combine(root, CourseIndexFileName);
        await WriteAtomicAsync(indexPath, index, MaxCourseIndexBytes, token);

        if (!TryLoadCourseIndex(root, out var check)
            || check is null
            || check.Lessons.Length != distinct.Length)
            throw new InvalidDataException(
                "Не удалось подтвердить итоговый индекс проверки курса.");

        var deleted = 0;
        foreach (var lesson in lessons)
        {
            foreach (var name in new[] { FileName, LegacyFileName })
            {
                var path = Path.Combine(lesson.LessonFolder, name);
                try
                {
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        return deleted;
    }

    public static bool TryLoadCourseIndex(
        string courseRoot,
        out CourseVerificationIndex? index)
    {
        index = null;
        try
        {
            var path = Path.Combine(Path.GetFullPath(courseRoot), CourseIndexFileName);
            if (!File.Exists(path) || new FileInfo(path).Length is <= 0 or > MaxCourseIndexBytes)
                return false;
            var parsed = JsonSerializer.Deserialize<CourseVerificationIndex>(File.ReadAllText(path), JsonOptions);
            if (parsed is null
                || parsed.SchemaVersion != CurrentSchemaVersion
                || parsed.Lessons is null
                || parsed.Lessons.Any(item => !IsValid(item)))
                return false;
            index = parsed;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            index = null;
            return false;
        }
    }

    private static bool SameLesson(string value, Uri expected)
        => Uri.TryCreate(value, UriKind.Absolute, out var parsed)
           && string.Equals(
               GetCourseCourseStructure.CanonicalKey(parsed),
               GetCourseCourseStructure.CanonicalKey(expected),
               StringComparison.Ordinal);

    private static bool IsValid(CourseLessonVerificationManifest? manifest)
        => manifest is
        {
            SchemaVersion: CurrentSchemaVersion,
            ExpectedVideoCount: >= 0,
            ExpectedAssetCount: >= 0
        }
        && Uri.TryCreate(manifest.LessonUrl, UriKind.Absolute, out _);

    private static CourseLessonVerificationManifest Normalize(
        CourseLessonVerificationManifest manifest)
        => manifest with
        {
            SchemaVersion = CurrentSchemaVersion,
            ExpectedVideoCount = Math.Max(0, manifest.ExpectedVideoCount),
            ExpectedAssetCount = Math.Max(0, manifest.ExpectedAssetCount),
            Quality = string.IsNullOrWhiteSpace(manifest.Quality) ? "best" : manifest.Quality.Trim(),
            UpdatedUtc = DateTimeOffset.UtcNow
        };

    private static async Task WriteAtomicAsync<T>(
        string path,
        T value,
        int maxBytes,
        CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".partial";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > maxBytes)
            throw new InvalidDataException("Служебный файл проверки превышает безопасный размер.");
        try
        {
            await File.WriteAllTextAsync(temp, json, new System.Text.UTF8Encoding(false), token);
            token.ThrowIfCancellationRequested();
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
    }
}
