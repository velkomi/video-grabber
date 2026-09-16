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
            "powershell.exe", ["-NoProfile", "-Command", "[Console]::WriteLine($PID); Start-Sleep -Seconds 30"]),            line => { if (int.TryParse(line, out var child)) { id = child; cts.Cancel(); } }, cts.Token));
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
        var childId = 0;
        try
        {
            var result = await new ProcessRunner().RunAsync(new ProcessSpec("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-File", fixture,
                    "-StatePath", state, "-ReadyPath", ready, "-GoPath", go], Timeout: TimeSpan.FromSeconds(30)), line =>
                {
                    const string prefix = "AUDIT_CHILD_PID_FROM_PARENT:";
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) return;
                    childId = int.Parse(line[prefix.Length..]);
                    File.WriteAllText(go, "drain");
                }, CancellationToken.None);
            Assert.True(result.IsSuccess);            Assert.True(childId > 0);
            Assert.Contains("AUDIT_CHILD_PIPE_HELD:" + childId, result.StandardOutput);
            AssertExited(childId);
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
            "powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"], Timeout: TimeSpan.FromMilliseconds(300)),
            null, CancellationToken.None));
    }

    private static string FixturePath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "ProcessTreeFixture.ps1"));

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