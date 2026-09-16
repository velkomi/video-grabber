namespace VideoGrabber.Infrastructure.Components;

public sealed class ToolLocator
{
    private readonly string _baseDirectory;
    private readonly ComponentSetSnapshot? _snapshot;

    public ToolLocator(string? baseDirectory = null, string? localToolsDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        LocalToolsDirectory = Path.GetFullPath(localToolsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGrabber", "tools"));
        var pointer = Path.Combine(LocalToolsDirectory, "components-current.json");
        if (File.Exists(pointer)) _snapshot = ComponentSetSnapshot.Load(LocalToolsDirectory);
    }

    public ToolLocator(ComponentSetSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _snapshot.Verify();
        _baseDirectory = snapshot.GenerationDirectory;
        var parent = Directory.GetParent(snapshot.GenerationDirectory);
        LocalToolsDirectory = parent is not null && parent.Name.Equals("generations", StringComparison.OrdinalIgnoreCase) && parent.Parent is not null
            ? parent.Parent.FullName
            : parent?.FullName ?? snapshot.GenerationDirectory;
    }

    public string LocalToolsDirectory { get; }
    public ComponentSetSnapshot? Snapshot => _snapshot;

    public string YtDlp => _snapshot?.YtDlp ?? Find("yt-dlp.exe", "yt-dlp");
    public string Ffmpeg => _snapshot?.Ffmpeg ?? Find("ffmpeg.exe", "ffmpeg");
    public string Ffprobe => _snapshot?.Ffprobe ?? Find("ffprobe.exe", "ffprobe");
    public string Deno => _snapshot?.Deno ?? Find("deno.exe", "deno");

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
