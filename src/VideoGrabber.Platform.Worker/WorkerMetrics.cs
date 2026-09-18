namespace VideoGrabber.Platform.Worker;

public sealed record WorkerMetricsSnapshot(
    DateTimeOffset CapturedAt,
    double FreeDiskPercent,
    long JobRootBytes,
    long JobFileCount,
    bool FfmpegAvailable,
    bool FfprobeAvailable,
    bool YtDlpAvailable);

public static class WorkerMetrics
{
    public static bool DiskTooLow(double freePercent)
        => freePercent < 20d;

    public static bool HeartbeatStale(TimeSpan age)
        => age > TimeSpan.FromSeconds(60);

    public static WorkerMetricsSnapshot Capture(
        string jobRoot,
        WorkerToolLocator tools,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobRoot);
        ArgumentNullException.ThrowIfNull(tools);
        var full = Path.GetFullPath(jobRoot);
        Directory.CreateDirectory(full);

        long bytes = 0;
        long files = 0;
        foreach (var file in Directory.EnumerateFiles(
                     full,
                     "*",
                     SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                bytes = checked(bytes + info.Length);
                files++;
            }
            catch (IOException)
            {
                // A concurrent cleanup can remove a file between enumerate/stat.
            }
        }

        return new WorkerMetricsSnapshot(
            (clock ?? TimeProvider.System).GetUtcNow(),
            FreeDiskPercent(full),
            bytes,
            files,
            ExecutableExists(tools.Ffmpeg),
            ExecutableExists(tools.Ffprobe),
            ExecutableExists(tools.YtDlp));
    }

    public static bool Ready(WorkerMetricsSnapshot snapshot)
        => !DiskTooLow(snapshot.FreeDiskPercent)
           && snapshot.FfmpegAvailable
           && snapshot.FfprobeAvailable
           && snapshot.YtDlpAvailable;

    private static double FreeDiskPercent(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root)) return 0;
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0) return 0;
            return 100d * drive.AvailableFreeSpace / drive.TotalSize;
        }
        catch
        {
            return 0;
        }
    }

    private static bool ExecutableExists(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (Path.IsPathFullyQualified(value))
            return File.Exists(value);
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return false;
        foreach (var root in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(root, value);
            if (File.Exists(candidate)) return true;
            if (OperatingSystem.IsWindows()
                && File.Exists(candidate + ".exe"))
                return true;
        }
        return false;
    }
}
