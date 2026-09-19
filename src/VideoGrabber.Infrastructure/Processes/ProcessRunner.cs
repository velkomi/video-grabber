using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(spec.Timeout ?? TimeSpan.FromHours(12));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var watch = Stopwatch.StartNew();
        var childId = Guid.NewGuid().ToString("N");
        var jobId = DiagnosticHub.CurrentJobId ?? childId;
        var stage = "process." + Path.GetFileNameWithoutExtension(spec.FileName);
        var callbackGate = new object();
        ExceptionDispatchInfo? callbackFailure = null;
        var cleanupStarted = 0;
        var disposedByCleanup = false;
        INativeChildProcessHandle? process = null;
        Task<string>? outputTask = null;
        Task<string>? errorTask = null;

        DiagnosticHub.Log.Write(stage, "started", "processId=" + childId, jobId: jobId);
        try
        {
            try { process = WindowsSuspendedProcessLauncher.Start(spec); ProcessPauseRegistry.Register(process); }
            catch (Exception ex) when (ex is Win32Exception or IOException)
            {
                DiagnosticHub.Log.Write(stage, "failed", ex.GetType().Name, jobId: jobId);
                throw new FileNotFoundException($"РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РїСѓСЃС‚РёС‚СЊ {Path.GetFileName(spec.FileName)}.", ex);
            }

            outputTask = PumpAsync(process.StandardOutput);
            errorTask = PumpAsync(process.StandardError);
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
            var exitCode = process.ExitCode;

            try
            {
                await Task.WhenAll(outputTask, errorTask)
                    .WaitAsync(TimeSpan.FromSeconds(2), stop.Token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await CleanupOwnedTreeAsync().ConfigureAwait(false);
            }

            callbackFailure?.Throw();
            cancellationToken.ThrowIfCancellationRequested();
            if (timeout.IsCancellationRequested)
                throw new TimeoutException("РџСЂРѕС†РµСЃСЃ РїСЂРµРІС‹СЃРёР» РґРѕРїСѓСЃС‚РёРјРѕРµ РІСЂРµРјСЏ СЂР°Р±РѕС‚С‹.");

            DiagnosticHub.Log.Write(stage, exitCode == 0 ? "succeeded" : "failed", "processId=" + childId,
                jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds, exitCode: exitCode);
            return new(exitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            await CleanupOwnedTreeAsync().ConfigureAwait(false);
            if (callbackFailure is not null)
            {
                DiagnosticHub.Log.Write(stage, "failed", "Output callback failed: " + callbackFailure.SourceException.GetType().Name,
                    jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds);
                callbackFailure.Throw();
            }
            if (cancellationToken.IsCancellationRequested)
            {
                DiagnosticHub.Log.Write(stage, "cancelled", "processId=" + childId,
                    jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds);
                cancellationToken.ThrowIfCancellationRequested();
            }
            DiagnosticHub.Log.Write(stage, "failed", "processId=" + childId + " timeout",
                jobId: jobId, durationMs: watch.Elapsed.TotalMilliseconds);
            throw new TimeoutException("РџСЂРѕС†РµСЃСЃ РїСЂРµРІС‹СЃРёР» РґРѕРїСѓСЃС‚РёРјРѕРµ РІСЂРµРјСЏ СЂР°Р±РѕС‚С‹.");
        }
        catch
        {
            await CleanupOwnedTreeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (process is not null)
                ProcessPauseRegistry.Unregister(process);
            if (process is not null && !disposedByCleanup)
            {
                try { process.TerminateOwnedTree(); }
                catch (Exception ex) { DiagnosticHub.Log.Write(stage, "cleanup-failed", ex.GetType().Name, jobId: jobId, debug: true); }
                process.Dispose();
            }
        }

        async Task CleanupOwnedTreeAsync()
        {
            if (process is null) return;
            if (Interlocked.Exchange(ref cleanupStarted, 1) == 0)
            {
                try { process.TerminateOwnedTree(); }
                catch (Exception ex) { DiagnosticHub.Log.Write(stage, "cleanup-failed", ex.GetType().Name, jobId: jobId, debug: true); }
            }
            if (outputTask is null || errorTask is null) return;
            try
            {
                await Task.WhenAll(outputTask, errorTask).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                process.Dispose();
                disposedByCleanup = true;
                try { await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false); }
                catch (Exception ex) when (ex is ObjectDisposedException or IOException) { }
            }
        }

        Task<string> PumpAsync(StreamReader reader) => Task.Run(() =>
        {
            const int maxTail = 262144, maxLine = 32768;
            var tail = new StringBuilder();
            try
            {
                while (reader.ReadLine() is { } rawLine)
                {
                    var raw = rawLine.Length <= maxLine ? rawLine : rawLine[..maxLine];
                    tail.AppendLine(raw);
                    if (tail.Length > maxTail) tail.Remove(0, tail.Length - maxTail);
                    if (!spec.SuppressOutputLogging)
                        DiagnosticHub.Log.Write(stage, "output", raw, debug: true, jobId: jobId);
                    lock (callbackGate)
                    {
                        if (callbackFailure is not null) continue;
                        try { onOutput?.Invoke(raw); }
                        catch (Exception ex)
                        {
                            callbackFailure = ExceptionDispatchInfo.Capture(ex);
                            stop.Cancel();
                        }
                    }
                }
            }
            catch (Exception ex) when (Volatile.Read(ref cleanupStarted) != 0 && ex is ObjectDisposedException or IOException) { }
            return tail.ToString();
        });
    }
}
