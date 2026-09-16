namespace VideoGrabber.Infrastructure.Components;

public interface IComponentInstallTransaction
{
    Task CommitAsync(string destination, CancellationToken recoveryDeadline);
    Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline);
}

/// <summary>
/// Task 13 safety gate: preparation may stage verified tools, but live activation
/// is forbidden until Task 17 supplies the journalled snapshot transaction.
/// </summary>
public sealed class DeferredComponentInstallTransaction : IComponentInstallTransaction
{
    public Task CommitAsync(string destination, CancellationToken recoveryDeadline)
    {
        recoveryDeadline.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            "Component activation is unavailable until transactional snapshot activation is installed.");
    }

    public Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline)
    {
        recoveryDeadline.ThrowIfCancellationRequested();
        if (!IsOwnedStage(destination)) return Task.CompletedTask;
        if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
        return Task.CompletedTask;
    }

    private static bool IsOwnedStage(string destination)
    {
        var full = Path.GetFullPath(destination);
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) return false;
        var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return leaf.StartsWith("VideoGrabber-component-stage-", StringComparison.OrdinalIgnoreCase);
    }
}
