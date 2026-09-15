using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserOperationResult(OperationOutcome Outcome, OperationCompletion Completion, DownloadResult? Download);

public sealed class BrowserDownloadOperation(IBrowserDownloadPreparation preparation, IVideoDownloader downloader,
    OperationCoordinator coordinator)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _operation;
    public IProgress<DownloadProgress>? Progress { get; init; }
    public void RequestQueue() => coordinator.RequestQueue();
    public void Cancel() { lock (_gate) { coordinator.Cancel(); _operation?.Cancel(); } }
    public void RequestClose() { lock (_gate) { coordinator.RequestClose(); _operation?.Cancel(); } }

    public async Task<BrowserOperationResult> RunAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken windowToken)
    {
        CancellationTokenSource operation;
        lock (_gate)
        {
            if (!coordinator.TryBegin())
                return new(OperationOutcome.Failed, OperationCompletion.None, new(false, "Другая операция уже выполняется."));
            operation = CancellationTokenSource.CreateLinkedTokenSource(lease.Token, windowToken);
            _operation = operation;
        }
        var outcome = OperationOutcome.Failed;
        var completion = OperationCompletion.None;
        DownloadResult? result = null;
        PreparedBrowserDownload? prepared = null;
        try
        {
            EnsureCurrent();
            prepared = await preparation.PrepareAsync(intent, lease, operation.Token);
            EnsureCurrent();
            var request = DownloadRequestFactory.Create(intent, prepared.Values);
            result = await downloader.DownloadAsync(request, new CurrentProgress(this, operation), operation.Token);
            EnsureCurrent();
            outcome = result.Success ? OperationOutcome.Succeeded : OperationOutcome.Failed;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            outcome = OperationOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            result = new(false, "Не удалось завершить загрузку.", Details: VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(ex.Message));
        }
        finally
        {
            try
            {
                if (prepared is not null) await prepared.DisposeAsync();
            }
            catch (Exception)
            {
                outcome = OperationOutcome.Failed;
                result = new(false, "Не удалось освободить ресурсы загрузки.");
            }
            finally
            {
                lock (_gate)
                {
                    _operation = null;
                    operation.Dispose();
                    completion = coordinator.Complete(outcome);
                }
            }
        }
        return new(outcome, completion, result);

        void EnsureCurrent()
        {
            operation.Token.ThrowIfCancellationRequested();
            if (intent.SessionEpoch != lease.Generation)
            {
                operation.Cancel();
                throw new OperationCanceledException("Browser page changed.", operation.Token);
            }
        }
    }

    private sealed class CurrentProgress(BrowserDownloadOperation owner, CancellationTokenSource operation) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            lock (owner._gate)
            {
                if (ReferenceEquals(owner._operation, operation) && !operation.IsCancellationRequested)
                    owner.Progress?.Report(value);
            }
        }
    }
}
