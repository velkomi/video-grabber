namespace VideoGrabber.Core.Licensing;

public sealed class ManagedOperationCoordinator(IManagedAccessClient access)
{
    public Task<T> RunAsync<T>(
        ManagedOperation operation,
        Func<CancellationToken, Task<T>> run,
        CancellationToken cancellationToken)
        => RunAsync(operation, run, _ => "completed", cancellationToken);

    public async Task<T> RunAsync<T>(
        ManagedOperation operation,
        Func<CancellationToken, Task<T>> run,
        Func<T, string> reportOutcome,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var permit = await access.AuthorizeAsync(operation, cancellationToken);

        T value;
        try
        {
            value = await run(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await access.ReportAsync(permit, "cancel_requested", CancellationToken.None);
            throw;
        }
        catch
        {
            await access.ReportAsync(permit, "failed", CancellationToken.None);
            throw;
        }

        // Keep finalization outside the operation catch block. If a successful
        // local download cannot be committed server-side, never reinterpret it
        // as a failed operation and release its already-delivered credit.
        await access.ReportAsync(
            permit,
            reportOutcome(value),
            CancellationToken.None);
        return value;
    }
}
