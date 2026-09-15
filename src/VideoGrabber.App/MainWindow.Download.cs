using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private enum DownloadAttemptOutcome { Succeeded, Failed, Cancelled }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!UrlPolicy.TryValidate(_urlBox.Text, out var uri, out var error) || uri is null)
        {
            SetDownloadState(error, null, true);
            DiagnosticHub.Log.Write("input", "failed", "invalid URL");
            return;
        }
        var intent = CaptureDownloadIntent(uri);
        await DownloadSourceAsync(intent, CancellationToken.None);
    }

    private UserDownloadIntent CaptureDownloadIntent(Uri source, string? qualityOverride = null)
        => new(source, qualityOverride ?? (_qualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best",
            _audioOnlyBox.IsChecked == true, _outputFolderBox.Text,
            (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), Volatile.Read(ref _browserDiscoveryGeneration));

    private Task<DownloadAttemptOutcome> DownloadSourceAsync(UserDownloadIntent intent, CancellationToken operationToken)
        => RunDownloadOperationAsync(intent,
            (token, routeScope) => DownloadPreparedSourceAsync(intent,
                new(intent.SelectedSource, null, null, null, null, null, null, false, false, null, null, null), token, routeScope),
            operationToken: operationToken);

    private async Task<DownloadAttemptOutcome> RunDownloadOperationAsync(UserDownloadIntent intent,
        Func<CancellationToken, DownloadRouteScope, Task<DownloadAttemptOutcome>> download,
        bool resetCookieSelectionAfterUse = true, CancellationToken operationToken = default)
    {
        if (_operation is not null || _isInstallingComponents)
        {
            SetDownloadState("Другая операция уже выполняется.", "Дождитесь завершения или нажмите «Отменить».", true);
            return DownloadAttemptOutcome.Failed;
        }
        if (string.IsNullOrWhiteSpace(intent.OutputDirectory))
        {
            SetDownloadState("Выберите папку сохранения.", null, true);
            return DownloadAttemptOutcome.Failed;
        }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
        using var job = DiagnosticHub.Begin("ui.download", intent.SelectedSource.Host);
        _operation = operation;
        _downloadButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _lastLoggedProgressBucket = -1;
        var selectedCookies = intent.CookieSelection;
        var routeScope = new DownloadRouteScope(_routePolicy, _browserUsesSiteRoutes ? _routeProxy : null);
        SetProgress(null);
        try
        {
            EnsureIntentSession(intent, operation.Token);
            var outcome = await download(operation.Token, routeScope);
            job.Complete(outcome == DownloadAttemptOutcome.Succeeded);
            return outcome;
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
            SetDownloadState("Загрузка отменена.", "Пакетная очередь также остановлена. Частичная загрузка сохранена во временной job-папке.");
            return DownloadAttemptOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("ui.download", "failed", ex.Message, jobId: job.Id);
            SetDownloadState("Не удалось завершить загрузку.", SensitiveDataRedactor.Redact(ex.Message), true);
            return DownloadAttemptOutcome.Failed;
        }
        finally
        {
            routeScope.Dispose();
            _operation = null;
            _downloadButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
            if (resetCookieSelectionAfterUse && BrowserDownloadSessionPolicy.ShouldResetAfterUse(selectedCookies)) _cookiesBox.SelectedIndex = 0;
            TryStartPendingQueue();
            TryCloseAfterOperation();
        }
    }

    private void EnsureIntentSession(UserDownloadIntent intent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (intent.SessionEpoch != Volatile.Read(ref _browserDiscoveryGeneration))
            throw new OperationCanceledException("Browser page changed.", cancellationToken);
    }

    private async Task<DownloadAttemptOutcome> DownloadPreparedSourceAsync(
        UserDownloadIntent intent, PreparedDownload selected, CancellationToken operationToken, DownloadRouteScope routeScope)
    {
        var operation = _operation;
        ScopedCookieFile? cookieFile = null;
        try
        {
            if (!RequiredComponentsAvailable())
            {
                SetDownloadState("Подготавливаю компоненты…", "Проверяются локальные инструменты.");
                if (!await InstallComponentsAsync(forceUpdate: false))
                {
                    SetDownloadState("Компоненты не установлены.", "Откройте раздел «Компоненты».", true);
                    return DownloadAttemptOutcome.Failed;
                }
            }
            EnsureIntentSession(intent, operationToken);
            var selectedCookies = intent.CookieSelection;
            var usingEmbeddedSession = BrowserDownloadSessionPolicy.UseEmbeddedSession(selectedCookies);
            if (usingEmbeddedSession && _mediaBrowser?.CoreWebView2 is null)
            {
                SetDownloadState("Сначала войдите во встроенном браузере.", "Пароль вводится на странице самого сайта.", true);
                return DownloadAttemptOutcome.Failed;
            }
            var request = await DownloadRequestFactory.PrepareAsync(intent, async (captured, token) =>
            {
                EnsureIntentSession(captured, token);
                cookieFile = usingEmbeddedSession ? await ExportBrowserSessionAsync(captured, selected, token) : null;
                EnsureIntentSession(captured, token);
                var agent = usingEmbeddedSession ? _mediaBrowser?.CoreWebView2.Settings.UserAgent : null;
                var routingProxy = EnsureRoutingProxy(selected.Source, selected.Referer, routeScope);
                return selected with { CookiesFile = cookieFile?.Path, UserAgent = agent, LocalProxy = routingProxy?.ProxyUrl };
            }, operationToken);
            EnsureIntentSession(intent, operationToken);
            var dispatcher = DispatcherQueue;
            var progress = new DispatchedProgress<DownloadProgress>(
                action => dispatcher.TryEnqueue(() => action()),
                value => { if (ReferenceEquals(_operation, operation)) ApplyDownloadProgress(value); });
            SetDownloadState("Анализирую страницу…", request.Source.Host);
            var result = await _downloader.DownloadAsync(request, progress, operationToken);
            SetDownloadState(result.Message, result.Success ? result.OutputPath : result.Details ?? result.OutputPath, !result.Success);
            SetProgress(result.Success ? 100 : 0);
            if (result.Success && result.OutputPath is not null)
            {
                _localMediaBox.Text = result.OutputPath;
                _localOutputBaseBox.Text = Path.Combine(Path.GetDirectoryName(result.OutputPath)!, Path.GetFileNameWithoutExtension(result.OutputPath) + "-text");
            }
            return result.Success ? DownloadAttemptOutcome.Succeeded : DownloadAttemptOutcome.Failed;
        }
        finally { cookieFile?.Dispose(); }
    }

    private void TryStartPendingQueue()
    {
        if (!_queueRunRequested || _queueRunnerActive || _operation is not null || _closeRequested) return;
        _queueRunRequested = false;
        DispatcherQueue.TryEnqueue(async () => await DownloadQueuedCandidatesAsync());
    }

    private void TryCloseAfterOperation()
    {
        if (!_closeRequested || _operation is not null) return;
        _closeRequested = false;
        _allowWindowClose = true;
        DispatcherQueue.TryEnqueue(Close);
    }

    private async Task<ScopedCookieFile> ExportBrowserSessionAsync(
        UserDownloadIntent intent, PreparedDownload selected, CancellationToken cancellationToken)
    {
        EnsureIntentSession(intent, cancellationToken);
        var browser = _mediaBrowser!.CoreWebView2;
        var sources = new[] { selected.Source, selected.Referer, _browserPageUri, selected.HlsVideoSource, selected.HlsAudioSource }
            .Where(source => source is not null)
            .Cast<Uri>()
            .Distinct()
            .ToArray();
        var cookies = new List<BrowserCookie>();
        foreach (var source in sources)
        {
            EnsureIntentSession(intent, cancellationToken);
            var scopedCookies = await browser.CookieManager.GetCookiesAsync(source.AbsoluteUri);
            EnsureIntentSession(intent, cancellationToken);
            if (!ReferenceEquals(browser, _mediaBrowser?.CoreWebView2))
                throw new OperationCanceledException("Browser instance changed.", cancellationToken);
            foreach (var cookie in scopedCookies)
                cookies.Add(new(cookie.Domain, cookie.Path, cookie.Name, cookie.Value, cookie.IsSecure, cookie.IsHttpOnly));
        }
        return ScopedCookieFile.Create(sources, cookies.Distinct());
    }
}
