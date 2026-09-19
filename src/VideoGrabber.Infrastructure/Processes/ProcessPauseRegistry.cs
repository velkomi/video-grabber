using System.Collections.Concurrent;

namespace VideoGrabber.Infrastructure.Processes;

public static class ProcessPauseRegistry
{
    private static readonly ConcurrentDictionary<int, INativeChildProcessHandle> Active = new();
    private static int _paused;

    public static bool IsPaused => Volatile.Read(ref _paused) != 0;

    internal static void Register(INativeChildProcessHandle process)
    {
        Active[process.Id] = process;
        if (!IsPaused) return;
        try { process.Suspend(); }
        catch { }
    }

    internal static void Unregister(INativeChildProcessHandle process)
        => Active.TryRemove(process.Id, out _);

    public static void PauseAll()
    {
        Interlocked.Exchange(ref _paused, 1);
        foreach (var process in Active.Values)
        {
            try { process.Suspend(); }
            catch { }
        }
    }

    public static void ResumeAll()
    {
        Interlocked.Exchange(ref _paused, 0);
        foreach (var process in Active.Values)
        {
            try { process.Resume(); }
            catch { }
        }
    }
}
