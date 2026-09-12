using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ProcessRunnerSafetyTests
{
    [Fact]
    public async Task Runner_preserves_machine_output_before_log_redaction()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string original = "https://example.test/private?token=sentinel";
        string? seen = null;
        var result = await new ProcessRunner().RunAsync(new ProcessSpec("cmd.exe", ["/d", "/c", "echo " + original]),
            line => seen = line, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Contains(original, result.StandardOutput);
        Assert.Equal(original, seen);
    }

    [Fact]
    public async Task Runner_pre_cancel_does_not_start_missing_program()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ProcessRunner().RunAsync(
            new ProcessSpec("vg-nonexistent-program.exe", []), null, cts.Token));
    }
}
