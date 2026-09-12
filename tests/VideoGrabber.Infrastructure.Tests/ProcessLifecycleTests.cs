using System.Diagnostics;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ProcessLifecycleTests
{
    [Fact]
    public async Task Callback_failure_drains_output_without_hanging_the_child()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = "1..10000 | ForEach-Object { [Console]::WriteLine('test-output-' + $_) }";
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessRunner().RunAsync(new ProcessSpec(
            "powershell.exe", ["-NoProfile", "-Command", script], Timeout: TimeSpan.FromSeconds(4)),
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
        try { using var process = Process.GetProcessById(id); Assert.True(process.HasExited); }
        catch (ArgumentException) { /* Windows has removed the reaped process. */ }
    }

    [Fact]
    public async Task Timeout_is_reported_as_timeout_not_success()
    {
        if (!OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<TimeoutException>(() => new ProcessRunner().RunAsync(new ProcessSpec(
            "powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"], Timeout: TimeSpan.FromMilliseconds(300)),
            null, CancellationToken.None));
    }
}
