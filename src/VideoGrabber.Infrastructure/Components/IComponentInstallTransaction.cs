namespace VideoGrabber.Infrastructure.Components;

public interface IComponentInstallTransaction
{
    Task CommitAsync(string destination, CancellationToken recoveryDeadline);
    Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline);
}
