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
        await DownloadSourceAsync(uri);
    }

    private async Task<DownloadAttemptOutcome> DownloadSourceAsync(Uri uri, Uri? referer = null, Uri? hlsVideoSource = null, Uri? hlsAudioSource = null, bool directManifest = false, string? suggestedBaseName = null, bool resetCookieSelectionAfterUse = true, double? expectedDurationSeconds = null, bool? expectedAudio = null)
    {
        if (_operation is not null || _isInstallingComponents)
        {
            SetDownloadState("Другая операция уже выполняется.", "Дождитесь завершения или нажмите «Отменить».", true);
            return DownloadAttemptOutcome.Failed;
        }
        if (string.IsNullOrWhiteSpace(_outputFolderBox.Text))
        {
            SetDownloadState("Выберите папку сохранения.", null, true);
            return DownloadAttemptOutcome.Failed;
        }
        using var operation = new CancellationTokenSource();
        using var job = DiagnosticHub.Begin("ui.download", uri.Host);
        _operation = operation;
        _downloadButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _lastLoggedProgressBucket = -1;
        var quality = (_qualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";
        var selectedCookies = (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var usingEmbeddedSession = BrowserDownloadSessionPolicy.UseEmbeddedSession(selectedCookies);
        var cookies = selectedCookies;
        var audio = _audioOnlyBox.IsChecked == true;
        var outputDirectory = _outputFolderBox.Text;
        var browserOwnsRouteSession = _mediaBrowser?.CoreWebView2 is not null
            && _browserUsesSiteRoutes && _browserPageUri is not null
            && DownloadRouteResolver.WouldConfigureSession(_routes, _browserPageUri, null);
        var downloadOwnsRouteSession = !browserOwnsRouteSession && DownloadRouteResolver.WouldConfigureSession(_routes, uri, referer);
        SetProgress(null);
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
            operation.Token.ThrowIfCancellationRequested();
            if (cookies == "embedded" && _mediaBrowser?.CoreWebView2 is null)
            {
                SetDownloadState("Сначала войдите во встроенном браузере.", "Пароль вводится на странице самого сайта.", true);
                return DownloadAttemptOutcome.Failed;
            }
            using var cookieFile = cookies == "embedded" ? await ExportBrowserSessionAsync(uri, referer, hlsVideoSource, hlsAudioSource) : null;
            var agent = cookies == "embedded" ? _mediaBrowser?.CoreWebView2.Settings.UserAgent : null;
            var browserName = cookies is null or "" or "embedded" ? null : cookies;
            var dispatcher = DispatcherQueue;
            var progress = new DispatchedProgress<DownloadProgress>(
                action => dispatcher.TryEnqueue(() => action()),
                value => { if (ReferenceEquals(_operation, operation)) ApplyDownloadProgress(value); });
            SetDownloadState("Анализирую страницу…", uri.Host);
            var routingProxy = EnsureRoutingProxy(uri, referer);
            var result = await _downloader.DownloadAsync(
                new DownloadRequest(uri, outputDirectory, quality, browserName, audio, cookieFile?.Path, referer, agent, routingProxy?.ProxyUrl, hlsVideoSource, hlsAudioSource, DirectManifest: directManifest, SuggestedBaseName: suggestedBaseName,
                    ExpectedDurationSeconds: expectedDurationSeconds, ExpectedAudio: audio ? true : expectedAudio),
                progress, operation.Token);
            SetDownloadState(result.Message, result.Success ? result.OutputPath : result.Details ?? result.OutputPath, !result.Success);
            SetProgress(result.Success ? 100 : 0);
            if (result.Success && result.OutputPath is not null)
            {
                _localMediaBox.Text = result.OutputPath;
                _localOutputBaseBox.Text = Path.Combine(Path.GetDirectoryName(result.OutputPath)!, Path.GetFileNameWithoutExtension(result.OutputPath) + "-text");
            }
            job.Complete(result.Success);
            return result.Success ? DownloadAttemptOutcome.Succeeded : DownloadAttemptOutcome.Failed;
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
            _operation = null;
            _downloadButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
            if (resetCookieSelectionAfterUse && BrowserDownloadSessionPolicy.ShouldResetAfterUse(selectedCookies)) _cookiesBox.SelectedIndex = 0;
            if (downloadOwnsRouteSession)
            {
                _routePolicy.ClearSession();
                _routeProxy?.Dispose();
                _routeProxy = null;
            }
            TryStartPendingQueue();
            TryCloseAfterOperation();
        }
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

    private async Task<ScopedCookieFile> ExportBrowserSessionAsync(Uri uri, Uri? referer, Uri? hlsVideoSource = null, Uri? hlsAudioSource = null)
    {
        var sources = new[] { uri, referer, _browserPageUri, hlsVideoSource, hlsAudioSource }
            .Where(source => source is not null)
            .Cast<Uri>()
            .Distinct()
            .ToArray();
        var cookies = new List<BrowserCookie>();
        foreach (var source in sources)
        {
            foreach (var cookie in await _mediaBrowser!.CoreWebView2.CookieManager.GetCookiesAsync(source.AbsoluteUri))
                cookies.Add(new(cookie.Domain, cookie.Path, cookie.Name, cookie.Value, cookie.IsSecure, cookie.IsHttpOnly));
        }
        return ScopedCookieFile.Create(sources, cookies.Distinct());
    }
}
