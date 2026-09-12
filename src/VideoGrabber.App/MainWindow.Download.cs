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

    private async Task DownloadSourceAsync(Uri uri, Uri? referer = null)
    {
        if (_operation is not null || _isInstallingComponents)
        {
            SetDownloadState("Другая операция уже выполняется.", "Дождитесь завершения или нажмите «Отменить».", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputFolderBox.Text))
        {
            SetDownloadState("Выберите папку сохранения.", null, true);
            return;
        }
        using var operation = new CancellationTokenSource();
        using var job = DiagnosticHub.Begin("ui.download", uri.Host);
        _operation = operation;
        _downloadButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _lastLoggedProgressBucket = -1;
        var quality = (_qualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";
        var cookies = (_cookiesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var audio = _audioOnlyBox.IsChecked == true;
        var outputDirectory = _outputFolderBox.Text;
        SetProgress(null);
        try
        {
            if (!RequiredComponentsAvailable())
            {
                SetDownloadState("Подготавливаю компоненты…", "Проверяются локальные инструменты.");
                if (!await InstallComponentsAsync(forceUpdate: false))
                {
                    SetDownloadState("Компоненты не установлены.", "Откройте раздел «Компоненты».", true);
                    return;
                }
            }
            operation.Token.ThrowIfCancellationRequested();
            if (cookies == "embedded" && _mediaBrowser?.CoreWebView2 is null)
            {
                SetDownloadState("Сначала войдите во встроенном браузере.", "Пароль вводится на странице самого сайта.", true);
                return;
            }
            using var cookieFile = cookies == "embedded" ? await ExportBrowserSessionAsync(uri, referer) : null;
            var agent = cookies == "embedded" ? _mediaBrowser?.CoreWebView2.Settings.UserAgent : null;
            var browserName = cookies is null or "" or "embedded" ? null : cookies;
            var dispatcher = DispatcherQueue;
            var progress = new DispatchedProgress<DownloadProgress>(
                action => dispatcher.TryEnqueue(() => action()),
                value => { if (ReferenceEquals(_operation, operation)) ApplyDownloadProgress(value); });
            SetDownloadState("Анализирую страницу…", uri.Host);
            var result = await _downloader.DownloadAsync(
                new DownloadRequest(uri, outputDirectory, quality, browserName, audio, cookieFile?.Path, referer, agent),
                progress, operation.Token);
            SetDownloadState(result.Message, result.Success ? result.OutputPath : result.Details ?? result.OutputPath, !result.Success);
            SetProgress(result.Success ? 100 : 0);
            if (result.Success && result.OutputPath is not null)
            {
                _localMediaBox.Text = result.OutputPath;
                _localOutputBaseBox.Text = Path.Combine(Path.GetDirectoryName(result.OutputPath)!, Path.GetFileNameWithoutExtension(result.OutputPath) + "-text");
            }
            job.Complete(result.Success);
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
            SetDownloadState("Загрузка отменена.", "Файлы .part можно использовать для докачки.");
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("ui.download", "failed", ex.Message, jobId: job.Id);
            SetDownloadState("Не удалось завершить загрузку.", SensitiveDataRedactor.Redact(ex.Message), true);
        }
        finally
        {
            _operation = null;
            _downloadButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
            if (cookies == "embedded") _cookiesBox.SelectedIndex = 0;
        }
    }

    private async Task<ScopedCookieFile> ExportBrowserSessionAsync(Uri uri, Uri? referer)
    {
        var sources = referer is null ? new[] { uri } : new[] { uri, referer };
        var cookies = new List<BrowserCookie>();
        foreach (var source in sources.Distinct())
        {
            foreach (var cookie in await _mediaBrowser!.CoreWebView2.CookieManager.GetCookiesAsync(source.AbsoluteUri))
                cookies.Add(new(cookie.Domain, cookie.Path, cookie.Name, cookie.Value, cookie.IsSecure, cookie.IsHttpOnly));
        }
        return ScopedCookieFile.Create(sources, cookies.Distinct());
    }
}
