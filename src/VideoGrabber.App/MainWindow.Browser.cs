using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private ComboBox _mediaCandidatesBox = null!;
    private TextBox _browserAddress = null!;
    private readonly HashSet<string> _seenMedia = new(StringComparer.Ordinal);
    private Uri? _browserPageUri;
    private bool _browserInitializing;

    private void AddBrowserControls(StackPanel panel)
    {
        _browserAddress = new TextBox { Header = "Открытая страница — проверяйте адрес перед входом", IsReadOnly = true };
        _mediaCandidatesBox = new ComboBox { Header = "Найденные видео и потоки", HorizontalAlignment = HorizontalAlignment.Stretch };
        var download = PrimaryButton("Скачать выбранный поток");
        download.Click += async (_, _) =>
        {
            if ((_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is MediaCandidate candidate)
                await DownloadSourceAsync(candidate.Source, candidate.Referer);
            else _browserHint.Text = "Запустите видео на странице и выберите найденный поток в списке.";
        };
        panel.Children.Add(_browserAddress);
        panel.Children.Add(_mediaCandidatesBox);
        panel.Children.Add(download);
        panel.Children.Add(MutedText("Для закрытого урока войдите на сайте и выберите выше «Встроенный браузер — только эта загрузка». Пароль приложение не читает. DRM не обходится."));
    }

    private async void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_browserInitializing) return;
        if (!UrlPolicy.TryValidate(_urlBox.Text, out var uri, out var error) || uri is null)
        {
            SetDownloadState(error, null, true);
            return;
        }
        _browserCard.Visibility = Visibility.Visible;
        _browserInitializing = true;
        try
        {
            if (_mediaBrowser is null)
            {
                var browser = new WebView2();
                _mediaBrowser = browser;
                _browserHost.Children.Add(browser);
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber", "browser-cache");
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, data, new CoreWebView2EnvironmentOptions());
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = "VideoGrabber";
                options.IsInPrivateModeEnabled = true;
                await browser.EnsureCoreWebView2Async(environment, options);
                var core = browser.CoreWebView2;
                if (!core.Profile.IsInPrivateModeEnabled) throw new InvalidOperationException("Изолированный режим браузера не включился.");
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.NewWindowRequested += (sender, args) =>
                {
                    args.Handled = true;
                    if (UrlPolicy.TryValidate(args.Uri, out var target, out _) && target is not null) core.Navigate(target.AbsoluteUri);
                };
                core.NavigationStarting += (sender, args) =>
                {
                    if (!UrlPolicy.TryValidate(args.Uri, out var target, out _) || target is null || !string.IsNullOrEmpty(target.UserInfo))
                    {
                        args.Cancel = true;
                        return;
                    }
                    _browserPageUri = target;
                    _browserAddress.Text = target.AbsoluteUri;
                    _seenMedia.Clear();
                    _mediaCandidatesBox.Items.Clear();
                    _browserHint.Text = "Войдите на сайте при необходимости и нажмите воспроизведение видео.";
                };
                core.NavigationCompleted += (_, args) =>
                {
                    DiagnosticHub.Log.Write("browser.navigation", args.IsSuccess ? "succeeded" : "failed",
                        args.IsSuccess ? "Page loaded" : args.WebErrorStatus.ToString());
                    if (!args.IsSuccess) _browserHint.Text = "Страница не загрузилась: " + args.WebErrorStatus;
                };
                core.WebResourceResponseReceived += Browser_WebResourceResponseReceived;
                DiagnosticHub.Log.Write("browser.session", "succeeded", "InPrivate enabled; password saving disabled");
            }
            _browserPageUri = uri;
            _mediaBrowser.Source = uri;
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("browser.session", "failed", ex.Message);
            _browserHint.Text = "Браузер недоступен: " + SensitiveDataRedactor.Redact(ex.Message);
            DestroyBrowser();
        }
        finally { _browserInitializing = false; }
    }

    private void Browser_WebResourceResponseReceived(CoreWebView2 sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        try
        {
            if (_browserPageUri is null || args.Response.StatusCode is not (200 or 206)) return;
            var mime = args.Response.Headers.Contains("Content-Type") ? args.Response.Headers.GetHeader("Content-Type") : "";
            var referer = _browserPageUri;
            if (args.Request.Headers.Contains("Referer") && Uri.TryCreate(args.Request.Headers.GetHeader("Referer"), UriKind.Absolute, out var actual)
                && actual.Scheme is "http" or "https") referer = actual;
            if (!MediaCandidate.TryCreate(args.Request.Uri, mime, referer, out var candidate) || candidate is null) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_mediaBrowser?.CoreWebView2 != sender || _browserPageUri is null || _seenMedia.Count >= 200 || !_seenMedia.Add(candidate.Source.AbsoluteUri)) return;
                _mediaCandidatesBox.Items.Add(new ComboBoxItem { Content = candidate.DisplayName + "  #" + _seenMedia.Count, Tag = candidate });
                _browserHint.Text = "Найдено: " + _seenMedia.Count + ". Выберите нужный поток; исходная ссылка на урок сохранена.";
                DiagnosticHub.Log.Write("browser.discovery", "succeeded", candidate.DisplayName);
            });
        }
        catch (Exception ex) { DiagnosticHub.Log.Write("browser.discovery", "failed", ex.Message); }
    }

    private void CloseBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_browserInitializing || _operation is not null)
        {
            _browserHint.Text = "Сначала завершите или отмените текущую операцию.";
            return;
        }
        DestroyBrowser();
        _browserCard.Visibility = Visibility.Collapsed;
        _cookiesBox.SelectedIndex = 0;
    }

    private void DestroyBrowser()
    {
        try
        {
            _mediaBrowser?.CoreWebView2?.CookieManager.DeleteAllCookies();
            _mediaBrowser?.Close();
        }
        catch (Exception ex) { DiagnosticHub.Log.Write("browser.close", "failed", ex.Message); }
        finally
        {
            _mediaBrowser = null;
            _browserHost.Children.Clear();
            _seenMedia.Clear();
            _mediaCandidatesBox.Items.Clear();
            _browserPageUri = null;
        }
    }
}
