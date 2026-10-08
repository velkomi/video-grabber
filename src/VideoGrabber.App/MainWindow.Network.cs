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
    private readonly ManagedEgressSessionRegistry _egressRegistry = new();
    private SiteRouteProxy? _routeProxy;
    private string? _routeReadError;
    private TextBox _routeHost = null!;
    private ComboBox _routeAdapter = null!;
    private ComboBox _routeList = null!;
    private bool _browserUsesSiteRoutes;
    private TextBlock _routeStatus = null!;
    private bool _routeProbeRunning;
    private static string RoutesPath => Path.Combine(AppDataRoot, "site-routes.json");
    private Border BuildNetworkCard()
    {
        var panel = Vertical(12);
        panel.Children.Add(SectionHeading("Подключение для отдельных сайтов"));
        panel.Children.Add(MutedText("Если сайт не открывается через VPN, попробуйте «Авто — обычное подключение». Приложение выберет Ethernet или Wi-Fi. Если они недоступны, подключение останется как в Windows."));
        _routeHost = new TextBox { Header = "Адрес сайта", PlaceholderText = "iglyrazuma.ru" };
        AttachPasteContextMenu(_routeHost);
        _routeAdapter = new ComboBox { Header = "Подключение для этого сайта", HorizontalAlignment = HorizontalAlignment.Stretch };
        _routeList = new ComboBox { Header = "Сохранённые правила", HorizontalAlignment = HorizontalAlignment.Stretch };
        _routeStatus = MutedText("");
        var save = PrimaryButton("Сохранить правило");
        var remove = SecondaryButton("Удалить выбранное правило");
        var refresh = SecondaryButton("Обновить подключения");
        var check = SecondaryButton("Проверить подключение к сайту");
        save.Click += (_, _) => SaveSiteRule();
        remove.Click += (_, _) => RemoveSiteRule();
        refresh.Click += (_, _) => RefreshRouteAdapters();
        check.Click += async (_, _) => await CheckSiteRouteAsync();
        panel.Children.Add(_routeHost); panel.Children.Add(_routeAdapter);
        panel.Children.Add(Horizontal(save, refresh));
        panel.Children.Add(_routeList); panel.Children.Add(remove); panel.Children.Add(check);
        panel.Children.Add(_routeStatus);
        panel.Children.Add(MutedText("После изменения правила встроенный браузер закроется — потребуется войти на сайт снова. Для GetCourse достаточно адреса учебного сайта: подключения к видео настроятся автоматически."));
        panel.Children.Add(MutedText("Правила действуют только внутри VideoGrabber. При их использовании системный прокси не применяется; настройки других приложений не меняются."));
        try
        {
            var loaded = SiteRouteSettings.Load(RoutesPath);
            var normalized = SiteRouteProfiles.NormalizePersisted(loaded);
            _routes = MakeRoutesPortable(normalized);
            if (!loaded.Rules.SequenceEqual(_routes.Rules))
                _routes.Save(RoutesPath);
        }
        catch (Exception) { _routeReadError = "Не удалось прочитать правила подключения. Проверьте настройки или обратитесь в поддержку."; }
        _routePolicy.Update(_routes);
        RefreshRouteAdapters(); RefreshRouteList();
        _routeStatus.Text = _routeReadError ?? "Подключение используется как в Windows, если для сайта не задано правило.";
        return Card(panel);
    }
    private static SiteRouteSettings MakeRoutesPortable(
        SiteRouteSettings settings)
    {
        var available = RouteConnector.GetAdapters()
            .Select(adapter => adapter.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rules = settings.Rules
            .Select(rule =>
                string.Equals(
                    rule.AdapterId,
                    RouteConnector.AutoPhysicalAdapterId,
                    StringComparison.OrdinalIgnoreCase)
                || available.Contains(rule.AdapterId)
                    ? rule
                    : rule with
                    {
                        AdapterId =
                            RouteConnector.AutoPhysicalAdapterId
                    })
            .ToArray();

        return new SiteRouteSettings(rules);
    }

    private void RefreshRouteAdapters()
    {
        try
        {
            _routeAdapter.Items.Clear();
            _routeAdapter.Items.Add(new ComboBoxItem
            {
                Content = "Авто — обычное подключение (рекомендуется)",
                Tag = RouteConnector.AutoPhysicalAdapterId
            });
            foreach (var item in RouteConnector.GetAdapters())
                _routeAdapter.Items.Add(new ComboBoxItem
                {
                    Content = item.Name + " — " + item.Address,
                    Tag = item.Id
                });
            _routeAdapter.SelectedIndex = 0;
        }
        catch (Exception) { _routeStatus.Text = "Не удалось получить список подключений. Попробуйте обновить его."; }
    }
    private void RefreshRouteList()
    {
        _routeList.Items.Clear();
        foreach (var rule in _routes.Rules)
        {
            var routeLabel = string.Equals(
                    rule.AdapterId,
                    RouteConnector.AutoPhysicalAdapterId,
                    StringComparison.OrdinalIgnoreCase)
                ? "Авто — обычное подключение"
                : "выбранное подключение";
            _routeList.Items.Add(new ComboBoxItem
            {
                Content = rule.Host
                    + (rule.IncludeSubdomains
                        ? " (включая страницы этого сайта)"
                        : " (только этот адрес)")
                    + " → "
                    + routeLabel,
                Tag = rule
            });
        }
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
            if (adapter is null) throw new InvalidOperationException("Выберите доступное подключение.");
            ApplySiteRules(SiteRouteProfiles.Merge(_routes, host, adapter));
            _routeStatus.Text = "Правило сохранено: " + host + ". Теперь откройте урок во встроенном браузере.";
        }
        catch (Exception) { _routeStatus.Text = "Не удалось сохранить правило. Проверьте адрес сайта и выбранное подключение."; }
    }
    private void RemoveSiteRule()
    {
        if (RoutingBusy()) return;
        if ((_routeList.SelectedItem as ComboBoxItem)?.Tag is not SiteRouteRule rule) return;
        try
        {
            ApplySiteRules(new SiteRouteSettings(_routes.Rules.Where(r => r.Host != rule.Host)));
            _routeStatus.Text = "Правило удалено. Теперь сайт использует подключение Windows или оставшееся подходящее правило.";
        }
        catch (Exception) { _routeStatus.Text = "Не удалось удалить правило. Попробуйте ещё раз."; }
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
        if (_routeReadError is not null) throw new InvalidOperationException("Не удалось прочитать правила подключения. Проверьте их в настройках или обратитесь в поддержку.");
        if (routeScope is not null) return routeScope.ResolveProxy(_routes, source, referer);
        if (DownloadRouteResolver.ResolveAdapterId(_routePolicy, _routes, source, referer) is null) return null;
        if (_routeProxy is not null) return _routeProxy;
        var connector = new RouteConnector(_routePolicy);
        _routeProxy = new SiteRouteProxy(
            connector.OpenAsync,
            onTransportFailure: connector.ReportTransportFailure);
        return _routeProxy;
    }

    private EgressSessionLease EnsureDownloadEgress(Uri source, Uri? referer, DownloadRouteScope routeScope)
    {
        if (_routeReadError is not null)
            throw new InvalidOperationException("Не удалось прочитать правила подключения. Проверьте их в настройках или обратитесь в поддержку.");
        return routeScope.ResolveEgress(_egressRegistry, _routes, source, referer);
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
            _routeStatus.Text = "Как в Windows: " + results[0] + "\nС правилом сайта: " + results[1];
        }
        catch (Exception) { _routeStatus.Text = "Не удалось проверить подключение. Проверьте адрес сайта и повторите попытку."; }
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
            var status = response.IsSuccessStatusCode ? "сайт доступен" : "подключение установлено, но сайт не открыл страницу";
            DiagnosticHub.Log.Write("network.check", "succeeded", "host=" + host + " mode=" + (settings.Find(host) is null ? "system" : "selected-interface") + " HTTP=" + (int)response.StatusCode);
            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException)
        {
            DiagnosticHub.Log.Write("network.check", "failed", "host=" + host + " error=" + ex.GetType().Name);
            return "подключиться не удалось";
        }
    }
}
