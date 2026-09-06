using System.Diagnostics;
using System.Text;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessSpec spec,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) => Capture(eventArgs.Data, stdout, onOutput);
        process.ErrorDataReceived += (_, eventArgs) => Capture(eventArgs.Data, stderr, onOutput);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Не удалось запустить компонент {Path.GetFileName(spec.FileName)}.");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new FileNotFoundException($"Компонент {Path.GetFileName(spec.FileName)} не найден. Откройте раздел Компоненты.", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void Capture(string? line, StringBuilder destination, Action<string>? callback)
    {
        if (line is null)
        {
            return;
        }

        var safeLine = SensitiveDataRedactor.Redact(line);
        destination.AppendLine(safeLine);
        callback?.Invoke(safeLine);
    }
}

