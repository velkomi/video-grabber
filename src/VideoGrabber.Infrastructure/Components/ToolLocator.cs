namespace VideoGrabber.Infrastructure.Components;

public sealed class ToolLocator
{
    private readonly string _baseDirectory;
    private readonly ComponentSetSnapshot? _snapshot;
    private readonly string _bundledToolsDirectory;

    public ToolLocator(string? baseDirectory = null, string? localToolsDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        _bundledToolsDirectory = Path.Combine(_baseDirectory, "tools");
        LocalToolsDirectory = Path.GetFullPath(localToolsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGrabber", "tools"));

        // A complete runtime shipped beside VideoGrabber is authoritative. This
        // prevents a stale/partial user-profile generation from shadowing the
        // self-contained package.
        if (!BundledCoreToolsAvailable())
        {
            var pointer = Path.Combine(LocalToolsDirectory, "components-current.json");
            if (File.Exists(pointer)) _snapshot = ComponentSetSnapshot.Load(LocalToolsDirectory);
        }
    }

    public ToolLocator(ComponentSetSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _snapshot.Verify();
        _baseDirectory = snapshot.GenerationDirectory;
        _bundledToolsDirectory = Path.Combine(_baseDirectory, "tools");
        var parent = Directory.GetParent(snapshot.GenerationDirectory);
        LocalToolsDirectory = parent is not null && parent.Name.Equals("generations", StringComparison.OrdinalIgnoreCase) && parent.Parent is not null
            ? parent.Parent.FullName
            : parent?.FullName ?? snapshot.GenerationDirectory;
    }

    public string LocalToolsDirectory { get; }
    public string BundledToolsDirectory => _bundledToolsDirectory;
    public ComponentSetSnapshot? Snapshot => _snapshot;
    public bool UsesBundledRuntime => BundledCoreToolsAvailable();

    public string YtDlp => UsesBundledRuntime ? Path.Combine(_bundledToolsDirectory, "yt-dlp.exe")
        : _snapshot?.YtDlp ?? Find("yt-dlp.exe", "yt-dlp");
    public string Ffmpeg => UsesBundledRuntime ? Path.Combine(_bundledToolsDirectory, "ffmpeg.exe")
        : _snapshot?.Ffmpeg ?? Find("ffmpeg.exe", "ffmpeg");
    public string Ffprobe => UsesBundledRuntime ? Path.Combine(_bundledToolsDirectory, "ffprobe.exe")
        : _snapshot?.Ffprobe ?? Find("ffprobe.exe", "ffprobe");
    public string Deno => UsesBundledRuntime ? Path.Combine(_bundledToolsDirectory, "deno.exe")
        : _snapshot?.Deno ?? Find("deno.exe", "deno");

    public string WhisperCli => FindBundledWhisper("whisper-cli.exe");
    public string WhisperModel => FindBundledWhisper("ggml-base.bin");
    public bool WhisperAvailable => File.Exists(WhisperCli) && File.Exists(WhisperModel);

    private bool BundledCoreToolsAvailable()
        => File.Exists(Path.Combine(_bundledToolsDirectory, "yt-dlp.exe"))
           && File.Exists(Path.Combine(_bundledToolsDirectory, "ffmpeg.exe"))
           && File.Exists(Path.Combine(_bundledToolsDirectory, "ffprobe.exe"))
           && File.Exists(Path.Combine(_bundledToolsDirectory, "deno.exe"));

    private string FindBundledWhisper(string name)
    {
        var candidates = new[]
        {
            Path.Combine(_bundledToolsDirectory, "whisper", name),
            Path.Combine(_baseDirectory, "whisper", name),
            Path.Combine(LocalToolsDirectory, "whisper", name)
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

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
