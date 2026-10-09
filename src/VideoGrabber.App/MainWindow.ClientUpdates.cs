using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private static readonly ClientServiceEndpoints BootstrapClientServices = new(
        new Uri("https://videograbber.srv1902378.hstgr.cloud/"),
        new Uri("https://videograbber.srv1902378.hstgr.cloud/web/"));
    private ClientServiceEndpoints _activeClientServices = BootstrapClientServices;
    private ClientServiceSelection _clientServiceSelection = null!;
    private bool _clientDirectoryBlocked;
    private ClientReleaseVerifier _clientReleaseVerifier = null!;
    private ClientReleaseCache _clientReleaseCache = null!;
    private readonly SemaphoreSlim _clientCheckGate = new(1, 1);
    private DispatcherTimer? _clientUpdateTimer = null;
    private Border _clientUpdateNotice = null!;
    private TextBlock _clientNoticeTitle = null!;
    private TextBlock _clientNoticeText = null!;
    private StackPanel _clientNoticeActions = null!;
    private TextBlock _clientUpdateStatus = null!;
    private Button _clientCheckButton = null!;
    private Button _clientDownloadButton = null!;
    private Button _clientRollbackButton = null!;
    private Button _clientOpenUpdateFolder = null!;
    private bool _clientDownloadBusy;
    private bool _clientMigrationAwaitingRestart;
    private long _clientDismissedSequence;
    private string? _clientPreparedInstaller;

    private void InitializeClientUpdateServices()
    {
        _clientReleaseVerifier = new ClientReleaseVerifier(ClientReleaseTrust.PublicKeys);
        _clientReleaseCache = new ClientReleaseCache(Path.Combine(AppDataRoot, "updates", "catalog-cache.json"),
            _clientReleaseVerifier, BootstrapClientServices);
        var cache = _clientReleaseCache.Read();
        _clientServiceSelection = ClientServiceDirectory.Select(cache, BootstrapClientServices,
            Environment.GetEnvironmentVariable("VIDEOGRABBER_PLATFORM_URL"));
        _clientDirectoryBlocked = _clientServiceSelection.BlockOnline;
        _activeClientServices = _clientServiceSelection.Services;
    }

    private FrameworkElement BuildClientUpdateNotice()
    {
        var body = Vertical(8);
        _clientNoticeTitle = SectionHeading("Обновление VideoGrabber");
        _clientNoticeText = MutedText("");
        _clientNoticeActions = Vertical(6);
        body.Children.Add(_clientNoticeTitle);
        body.Children.Add(_clientNoticeText);
        body.Children.Add(_clientNoticeActions);
        _clientUpdateNotice = new Border
        {
            Padding = new Thickness(18, 12, 18, 12), Background = CardBrush,
            BorderBrush = CardBorderBrush, BorderThickness = new Thickness(0, 0, 0, 1),
            Child = body, Visibility = Visibility.Collapsed
        };
        return _clientUpdateNotice;
    }

    private FrameworkElement BuildClientUpdateCard()
    {
        var body = Vertical(10);
        body.Children.Add(SectionHeading("Обновления"));
        body.Children.Add(MutedText("Установлена версия " + CurrentVersion()));
        _clientUpdateStatus = MutedText(_clientDirectoryBlocked
            ? "Проверьте обновления, чтобы восстановить подключение."
            : "Обновления проверяются при запуске. Установка — по вашему выбору.");
        body.Children.Add(_clientUpdateStatus);
        _clientCheckButton = SecondaryButton("Проверить обновления");
        _clientCheckButton.Click += async (_, _) => await CheckClientReleaseAsync(manual: true);
        body.Children.Add(_clientCheckButton);
        _clientDownloadButton = PrimaryButton("Обновить");
        _clientDownloadButton.Visibility = Visibility.Collapsed;
        _clientDownloadButton.Click += async (_, _) => await DownloadClientInstallerAsync(rollback: false);
        body.Children.Add(_clientDownloadButton);
        _clientRollbackButton = SecondaryButton("Скачать предыдущую версию");
        _clientRollbackButton.Visibility = Visibility.Collapsed;
        _clientRollbackButton.Click += async (_, _) => await DownloadClientInstallerAsync(rollback: true);
        body.Children.Add(_clientRollbackButton);
        _clientOpenUpdateFolder = SecondaryButton("Открыть папку с установщиком");
        _clientOpenUpdateFolder.Visibility = Visibility.Collapsed;
        _clientOpenUpdateFolder.Click += (_, _) => OpenPreparedUpdateFolder();
        body.Children.Add(_clientOpenUpdateFolder);
        return Card(body);
    }

    private void StartClientUpdateChecks()
    {
#if !VIDEOGRABBER_PRESENTATION_PROBE
        _rootHost.Loaded += async (_, _) => await CheckClientReleaseAsync(manual: false);
        _clientUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
        _clientUpdateTimer.Tick += async (_, _) => await CheckClientReleaseAsync(manual: false);
        _clientUpdateTimer.Start();
#endif
    }

    private async Task CheckClientReleaseAsync(bool manual)
    {
        if (!await _clientCheckGate.WaitAsync(0, _windowLifetime.Token)) return;
        try
        {
            _clientCheckButton.IsEnabled = false;
            if (manual) _clientUpdateStatus.Text = "Проверяю обновления…";
            using var client = new ClientReleaseClient();
            var cache = _clientReleaseCache.Read();
            var sources = new List<Uri>();
            if (cache.Latest is { } latest) sources.Add(new Uri(latest.Services.ApiBase, "v1/client-release/preview"));
            sources.Add(new Uri(BootstrapClientServices.ApiBase, "v1/client-release/preview"));
            sources.Add(new Uri("https://raw.githubusercontent.com/velkomi/video-grabber/main/releases/windows/preview.json"));
            var accepted = false;
            foreach (var source in sources.Distinct())
            {
                try
                {
                    var envelope = await client.FetchAsync(source, _windowLifetime.Token);
                    _ = _clientReleaseCache.AcceptChecked(envelope);
                    accepted = true;
                    break;
                }
                catch (OperationCanceledException) when (_windowLifetime.IsCancellationRequested) { throw; }
                catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or ClientReleaseRejectedException)
                { AppDiagnostics.Write("Client update check unavailable: " + e.GetType().Name); }
            }
            if (accepted)
            {
                var recovered = _clientReleaseCache.Read();
                if (_clientDirectoryBlocked && !recovered.ActiveBlocked && recovered.Active is { } active &&
                    ClientReleaseCache.SameServices(active.Services, _activeClientServices))
                {
                    _clientDirectoryBlocked = false;
#if VIDEOGRABBER_MANAGED && !VIDEOGRABBER_PRESENTATION_PROBE
                    if (string.IsNullOrWhiteSpace(_managedAccessToken)) await InitializeManagedAccountAsync();
#endif
                }
                RenderClientReleaseState(manual);
            }
            else if (manual) _clientUpdateStatus.Text = "Не удалось проверить обновления. Проверьте интернет и попробуйте ещё раз.";
        }
        catch (OperationCanceledException) { }
        finally { _clientCheckButton.IsEnabled = true; _clientCheckGate.Release(); }
    }

    private void RenderClientReleaseState(bool manual)
    {
        var cache = _clientReleaseCache.Read();
        var latest = cache.Latest;
        if (latest is null || latest.ExpiresAt <= DateTimeOffset.UtcNow) return;
        var decision = ClientUpdateDecision.Evaluate(CurrentVersion(), _activeClientServices, latest, DateTimeOffset.UtcNow);
        _clientUpdateStatus.Text = decision.UpdateAvailable
            ? "Доступна версия " + latest.Release.Version + ". " + latest.Release.Notes
            : "Установлена актуальная версия. " + latest.Release.Notes;
        _clientDownloadButton.Visibility = decision.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
        _clientRollbackButton.Visibility = latest.Release.RollbackInstaller is not null ? Visibility.Visible : Visibility.Collapsed;
        if (!manual && cache.HighWater == _clientDismissedSequence) return;
        _clientNoticeActions.Children.Clear();
        if (decision.ServicesChanged || cache.ActiveBlocked || _clientMigrationAwaitingRestart)
        {
            _clientNoticeTitle.Text = _clientMigrationAwaitingRestart ? "Новый адрес сохранён" : "VideoGrabber переехал на новый сайт";
            _clientNoticeText.Text = _clientMigrationAwaitingRestart
                ? "Перезапустите приложение после завершения загрузок. Ваш аккаунт и настройки сохраняются."
                : "Ваш аккаунт сохранён. Новый сайт: " + latest.Services.WebsiteBase.GetLeftPart(UriPartial.Authority);
            if (!_clientMigrationAwaitingRestart)
            {
                var apply = PrimaryButton("Применить после перезапуска");
                apply.Click += (_, _) =>
                {
                    try { _clientReleaseCache.ApproveLatestServices(); _clientMigrationAwaitingRestart = true; RenderClientReleaseState(manual: true); }
                    catch (Exception e) when (e is ClientReleaseRejectedException or IOException)
                    { _clientUpdateStatus.Text = "Не удалось сохранить новый адрес. Проверьте обновления ещё раз."; }
                };
                _clientNoticeActions.Children.Add(apply);
            }
            _clientUpdateNotice.Visibility = Visibility.Visible;
        }
        else if (decision.UpdateAvailable)
        {
            _clientNoticeTitle.Text = "Доступна новая версия VideoGrabber";
            _clientNoticeText.Text = latest.Release.Version + ". " + NoticeExcerpt(latest.Release.Notes);
            var download = PrimaryButton("Обновить");
            download.IsEnabled = !_clientDownloadBusy;
            download.Click += async (_, _) => await DownloadClientInstallerAsync(rollback: false);
            _clientNoticeActions.Children.Add(download);
            _clientUpdateNotice.Visibility = Visibility.Visible;
        }
        else { _clientUpdateNotice.Visibility = Visibility.Collapsed; return; }
        var later = SecondaryButton("Позже");
        later.Click += (_, _) => { _clientDismissedSequence = cache.HighWater; _clientUpdateNotice.Visibility = Visibility.Collapsed; };
        _clientNoticeActions.Children.Add(later);
    }

    private async Task DownloadClientInstallerAsync(bool rollback)
    {
        if (_clientDownloadBusy) return;
        var cache = _clientReleaseCache.Read();
        if (cache.LatestEnvelope is null) return;
        if (!rollback && !ClientUpdateDecision.Evaluate(CurrentVersion(), _activeClientServices, cache.Latest!, DateTimeOffset.UtcNow).UpdateAvailable) return;
        _clientDownloadBusy = true;
        _clientDownloadButton.IsEnabled = false;
        _clientRollbackButton.IsEnabled = false;
        _clientPreparedInstaller = null;
        _clientOpenUpdateFolder.Visibility = Visibility.Collapsed;
        RenderClientReleaseState(manual: true);
        try
        {
            using var downloader = new VerifiedInstallerDownloader(Path.Combine(AppDataRoot, "updates", "packages"), _clientReleaseVerifier);
            var progress = new Progress<double>(fraction => _clientUpdateStatus.Text = "Скачиваю обновление: " + Math.Round(fraction * 100) + "%");
            _clientPreparedInstaller = await downloader.DownloadAsync(cache.LatestEnvelope, rollback, progress, _windowLifetime.Token);
            _clientUpdateStatus.Text = "Установщик готов и проверен. Завершите загрузки, закройте VideoGrabber и запустите установщик из папки. Настройки и аккаунт сохранятся.";
            _clientOpenUpdateFolder.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { if (!_windowLifetime.IsCancellationRequested) _clientUpdateStatus.Text = "Скачивание прервано. Попробуйте ещё раз."; }
        catch (Exception e) when (e is IOException or HttpRequestException or ClientReleaseRejectedException or UnauthorizedAccessException)
        { _clientUpdateStatus.Text = "Не удалось получить проверенный установщик. Проверьте интернет и попробуйте ещё раз."; AppDiagnostics.Write("Client installer unavailable: " + e.GetType().Name); }
        finally { _clientDownloadBusy = false; _clientDownloadButton.IsEnabled = true; _clientRollbackButton.IsEnabled = true; }
    }

    private void OpenPreparedUpdateFolder()
    {
        if (_clientPreparedInstaller is null || !File.Exists(_clientPreparedInstaller)) return;
        try { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_clientPreparedInstaller)!) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        { _clientUpdateStatus.Text = "Не удалось открыть папку. Установщик сохранён в папке обновлений VideoGrabber."; }
    }

    private static string NoticeExcerpt(string text) => text.Length <= 240 ? text : text[..240] + "…";

}
