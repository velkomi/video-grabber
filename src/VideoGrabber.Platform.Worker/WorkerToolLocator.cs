namespace VideoGrabber.Platform.Worker;

public sealed class WorkerToolLocator
{
    public WorkerToolLocator(
        string? ytDlp = null,
        string? ffmpeg = null,
        string? ffprobe = null,
        string? whisper = null)
    {
        YtDlp = Resolve(ytDlp, "VG_WORKER_YTDLP", "yt-dlp");
        Ffmpeg = Resolve(ffmpeg, "VG_WORKER_FFMPEG", "ffmpeg");
        Ffprobe = Resolve(ffprobe, "VG_WORKER_FFPROBE", "ffprobe");
        Whisper = Resolve(whisper, "VG_WORKER_WHISPER", "whisper-cli");
    }

    public string YtDlp { get; }
    public string Ffmpeg { get; }
    public string Ffprobe { get; }
    public string Whisper { get; }

    private static string Resolve(string? explicitPath, string variable, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(explicitPath)
            ? Environment.GetEnvironmentVariable(variable)
            : explicitPath;
        value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Tool path contains control characters.");
        return value;
    }
}
