namespace VideoGrabber.Core.Licensing;

public interface IManagedAccessClient
{
    Task<OperationPermit> AuthorizeAsync(
        ManagedOperation operation,
        CancellationToken cancellationToken);

    Task ReportAsync(
        OperationPermit permit,
        string outcome,
        CancellationToken cancellationToken);
}
