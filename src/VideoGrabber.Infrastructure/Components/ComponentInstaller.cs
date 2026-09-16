using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Components;

public sealed class ComponentInstaller(
    IProcessRunner runner,
    IComponentInstallTransaction transaction)
{
    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(30);

    public async Task<ProcessResult> InstallAsync(
        string script, string destination, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        token.ThrowIfCancellationRequested();

        ProcessResult prepared;
        try
        {
            prepared = await runner.RunAsync(BuildPrepareSpec(script, destination), null, token)
                .ConfigureAwait(false);
        }
        catch
        {
            await AbortRecoveryAsync(destination).ConfigureAwait(false);
            throw;
        }

        if (!prepared.IsSuccess)
        {
            await AbortRecoveryAsync(destination).ConfigureAwait(false);
            return prepared;
        }

        using var recovery = new CancellationTokenSource(RecoveryTimeout);
        try
        {
            await transaction.CommitAsync(destination, recovery.Token).ConfigureAwait(false);
            return prepared;
        }
        catch (Exception commitError)
        {
            try
            {
                await transaction.AbortRecoveryAsync(destination, recovery.Token).ConfigureAwait(false);
            }
            catch (Exception recoveryError)
            {
                throw new InvalidOperationException(
                    "Component installation recovery failed after commit error.",
                    new AggregateException(commitError, recoveryError));
            }
            throw;
        }
    }

    private async Task AbortRecoveryAsync(string destination)
    {
        using var recovery = new CancellationTokenSource(RecoveryTimeout);
        try
        {
            await transaction.AbortRecoveryAsync(destination, recovery.Token).ConfigureAwait(false);
        }
        catch (Exception recoveryError)
        {
            throw new InvalidOperationException("Component installation recovery failed.", recoveryError);
        }
    }

    private static ProcessSpec BuildPrepareSpec(string script, string destination)
    {
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return new ProcessSpec(
            powershell,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", script, "-Destination", destination],
            Timeout: PrepareTimeout,
            SuppressOutputLogging: true);
    }
}
