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
        Assert.True(completedAt - callbackAt < TimeSpan.FromSeconds(2),
            $"Callback abort took {(completedAt - callbackAt).TotalSeconds:F1}s after first output");
    }
}
