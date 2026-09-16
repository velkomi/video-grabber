using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private readonly OperationCoordinator _operations = new();
    private readonly CancellationTokenSource _windowLifetime = new();
    private BrowserDownloadOperation? _browserOperation;
    private object? _progressOwner;

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!UrlPolicy.TryValidate(_urlBox.Text, out var uri, out var error) || uri is null)
        {
            SetDownloadState(error, null, true);
            DiagnosticHub.Log.Write("input", "failed", "invalid URL");
            return;
        }
        var intent = CaptureDownloadIntent(uri);
        await DownloadSourceAsync(intent);
    }

    private UserDownloadIntent CaptureDownloadIntent(Uri source, string? qualityOverride = null)
        => new(source, qualityOverride ?? (_qualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best",
            _audioOnlyBox.IsChecked == true, _outputFolderBox.Text,
            (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), Volatile.Read(ref _browserDiscoveryGeneration));

    private Task<OperationOutcome> DownloadSourceAsync(UserDownloadIntent intent)
        => RunDownloadOperationAsync(intent, new BrowserDownloadPreparation(this));

    private async Task<OperationOutcome> RunDownloadOperationAsync(UserDownloadIntent intent,
        IBrowserDownloadPreparation preparation, bool resetCookieSelectionAfterUse = true, BrowserDownloadQueueItem? queuedEntry = null)
    {
        if (_operations.IsBusy || _isInstallingComponents)
        {
            SetDownloadState("Другая операция уже выполняется.", "Дождитесь завершения или нажмите «Отменить».", true);
            return OperationOutcome.Failed;
        }
        if (string.IsNullOrWhiteSpace(intent.OutputDirectory))
        {
            SetDownloadState("Выберите папку сохранения.", null, true);
            return OperationOutcome.Failed;
        }
        using var job = DiagnosticHub.Begin("ui.download", intent.SelectedSource.Host);
        var owner = new object();
        _progressOwner = owner;
        var dispatcher = DispatcherQueue;
        var lease = _browserPages.Capture();
        var navigationVersion = _queueNavigationVersion;
        var service = new BrowserDownloadOperation(preparation, _downloader, _operations)
        {
            Progress = new DispatchedProgress<DownloadProgress>(
                action => dispatcher.TryEnqueue(() => action()),
                value => { if (ReferenceEquals(_progressOwner, owner) && !lease.Token.IsCancellationRequested && !_windowLifetime.IsCancellationRequested) ApplyDownloadProgress(value); })
        };
        _browserOperation = service;
        SetOperationControls(true);
        _lastLoggedProgressBucket = -1;
        SetProgress(null);
        SetDownloadState("Анализирую страницу…", intent.SelectedSource.Host);
        var completion = OperationCompletion.None;
        try
        {
            // No await occurs between capture and the production service's ownership claim.
            var result = queuedEntry is null
                ? await service.RunAsync(intent, lease, _windowLifetime.Token)
                : await service.RunQueuedAsync(queuedEntry, intent, lease, _browserSessionEpoch, _windowLifetime.Token);
            completion = result.Completion == OperationCompletion.StartQueue && navigationVersion != _queueNavigationVersion
                ? OperationCompletion.None : result.Completion;
            job.Complete(result.Outcome == OperationOutcome.Succeeded);
            if (result.Outcome == OperationOutcome.Cancelled)
            {
                job.Cancel();
                SetDownloadState("Загрузка отменена.", "Очередь остановлена. Невыполненные пункты сохранены.");
            }
            else if (result.Download is { } download)
            {
                SetDownloadState(download.Message, download.Success ? download.OutputPath : download.Details ?? download.OutputPath, !download.Success);
                SetProgress(download.Success ? 100 : 0);
                if (download.Success && download.OutputPath is { } path)
                {
                    _localMediaBox.Text = path;
                    _localOutputBaseBox.Text = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-text");
                }
            }
            return result.Outcome;
        }
        finally
        {
            _browserOperation = null;
            _progressOwner = null;
            if (resetCookieSelectionAfterUse && BrowserDownloadSessionPolicy.ShouldResetAfterUse(intent.CookieSelection))
                _browserSession.RunProgrammaticSelectionCleanup(() => _cookiesBox.SelectedIndex = 0);
            CompleteOperation(completion);
        }
    }

    private void CancelOperation()
    {
        _progressOwner = null;
        _operations.Cancel();
        _browserOperation?.Cancel();
        _operation?.Cancel();
    }

    private void SetOperationControls(bool busy)
    {
        _downloadButton.IsEnabled = _mp3Button.IsEnabled = _textButton.IsEnabled = !busy;
        _cancelButton.IsEnabled = busy;
    }

    // The service or local operation has already computed completion exactly once.
    private void CompleteOperation(OperationCompletion completion)
    {
        _operation = null;
        SetOperationControls(false);
        switch (completion)
        {
            case OperationCompletion.CloseWindow:
                _allowWindowClose = true;
                DispatcherQueue.TryEnqueue(Close);
                break;
            case OperationCompletion.StartQueue:
                var navigationVersion = _queueNavigationVersion;
                if (!_queueRunnerActive) DispatcherQueue.TryEnqueue(async () =>
                {
                    if (navigationVersion == _queueNavigationVersion) await DownloadQueuedCandidatesAsync();
                });
                break;
        }
    }

    private void EnsureIntentSession(UserDownloadIntent intent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (intent.SessionEpoch != Volatile.Read(ref _browserDiscoveryGeneration))
            throw new OperationCanceledException("Browser page changed.", cancellationToken);
    }
}
