using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Worker;

public sealed class ArtifactVerifier(
    BoundedProcessRunner runner,
    WorkerToolLocator tools)
{
    public async Task<ArtifactReceipt> VerifyAsync(
        string attemptRoot,
        string outputPath,
        int? expectedWidth,
        int? expectedHeight,
        string mediaType,
        string evidenceId,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(attemptRoot);
        var path = Path.GetFullPath(outputPath);
        if (!IsWithin(root, path) || !File.Exists(path))
            throw new UnauthorizedAccessException("artifact_outside_attempt_root");
        var info = new FileInfo(path);
        if (info.Length <= 0)
            throw new InvalidDataException("Artifact is empty.");
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("artifact_symlink_rejected");

        var probe = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffprobe,
            ["-v", "error", "-show_streams", "-show_format", "-of", "json", "--", path],
            root, TimeSpan.FromMinutes(1)), cancellationToken).ConfigureAwait(false);
        if (!probe.Success) throw new InvalidDataException("ffprobe rejected artifact.");
        using var json = JsonDocument.Parse(probe.StandardOutput);
        var streams = json.RootElement.GetProperty("streams");
        var video = streams.EnumerateArray().FirstOrDefault(
            x => x.TryGetProperty("codec_type", out var kind)
                 && kind.GetString() == "video");
        if (mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            if (video.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Expected video stream is missing.");
            if (expectedWidth is int width
                && (!video.TryGetProperty("width", out var w) || w.GetInt32() != width))
                throw new InvalidDataException("Artifact width differs from selected quality.");
            if (expectedHeight is int height
                && (!video.TryGetProperty("height", out var h) || h.GetInt32() != height))
                throw new InvalidDataException("Artifact height differs from selected quality.");
        }

        var decode = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffmpeg,
            ["-v", "error", "-xerror", "-i", path, "-f", "null", "-"],
            root, TimeSpan.FromHours(2)), cancellationToken).ConfigureAwait(false);
        if (!decode.Success)
            throw new InvalidDataException("Full media decode failed.");

        await using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
        return new ArtifactReceipt(
            Guid.NewGuid(),
            Convert.ToHexString(digest).ToLowerInvariant(),
            info.Length,
            mediaType,
            evidenceId,
            path);
    }

    private static bool IsWithin(string root, string path)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }
}
