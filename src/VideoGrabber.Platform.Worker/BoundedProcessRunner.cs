using System.Diagnostics;
using System.Text;

namespace VideoGrabber.Platform.Worker;

public sealed record WorkerProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string?>? Environment = null);

public sealed record WorkerProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration)
{
    public bool Success => ExitCode == 0;
}

public sealed class BoundedProcessRunner
{
    private const int MaxCapturedChars = 1024 * 1024;

    public async Task<WorkerProcessResult> RunAsync(
        WorkerProcessSpec spec,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.FileName);
        if (spec.Timeout <= TimeSpan.Zero || spec.Timeout > TimeSpan.FromHours(2))
            throw new ArgumentOutOfRangeException(nameof(spec));
        var working = Path.GetFullPath(spec.WorkingDirectory);
        if (!Directory.Exists(working))
            throw new DirectoryNotFoundException(working);

        var start = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            WorkingDirectory = working
        };
        foreach (var argument in spec.Arguments) start.ArgumentList.Add(argument);
        if (spec.Environment is not null)
            foreach (var pair in spec.Environment)
                start.Environment[pair.Key] = pair.Value;

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) { stdoutClosed.TrySetResult(); return; }
            AppendBounded(stdout, e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) { stderrClosed.TrySetResult(); return; }
            AppendBounded(stderr, e.Data);
        };

        var watch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException("Worker process could not be started.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(spec.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException("Worker process exceeded its deadline.");
        }
        finally
        {
            watch.Stop();
        }

        return new WorkerProcessResult(
            process.ExitCode, stdout.ToString(), stderr.ToString(), watch.Elapsed);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void AppendBounded(StringBuilder builder, string line)
    {
        if (builder.Length >= MaxCapturedChars) return;
        var remaining = MaxCapturedChars - builder.Length;
        if (line.Length > remaining) line = line[..remaining];
        builder.AppendLine(line);
    }
}
