using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private ulong _browserNavigationId;
    private ComboBox _mediaCandidatesBox = null!;
    private ComboBox _mediaQualityBox = null!;
    private ListView _downloadQueueList = null!;
    private TextBox _browserAddress = null!;
    private readonly HashSet<string> _seenMedia = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mediaQualitySelections = new(StringComparer.Ordinal);
    private readonly BrowserDownloadQueue _browserDownloadQueue = new();
    private readonly BrowserPlayerMasterBindings _playerMasterBindings = new();
    private Uri? _browserPageUri;
    private bool _browserInitializing;

    private void AddBrowserControls(StackPanel panel)
    {
        _browserAddress = new TextBox { Header = "Открытая страница — проверяйте адрес перед входом", IsReadOnly = true };
        _mediaCandidatesBox = new ComboBox { Header = "Найденные видео и потоки", HorizontalAlignment = HorizontalAlignment.Stretch };
        _mediaQualityBox = new ComboBox { Header = "Качество выбранного видео", HorizontalAlignment = HorizontalAlignment.Stretch };
        _mediaCandidatesBox.SelectionChanged += (_, _) =>
        {
            if (_updatingMediaQuality) return;
            SyncMediaQualityChoices();
        };
        _mediaQualityBox.SelectionChanged += (_, _) => StoreSelectedMediaQuality();

        var download = PrimaryButton("Скачать выбранное видео");
        download.Click += async (_, _) =>
        {
            if ((_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is MediaCandidate candidate)
            {
                var ordinal = candidate.PageOrdinal ?? Math.Max(1, _mediaCandidatesBox.SelectedIndex + 1);
                await DownloadCandidateAsync(candidate, ordinal, qualityOverride: SelectedBrowserQuality(candidate));
            }
            else _browserHint.Text = "Выберите найденное видео в списке.";
        };
        var addQueue = SecondaryButton("Добавить в очередь");
        addQueue.Click += (_, _) => QueueSelectedCandidate();
        var downloadAll = SecondaryButton("Скачать все найденные");
        downloadAll.Click += async (_, _) => await DownloadAllVisibleCandidatesAsync();
        var howTo = SecondaryButton("\u24D8 Как скачать?");
        howTo.Click += (_, _) => ShowPage("info");

        panel.Children.Add(_browserAddress);
        panel.Children.Add(TwoColumn(_mediaCandidatesBox, _mediaQualityBox));
        panel.Children.Add(Horizontal(download, addQueue, downloadAll, howTo));
        panel.Children.Add(SectionHeading("Очередь загрузок"));
        _downloadQueueList = new ListView { Height = 150, SelectionMode = ListViewSelectionMode.Single };
        panel.Children.Add(_downloadQueueList);

        var addAll = SecondaryButton("Добавить все");
        addAll.Click += (_, _) => QueueAllVisibleCandidates();
        var up = SecondaryButton("↑ Выше");
        up.Click += (_, _) => MoveQueuedCandidate(-1);
        var down = SecondaryButton("↓ Ниже");
        down.Click += (_, _) => MoveQueuedCandidate(1);
        var remove = SecondaryButton("Удалить");
        remove.Click += (_, _) => RemoveQueuedCandidate();
        var runQueue = PrimaryButton("Скачать очередь / продолжить");
        runQueue.Click += async (_, _) => await DownloadQueuedCandidatesAsync();
        panel.Children.Add(Horizontal(addAll, up, down, remove));
        panel.Children.Add(runQueue);
        panel.Children.Add(MutedText("Для каждого найденного видео можно выбрать своё качество. Успешные пункты удаляются из очереди; оставшиеся можно продолжить позже. Отмена останавливает всю очередь."));
        panel.Children.Add(MutedText("Для закрытого урока войдите на сайте и выберите выше «Встроенный браузер — только эта загрузка». Пароль приложение не читает. DRM не обходится."));
    }

    private async void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_browserInitializing) return;
        if (_operation is not null)
        {
            _browserHint.Text = "Сначала завершите или отмените текущую загрузку.";
            return;
        }
        if (!UrlPolicy.TryValidate(_urlBox.Text, out var uri, out var error) || uri is null)
        {
            SetDownloadState(error, null, true);
            return;
        }
        _browserCard.Visibility = Visibility.Visible;
        _browserInitializing = true;
        BrowserPageLease? initializationLease = null;
        try
        {
            var shouldUseSiteRoutes = DownloadRouteResolver.RequiresProxy(_routes, uri, null);
            if (_mediaBrowser is not null && _browserUsesSiteRoutes != shouldUseSiteRoutes) DestroyBrowser();
            initializationLease = _browserPages.Capture();
            var routeProxy = EnsureRoutingProxy(uri);
            if (_mediaBrowser is null)
            {
                var browser = new WebView2();
                _mediaBrowser = browser;
                _browserHost.Children.Add(browser);
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber", "browser-cache");
                _browserUsesSiteRoutes = routeProxy is not null;
                var environmentOptions = new CoreWebView2EnvironmentOptions();
                if (routeProxy is not null)
                {
                    data = Path.Combine(data, "routed-" + Environment.ProcessId + "-" + routeProxy.Port);
                    environmentOptions.AdditionalBrowserArguments = "--proxy-server=" + routeProxy.ProxyUrl + " --proxy-bypass-list=<-loopback>";
                }
                var lease = initializationLease;
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, data, environmentOptions);
                if (!_browserPages.IsCurrent(lease) || !ReferenceEquals(_mediaBrowser, browser)) return;
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = "VideoGrabber";
                options.IsInPrivateModeEnabled = true;
                await browser.EnsureCoreWebView2Async(environment, options);
                if (!_browserPages.IsCurrent(lease) || !ReferenceEquals(_mediaBrowser, browser)) return;
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
                    if (!ReferenceEquals(_mediaBrowser?.CoreWebView2, core)) { args.Cancel = true; return; }
                    _browserNavigationId = args.NavigationId;
                    _browserPageUri = target;
                    _browserAddress.Text = target.AbsoluteUri;
                    ResetDevToolsDiscoveryForNavigation();
                    _browserHint.Text = "Войдите на сайте при необходимости и нажмите воспроизведение видео.";
                };
                core.NavigationCompleted += async (_, args) =>
                {
                    if (!ReferenceEquals(_mediaBrowser?.CoreWebView2, core) || args.NavigationId != _browserNavigationId) return;
                    DiagnosticHub.Log.Write("browser.navigation", args.IsSuccess ? "succeeded" : "failed",
                        args.IsSuccess ? "Page loaded" : args.WebErrorStatus.ToString());
                    if (!args.IsSuccess)
                    {
                        _browserHint.Text = "Страница не загрузилась: " + args.WebErrorStatus;
                        return;
                    }
                    var completedLease = _browserPages.Capture();
                    if (!IsCurrentBrowserPage(completedLease, core)) return;
                    await RefreshBrowserBindingsAsync(core, completedLease);
                };
                core.WebResourceResponseReceived += Browser_WebResourceResponseReceived;
                await EnableDevToolsMediaDiscoveryAsync(core);
                if (!IsCurrentBrowserPage(lease, core)) return;
                DiagnosticHub.Log.Write("browser.session", "succeeded", "InPrivate enabled; password saving disabled");
            }
            _browserPageUri = uri;
            _mediaBrowser.Source = uri;
        }
        catch (Exception ex)
        {
            if (initializationLease is not null && !_browserPages.IsCurrent(initializationLease)) return;
            DiagnosticHub.Log.Write("browser.session", "failed", ex.Message);
            _browserHint.Text = "Браузер недоступен: " + SensitiveDataRedactor.Redact(ex.Message);
            DestroyBrowser();
        }
        finally { _browserInitializing = false; }
    }

    private async void Browser_WebResourceResponseReceived(CoreWebView2 sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        try
        {
            if (!ReferenceEquals(_mediaBrowser?.CoreWebView2, sender)) return;
            var lease = _browserPages.Capture();
            if (!IsCurrentBrowserPage(lease, sender)) return;
            if (_browserPageUri is null || args.Response.StatusCode < 200 || args.Response.StatusCode >= 300) return;
            var mime = args.Response.Headers.Contains("Content-Type") ? args.Response.Headers.GetHeader("Content-Type") : "";
            var referer = _browserPageUri;
            if (args.Request.Headers.Contains("Referer") && Uri.TryCreate(args.Request.Headers.GetHeader("Referer"), UriKind.Absolute, out var actual)
                && actual.Scheme is "http" or "https") referer = actual;
            var contentLength = 0L;
            if (args.Response.Headers.Contains("Content-Length"))
                long.TryParse(args.Response.Headers.GetHeader("Content-Length"), out contentLength);
            MediaCandidate.TryCreate(args.Request.Uri, mime, referer, out var candidate);
            Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var responseUri);

            if (candidate?.Kind == "GetCourse")
            {
                if (contentLength > 4_000_000) return;
                using var content = await args.Response.GetContentAsync();
                if (!IsCurrentBrowserPage(lease, sender)) return;
                if (content is null) return;
                using var contentStream = content.AsStreamForRead();
                var html = await ReadLimitedTextAsync(contentStream, 4_000_000, lease.Token);
                if (!IsCurrentBrowserPage(lease, sender)) return;
                if (!GetCoursePlayerConfigParser.TryExtractMasterPlaylist(html, candidate.Source, out var playlist) || playlist is null)
                {
                    DiagnosticHub.Log.Write("browser.player", "observed", "GetCourse player " + candidate.Source.IdnHost + " without master playlist");
                    return;
                }
                _playerMasterBindings.Remember(playlist, candidate.Source);
                DiagnosticHub.Log.Write("browser.binding.master", "observed",
                    $"master={BrowserBindingFingerprint.Describe(playlist)} player={BrowserBindingFingerprint.Describe(candidate.Source)}");
                DiagnosticHub.Log.Write("browser.player", "succeeded", "master HLS announced on " + playlist.IdnHost + "; waiting for manifest response");
                return;
            }

            var inspectHlsBody = candidate?.Kind == "HLS"
                || (responseUri is not null && HlsResponseCandidateResolver.ShouldInspectBody(responseUri, mime, contentLength));
            if (inspectHlsBody && responseUri is not null)
            {
                using var content = await args.Response.GetContentAsync();
                if (!IsCurrentBrowserPage(lease, sender)) return;
                if (content is not null)
                {
                    using var contentStream = content.AsStreamForRead();
                    var body = await ReadLimitedTextAsync(contentStream, 4_000_000, lease.Token);
                    if (!IsCurrentBrowserPage(lease, sender)) return;
                    var bindingReferer = _playerMasterBindings.TryResolve(responseUri, out var playerUri) && playerUri is not null
                        ? playerUri : referer;
                    if (playerUri is not null)
                        DiagnosticHub.Log.Write("browser.binding.master", "resolved",
                            $"master={BrowserBindingFingerprint.Describe(responseUri)} player={BrowserBindingFingerprint.Describe(playerUri)}");
                    if (HlsResponseCandidateResolver.TryResolve(body, responseUri, bindingReferer, out var verified, out var blocked))
                    {
                        if (blocked)
                        {
                            _verifiedClearHls.TryRemove(responseUri.AbsoluteUri, out _);
                            RemoveMediaCandidatesReferencing(responseUri, lease, sender);
                            _browserHint.Text = "Обнаружен зашифрованный HLS (EXT-X-KEY). Получение ключей не поддерживается.";
                            DiagnosticHub.Log.Write("browser.hls", "blocked", "HLS " + responseUri.IdnHost + " via WebResourceResponseReceived");
                            return;
                        }
                        if (verified is not null)
                        {
                            HlsDownloadPolicy.UpdateVerifiedClearLeafCache(_verifiedClearHls, responseUri, verified.HlsManifest);
                            QueueMediaCandidate(verified, lease, sender);
                            DiagnosticHub.Log.Write("browser.hls", "succeeded", "HLS " + responseUri.IdnHost + " via WebResourceResponseReceived " + verified.HlsManifest!.SafeSummary);
                            return;
                        }
                    }
                    HlsDownloadPolicy.UpdateVerifiedClearLeafCache(_verifiedClearHls, responseUri, null);
                }
                if (candidate?.Kind == "HLS") return;
                DiagnosticHub.Log.Write("browser.webresource.observe", "event", responseUri.IdnHost + " " + mime.Split(';')[0] + " HTTP=" + args.Response.StatusCode);
            }

            if (candidate is not null) QueueMediaCandidate(candidate, lease, sender);
        }
        catch (Exception ex) { DiagnosticHub.Log.Write("browser.discovery", "failed", ex.GetType().Name); }
    }

    private static async Task<string> ReadLimitedTextAsync(Stream input, int maxChars, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input);
        var buffer = new char[8192];
        var text = new System.Text.StringBuilder(Math.Min(maxChars, 65536));
        while (text.Length <= maxChars)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maxChars + 1 - text.Length)), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0) return text.ToString();
            text.Append(buffer, 0, read);
        }
        throw new InvalidDataException("GetCourse player response is too large.");
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

    private void DestroyBrowser(bool forWindowClose = false)
    {
        try
        {
            DisableDevToolsMediaDiscovery(clearUi: !forWindowClose);
            if (forWindowClose) _browserPages.Dispose();
            if (!forWindowClose) _mediaBrowser?.CoreWebView2?.CookieManager.DeleteAllCookies();
            _mediaBrowser?.Close();
        }
        catch (Exception ex) { DiagnosticHub.Log.Write("browser.close", "failed", ex.GetType().Name); }
        finally
        {
            _mediaBrowser = null;
            if (!forWindowClose) _browserHost.Children.Clear();
            _browserPageUri = null;
            _browserUsesSiteRoutes = false;
            _routePolicy.ClearSession();
        }
    }
}
