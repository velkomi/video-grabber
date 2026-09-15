using System.Net;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;
namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private SiteRouteSettings _routes = new([]);
    private readonly SiteRoutePolicy _routePolicy = new(new SiteRouteSettings([]));
    private SiteRouteProxy? _routeProxy;
    private string? _routeReadError;
    private TextBox _routeHost = null!;
    private ComboBox _routeAdapter = null!;
    private ComboBox _routeList = null!;
    private bool _browserUsesSiteRoutes;
    private TextBlock _routeStatus = null!;
    private bool _routeProbeRunning;
    private static string RoutesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber", "site-routes.json");
    private Border BuildNetworkCard()
    {
        var panel = Vertical(12);
        panel.Children.Add(SectionHeading("Подключение для отдельных сайтов"));
        panel.Children.Add(MutedText("Выбранный сайт будет выходить через указанный адаптер, например Ethernet без VPN. Сайт увидит IP этого подключения. Остальные сайты — по маршруту Windows. Настройки VPN и других программ не меняются."));
        _routeHost = new TextBox { Header = "Домен или ссылка для правила", PlaceholderText = "iglyrazuma.ru" };
        AttachPasteContextMenu(_routeHost);
        _routeAdapter = new ComboBox { Header = "Подключение для этого сайта", HorizontalAlignment = HorizontalAlignment.Stretch };
        _routeList = new ComboBox { Header = "Сохранённые правила", HorizontalAlignment = HorizontalAlignment.Stretch };
        _routeStatus = MutedText("");
        var save = PrimaryButton("Сохранить правило");
        var remove = SecondaryButton("Удалить выбранное правило");
        var refresh = SecondaryButton("Обновить адаптеры");
        var check = SecondaryButton("Проверить подключение к сайту");
        save.Click += (_, _) => SaveSiteRule();
        remove.Click += (_, _) => RemoveSiteRule();
        refresh.Click += (_, _) => RefreshRouteAdapters();
        check.Click += async (_, _) => await CheckSiteRouteAsync();
        panel.Children.Add(_routeHost); panel.Children.Add(_routeAdapter);
        panel.Children.Add(Horizontal(save, refresh));
        panel.Children.Add(_routeList); panel.Children.Add(remove); panel.Children.Add(check);
        panel.Children.Add(_routeStatus);
        panel.Children.Add(MutedText("Правила сохраняются автоматически. При изменении правила встроенный браузер закрывается: потребуется повторный вход. Если адаптер недоступен, скрытого переключения на другой маршрут нет. Внешние домены видеосервера добавляются отдельными правилами."));
        panel.Children.Add(MutedText("Прямой режим: IPv4. При включённых правилах работает локальный прокси VideoGrabber; системные HTTP/PAC-прокси не наследуются. Это не настройка VPN для всего компьютера."));
        try
        {
            var loaded = SiteRouteSettings.Load(RoutesPath);
            _routes = SiteRouteProfiles.NormalizePersisted(loaded);
            if (_routes.Rules.Count != loaded.Rules.Count) _routes.Save(RoutesPath);
        }
        catch (Exception ex) { _routeReadError = SensitiveDataRedactor.Redact(ex.Message); }
        _routePolicy.Update(_routes);
        RefreshRouteAdapters(); RefreshRouteList();
        _routeStatus.Text = _routeReadError ?? "Нет автоматического обхода: действуют только сохранённые правила.";
        return Card(panel);
    }
    private void RefreshRouteAdapters()
    {
        try
        {
            _routeAdapter.Items.Clear();
            foreach (var item in RouteConnector.GetAdapters())
                _routeAdapter.Items.Add(new ComboBoxItem { Content = item.Name + " — " + item.Address, Tag = item.Id });
            if (_routeAdapter.Items.Count > 0) _routeAdapter.SelectedIndex = 0;
        }
        catch (Exception ex) { _routeStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
    }
    private void RefreshRouteList()
    {
        _routeList.Items.Clear();
        foreach (var rule in _routes.Rules)
            _routeList.Items.Add(new ComboBoxItem { Content = rule.Host + (rule.IncludeSubdomains ? " (+ поддомены)" : " (точно)") + " → выбранный адаптер", Tag = rule });
        if (_routeList.Items.Count > 0) _routeList.SelectedIndex = 0;
    }
    private bool RoutingBusy()
    {
        if (!_operations.IsBusy && !_isInstallingComponents && !_browserInitializing && !_routeProbeRunning) return false;
        _routeStatus.Text = "Сначала завершите текущую загрузку, проверку или обработку.";
        return true;
    }
    private void SaveSiteRule()
    {
        if (RoutingBusy()) return;
        try
        {
            var host = SiteRouteSettings.NormalizeHost(_routeHost.Text);
            var adapter = (_routeAdapter.SelectedItem as ComboBoxItem)?.Tag as string;
            if (adapter is null) throw new InvalidOperationException("Выберите доступный сетевой адаптер.");
            ApplySiteRules(SiteRouteProfiles.Merge(_routes, host, adapter));
            _routeStatus.Text = "Правило сохранено: " + host + ". Теперь откройте урок во встроенном браузере.";
        }
        catch (Exception ex) { _routeStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
    }
    private void RemoveSiteRule()
    {
        if (RoutingBusy()) return;
        if ((_routeList.SelectedItem as ComboBoxItem)?.Tag is not SiteRouteRule rule) return;
        try
        {
            ApplySiteRules(new SiteRouteSettings(_routes.Rules.Where(r => r.Host != rule.Host)));
            _routeStatus.Text = "Правило удалено. Новые подключения следуют оставшимся правилам или маршруту Windows.";
        }
        catch (Exception ex) { _routeStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
    }
    private void ApplySiteRules(SiteRouteSettings settings)
    {
        if (_mediaBrowser is not null)
        {
            DestroyBrowser();
            _browserCard.Visibility = Visibility.Collapsed;
        }
        _routeProxy?.Dispose();
        _routeProxy = null;
        _browserUsesSiteRoutes = false;
        settings.Save(RoutesPath);
        _routes = settings;
        _routePolicy.Update(settings);
        _routeReadError = null;
        RefreshRouteList();
        DiagnosticHub.Log.Write("network.rules", "succeeded", "Saved rule count=" + settings.Rules.Count);
    }
    private SiteRouteProxy? EnsureRoutingProxy(Uri source, Uri? referer = null, DownloadRouteScope? routeScope = null)
    {
        if (_routeReadError is not null) throw new InvalidOperationException("Исправьте файл правил подключения в разделе «Компоненты»: " + _routeReadError);
        if (routeScope is not null) return routeScope.ResolveProxy(_routes, source, referer);
        if (DownloadRouteResolver.ResolveAdapterId(_routePolicy, _routes, source, referer) is null) return null;
        return _routeProxy ??= new SiteRouteProxy(new RouteConnector(_routePolicy).OpenAsync);
    }
    private async Task CheckSiteRouteAsync()
    {
        if (RoutingBusy()) return;
        _routeProbeRunning = true;
        try
        {
            var host = SiteRouteSettings.NormalizeHost(_routeHost.Text);
            if (_routes.Find(host) is null) { _routeStatus.Text = "Сначала сохраните правило для этого сайта."; return; }
            _routeStatus.Text = "Сравниваю подключение Windows и сохранённое правило…";
            var results = await Task.WhenAll(ProbeSiteAsync(host, new SiteRouteSettings([])), ProbeSiteAsync(host, _routes));
            _routeStatus.Text = "По маршруту Windows: " + results[0] + "\nПо правилу сайта: " + results[1];
        }
        catch (Exception ex) { _routeStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
        finally { _routeProbeRunning = false; }
    }
    private static async Task<string> ProbeSiteAsync(string host, SiteRouteSettings settings)
    {
        try
        {
            var connector = new RouteConnector(settings);
            using var handler = new SocketsHttpHandler
            {
                UseProxy = false, UseCookies = false, AllowAutoRedirect = false,
                ConnectCallback = (context, token) => new ValueTask<Stream>(connector.OpenAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, token))
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            using var response = await http.GetAsync("https://" + host + "/", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            var status = "HTTPS установлен, HTTP " + (int)response.StatusCode;
            DiagnosticHub.Log.Write("network.check", "succeeded", "host=" + host + " mode=" + (settings.Find(host) is null ? "system" : "selected-interface") + " HTTP=" + (int)response.StatusCode);
            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException)
        {
            DiagnosticHub.Log.Write("network.check", "failed", "host=" + host + " error=" + ex.GetType().Name);
            return "не установлено (" + ex.GetType().Name + ")";
        }
    }
}
