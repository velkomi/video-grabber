using System.Diagnostics;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

[Collection("ProcessTreeSerial")]
public sealed class ProcessLifecycleTests
{
    [Fact]
    public async Task Callback_failure_drains_output_without_hanging_the_child()
    {
        if (!OperatingSystem.IsWindows()) return;
        var command = "for /L %i in (1,1,10000) do @echo test-output-%i";
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessRunner().RunAsync(new ProcessSpec(
            "cmd.exe", ["/d", "/c", command], Timeout: TimeSpan.FromSeconds(4)),
            _ => throw new InvalidOperationException("callback failed"), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_reaps_the_real_child_process()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var id = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessRunner().RunAsync(new ProcessSpec(
            "powershell.exe", ["-NoProfile", "-Command", "[Console]::WriteLine($PID); Start-Sleep -Seconds 30"]),
            line => { if (int.TryParse(line, out var child)) { id = child; cts.Cancel(); } }, cts.Token));
        Assert.True(id > 0);
        AssertExited(id);
    }

    [Fact]
    public async Task Normal_root_exit_reaps_pipe_holding_descendant_after_drain_deadline()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "VG-drain-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = Path.Combine(root, "state.txt");
        var ready = Path.Combine(root, "ready.txt");
        var go = Path.Combine(root, "go.signal");
        var fixture = FixturePath();
        try
        {
            var work = new ProcessRunner().RunAsync(new ProcessSpec("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-File", fixture,
                    "-StatePath", state, "-ReadyPath", ready, "-GoPath", go],
                Timeout: TimeSpan.FromSeconds(30)), null, CancellationToken.None);

            var childId = await WaitForOwnedChildEvidenceAsync(state, ready, TimeSpan.FromSeconds(20));
            using var child = Process.GetProcessById(childId);
            Assert.False(child.HasExited);
            File.WriteAllText(go, "drain");
            await Task.Delay(150);
            Assert.False(work.IsCompleted,
                "Runner completed while the verified descendant still held the inherited pipe.");
            var watch = Stopwatch.StartNew();
            var result = await work.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.True(result.IsSuccess);
            Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(6));
            child.Refresh();
            Assert.True(child.HasExited);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Timeout_is_reported_as_timeout_not_success()
    {
        if (!OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<TimeoutException>(() => new ProcessRunner().RunAsync(new ProcessSpec(
            "powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"],
            Timeout: TimeSpan.FromMilliseconds(300)), null, CancellationToken.None));
    }

    private static string FixturePath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "ProcessTreeFixture.ps1"));

    private static async Task<int> WaitForOwnedChildEvidenceAsync(
        string statePath, string readyPath, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (TryRead(statePath, out var state) && TryRead(readyPath, out var ready))
            {
                Assert.Equal(state.Pid, ready.Pid);
                Assert.Equal(state.StartTicks, ready.StartTicks);
                return state.Pid;
            }
            await Task.Delay(25);
        }
        throw new TimeoutException("Synthetic child did not publish readiness evidence.");
    }

    private static bool TryRead(string path, out (int Pid, long StartTicks) value)
    {
        value = default;
        if (!File.Exists(path)) return false;
        try
        {
            var parts = File.ReadAllText(path).Trim().Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var pid) || !long.TryParse(parts[1], out var ticks))
                return false;
            value = (pid, ticks);
            return true;
        }
        catch (IOException) { return false; }
    }

    private static void AssertExited(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Refresh();
            Assert.True(process.HasExited);
        }
        catch (ArgumentException) { }
    }
}
