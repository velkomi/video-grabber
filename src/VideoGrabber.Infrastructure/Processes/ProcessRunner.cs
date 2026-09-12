using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.Infrastructure.Processes;

public sealed class ProcessRunner
    : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(spec.Timeout ?? TimeSpan.FromHours(12));
        var info = new ProcessStartInfo
        {
            FileName = spec.FileName, WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in spec.Arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        var watch = Stopwatch.StartNew();
        var childId = Guid.NewGuid().ToString("N");
        var jobId = DiagnosticHub.CurrentJobId ?? childId;
        var stage = "process." + Path.GetFileNameWithoutExtension(spec.FileName);
        DiagnosticHub.Log.Write(stage, "started", "processId=" + childId, jobId: jobId);
        try
        {
            if (!process.Start()) throw new IOException("Компонент не запустился.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            DiagnosticHub.Log.Write(stage, "failed", ex.Message, jobId: jobId);
            throw new FileNotFoundException($"Не удалось запустить {Path.GetFileName(spec.FileName)}. Проверьте раздел «Компоненты».", ex);
        }
        var callbackGate = new object();
        ExceptionDispatchInfo? callbackFailure = null;
        var outputTask = PumpAsync(process.StandardOutput);
        var errorTask = PumpAsync(process.StandardError);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).WaitAsync(deadline.Token).ConfigureAwait(false);
            callbackFailure?.Throw();
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticHub.Log.Write(stage, process.ExitCode == 0 ? "succeeded" : "failed", "processId=" + childId,
                jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds, exitCode: process.ExitCode);
            return new(process.ExitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (callbackFailure is not null)
            {
                DiagnosticHub.Log.Write(stage, "failed", "Output callback failed: " + callbackFailure.SourceException.GetType().Name, jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds);
                callbackFailure.Throw();
            }
            DiagnosticHub.Log.Write(stage, cancellationToken.IsCancellationRequested ? "cancelled" : "failed",
                "processId=" + childId + (cancellationToken.IsCancellationRequested ? "" : " timeout"),
                jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Компонент превысил допустимое время работы.");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }

        async Task<string> PumpAsync(StreamReader reader)
        {
            const int maxTail = 262144, maxLine = 32768;
            var tail = new StringBuilder();
            var line = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var c = buffer[i];
                    if (c == '\n') { Emit(); line.Clear(); }
                    else if (line.Length < maxLine) line.Append(c);
                }
            }
            if (line.Length > 0) Emit();
            return tail.ToString();
            void Emit()
            {
                var raw = line.ToString().TrimEnd('\r');
                tail.AppendLine(raw);
                if (tail.Length > maxTail) tail.Remove(0, tail.Length - maxTail);
                if (!spec.SuppressOutputLogging)
                    DiagnosticHub.Log.Write(stage, "output", raw, debug: true, jobId: jobId);
                lock (callbackGate)
                {
                    if (callbackFailure is null)
                    {
                        try { onOutput?.Invoke(raw); }
                        catch (Exception ex)
                        {
                            callbackFailure = ExceptionDispatchInfo.Capture(ex);
                            deadline.Cancel();
                        }
                    }
                }
            }
        }
    }
}
