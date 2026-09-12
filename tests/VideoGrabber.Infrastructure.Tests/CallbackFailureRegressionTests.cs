using System.Diagnostics;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;
namespace VideoGrabber.Infrastructure.Tests;
public sealed class CallbackFailureRegressionTests
{
    [Fact]
    public async Task Failed_callback_aborts_instead_of_waiting_for_process_timeout()
    {
        if (!OperatingSystem.IsWindows()) return;
        var started = Stopwatch.StartNew();
        var childId = 0;
        var script = "[Console]::WriteLine($PID); while($true) { [Console]::WriteLine('tick'); Start-Sleep -Milliseconds 20 }";
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessRunner().RunAsync(
            new ProcessSpec("powershell.exe", ["-NoProfile", "-Command", script], Timeout: TimeSpan.FromSeconds(7)),
            line => { if (int.TryParse(line, out var id)) childId = id; throw new InvalidOperationException("callback failed"); },
            CancellationToken.None));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(4), $"Failure took {started.Elapsed.TotalSeconds:F1}s");
        Assert.True(childId > 0);
        try { using var child = Process.GetProcessById(childId); Assert.True(child.HasExited); }
        catch (ArgumentException) { }
    }
}
