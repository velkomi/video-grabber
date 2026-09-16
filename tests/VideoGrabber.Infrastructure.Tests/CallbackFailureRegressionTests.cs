using System.Diagnostics;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

[Collection("ProcessTreeSerial")]
public sealed class CallbackFailureRegressionTests
{
    [Fact]
    public async Task Failed_callback_aborts_instead_of_waiting_for_process_timeout()
    {
        if (!OperatingSystem.IsWindows()) return;
        var callbackAt = TimeSpan.Zero;
        var completedAt = TimeSpan.Zero;
        var started = Stopwatch.StartNew();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            try
            {
                await new ProcessRunner().RunAsync(
                    new ProcessSpec("cmd.exe", ["/d", "/c", "echo tick & ping -n 30 127.0.0.1 >nul"], Timeout: TimeSpan.FromSeconds(7)),
                    _ => { callbackAt = started.Elapsed; throw new InvalidOperationException("callback failed"); },
                    CancellationToken.None);
            }
            finally { completedAt = started.Elapsed; }
        });
        Assert.True(callbackAt > TimeSpan.Zero);
        Assert.True(completedAt - callbackAt < TimeSpan.FromSeconds(2),            $"Callback abort took {(completedAt - callbackAt).TotalSeconds:F1}s after first output");
    }

    [Fact]
    public async Task Callback_failure_reaps_pipe_holding_descendant()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "VG-callback-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = Path.Combine(root, "state.txt");
        var ready = Path.Combine(root, "ready.txt");
        var go = Path.Combine(root, "go.signal");
        var fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "ProcessTreeFixture.ps1"));
        var childId = 0;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessRunner().RunAsync(
                new ProcessSpec("powershell.exe",
                    ["-NoProfile", "-NonInteractive", "-File", fixture,
                        "-StatePath", state, "-ReadyPath", ready, "-GoPath", go], Timeout: TimeSpan.FromSeconds(30)), line =>
                    {
                        const string childPrefix = "AUDIT_CHILD_PID_FROM_PARENT:";
                        if (line.StartsWith(childPrefix, StringComparison.Ordinal))
                        {
                            childId = int.Parse(line[childPrefix.Length..]);
                            File.WriteAllText(go, "callback");
                        }
                        if (line.StartsWith("AUDIT_CHILD_PIPE_HELD:", StringComparison.Ordinal))
                            throw new InvalidOperationException("callback failed after inherited pipe proof");
                    }, CancellationToken.None));            Assert.True(childId > 0);
            try
            {
                using var child = Process.GetProcessById(childId);
                child.Refresh();
                Assert.True(child.HasExited);
            }
            catch (ArgumentException) { }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}