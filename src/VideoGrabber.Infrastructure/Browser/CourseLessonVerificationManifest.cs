using System.Text.Json;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record CourseLessonVerificationManifest(
    int SchemaVersion,
    string LessonUrl,
    int ExpectedVideoCount,
    int ExpectedAssetCount,
    string Quality,
    DateTimeOffset UpdatedUtc);

public static class CourseLessonVerificationManifestStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "VideoGrabber.lesson.json";

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
            var path = Path.Combine(Path.GetFullPath(lessonFolder), FileName);
            if (!File.Exists(path) || new FileInfo(path).Length is <= 0 or > 262144) return false;
            manifest = JsonSerializer.Deserialize<CourseLessonVerificationManifest>(File.ReadAllText(path), JsonOptions);
            return manifest is { SchemaVersion: CurrentSchemaVersion, ExpectedVideoCount: >= 0, ExpectedAssetCount: >= 0 }
                && Uri.TryCreate(manifest.LessonUrl, UriKind.Absolute, out _);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            manifest = null;
            return false;
        }
    }

    public static async Task SaveAtomicAsync(string lessonFolder, CourseLessonVerificationManifest manifest, CancellationToken token)
    {
        var folder = Path.GetFullPath(lessonFolder);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        var temp = path + ".partial";
        var json = JsonSerializer.Serialize(manifest with
        {
            SchemaVersion = CurrentSchemaVersion,
            ExpectedVideoCount = Math.Max(0, manifest.ExpectedVideoCount),
            ExpectedAssetCount = Math.Max(0, manifest.ExpectedAssetCount),
            Quality = string.IsNullOrWhiteSpace(manifest.Quality) ? "best" : manifest.Quality.Trim(),
            UpdatedUtc = DateTimeOffset.UtcNow
        }, JsonOptions);
        await File.WriteAllTextAsync(temp, json, new System.Text.UTF8Encoding(false), token);
        File.Move(temp, path, overwrite: true);
    }
}
