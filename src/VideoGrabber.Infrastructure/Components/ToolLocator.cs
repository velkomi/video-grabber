namespace VideoGrabber.Infrastructure.Components;

public sealed class ToolLocator
{
    private readonly string _baseDirectory;

    public ToolLocator(string? baseDirectory = null, string? localToolsDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        LocalToolsDirectory = localToolsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGrabber",
            "tools");
    }

    public string LocalToolsDirectory { get; }

    public string YtDlp => Find("yt-dlp.exe", "yt-dlp");
    public string Ffmpeg => Find("ffmpeg.exe", "ffmpeg");
    public string Ffprobe => Find("ffprobe.exe", "ffprobe");
    public string Deno => Find("deno.exe", "deno");

    private string Find(string windowsName, string fallback)
    {
        var candidates = new[]
        {
            Path.Combine(_baseDirectory, "tools", windowsName),
            Path.Combine(_baseDirectory, windowsName),
            Path.Combine(LocalToolsDirectory, windowsName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? fallback;
    }
}
