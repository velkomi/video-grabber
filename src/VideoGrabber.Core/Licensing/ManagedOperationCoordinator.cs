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
        var permit = await access.AuthorizeAsync(operation, cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await run(cancellationToken).ConfigureAwait(false);
            await access.ReportAsync(permit, reportOutcome(value), CancellationToken.None).ConfigureAwait(false);
            return value;
        }
        catch (OperationCanceledException)
        {
            await access.ReportAsync(permit, "cancel_requested", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await access.ReportAsync(permit, "failed", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
