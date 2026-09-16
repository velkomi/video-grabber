using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserOperationResult(OperationOutcome Outcome, OperationCompletion Completion, DownloadResult? Download);

public sealed class BrowserDownloadOperation(IBrowserDownloadPreparation preparation, IVideoDownloader downloader,
    OperationCoordinator coordinator)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _operation;
    private long? _sessionEpoch;
    public IProgress<DownloadProgress>? Progress { get; init; }
    public void RequestQueue() => coordinator.RequestQueue();
    public void Cancel() { lock (_gate) { coordinator.Cancel(); _operation?.Cancel(); } }
    public void RequestClose() { lock (_gate) { coordinator.RequestClose(); _operation?.Cancel(); } }

    public void OnNavigation(long currentSessionEpoch)
    {
        lock (_gate)
        {
            _sessionEpoch = currentSessionEpoch;
            coordinator.Cancel();
            _operation?.Cancel();
        }
    }
    public void OnSessionChanged(long currentSessionEpoch) => OnNavigation(currentSessionEpoch);
    public bool CanContinue(BrowserQueueContext context, long currentSessionEpoch)
    {
        lock (_gate)
            return context.SessionEpoch == currentSessionEpoch && (_sessionEpoch is null || _sessionEpoch == currentSessionEpoch);
    }
    public Task<BrowserOperationResult> RunQueuedAsync(BrowserDownloadQueueItem entry, UserDownloadIntent intent,
        BrowserPageLease lease, long currentSessionEpoch, CancellationToken windowToken)
    {
        lock (_gate)
        {
            if (entry.Context is not { } context || !CanContinue(context, currentSessionEpoch)
                || intent.SessionEpoch != context.SessionEpoch
                || !string.Equals(intent.SelectedSource.AbsoluteUri, entry.Candidate.Source.AbsoluteUri, StringComparison.Ordinal)
                || !string.Equals(intent.CookieSelection, context.CookieSelection, StringComparison.Ordinal)
                || !string.Equals(intent.Quality, entry.Quality, StringComparison.Ordinal))
                return Task.FromResult(new BrowserOperationResult(OperationOutcome.Failed, OperationCompletion.None,
                    new(false, "Требуется повторный выбор сессии", Details: "Пункт сохранён. Выберите сессию и явно добавьте видео в очередь заново.")));
            return RunAsync(intent, lease, windowToken, currentSessionEpoch);
        }
    }

    public Task<BrowserOperationResult> RunAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken windowToken)
        => RunAsync(intent, lease, windowToken, lease.Generation);

    private async Task<BrowserOperationResult> RunAsync(UserDownloadIntent intent, BrowserPageLease lease,
        CancellationToken windowToken, long expectedIntentEpoch)
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
            // Direct intents use the page generation; queued intents retain their session identity.
            // Both are cancelled by the live page lease and the explicit lifecycle callbacks.
            if (intent.SessionEpoch != expectedIntentEpoch)
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
