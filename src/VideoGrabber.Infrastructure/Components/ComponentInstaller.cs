using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Components;

public sealed class ComponentInstaller(
    IProcessRunner runner,
    IComponentInstallTransaction transaction)
{
    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(30);

    public async Task<ProcessResult> InstallAsync(
        string script, string destination, CancellationToken token, string? assetManifestPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        token.ThrowIfCancellationRequested();

        ProcessResult prepared;
        try
        {
            prepared = await runner.RunAsync(
                BuildPrepareSpec(script, destination, assetManifestPath), null, token).ConfigureAwait(false);
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

        if (token.IsCancellationRequested)
        {
            await transaction.AbortRecoveryAsync(destination, recovery.Token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        return prepared;
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

    private static ProcessSpec BuildPrepareSpec(string script, string destination, string? assetManifestPath)
    {
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var arguments = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", script, "-Destination", destination, "-Phase", "Prepare"
        };
        if (!string.IsNullOrWhiteSpace(assetManifestPath))
        {
            arguments.Add("-AssetManifestPath");
            arguments.Add(assetManifestPath);
        }
        return new ProcessSpec(powershell, arguments,
            Timeout: PrepareTimeout, SuppressOutputLogging: true);
    }
}
