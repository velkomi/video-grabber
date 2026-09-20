using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Networking;

public sealed record RouteAdapter(string Id, string Name, int Index, IPAddress Address);

public sealed class RouteConnector
{
    public const string AutoPhysicalAdapterId = "auto-physical";

    private readonly SiteRoutePolicy _policy;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _dnsResolver;

    public RouteConnector(SiteRouteSettings settings) : this(new SiteRoutePolicy(settings)) { }
    public RouteConnector(SiteRoutePolicy policy) : this(policy, static (host, token) => Dns.GetHostAddressesAsync(host, token)) { }
    public RouteConnector(SiteRoutePolicy policy, Func<string, CancellationToken, Task<IPAddress[]>> dnsResolver)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _dnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));
    }

    public static IReadOnlyList<RouteAdapter> GetAdapters()
    {
        var result = new List<RouteAdapter>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up
                || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel
                || LooksVirtualOrVpn(ni))
                continue;
            var props = ni.GetIPProperties();
            var ipv4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(a.Address) && !a.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))?.Address;
            if (ipv4 is null || !props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) continue;
            var index = props.GetIPv4Properties()?.Index ?? 0;
            if (index > 0) result.Add(new(ni.Id, ni.Name, index, ipv4));
        }
        return result;
    }

    public async Task<Stream> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProxyTarget(host, port);
        host = host.TrimEnd('.').ToLowerInvariant();

        string? adapterId;
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            adapterId = _policy.ResolveAdapterId(host);
            if (adapterId is null)
                throw new ArgumentException("Literal IP target was not resolved from an allowed routed media host.");
            addresses = [literal];
        }
        else
        {
            var lease = _policy.CaptureLease(host);
            adapterId = lease.AdapterId;
            using var dnsDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dnsDeadline.CancelAfter(
                string.Equals(adapterId, AutoPhysicalAdapterId, StringComparison.OrdinalIgnoreCase)
                    ? TimeSpan.FromSeconds(8)
                    : TimeSpan.FromSeconds(15));
            addresses = await _dnsResolver(host, dnsDeadline.Token).ConfigureAwait(false);
            if (!_policy.IsLeaseCurrent(lease, host))
                throw new InvalidOperationException("Маршрутизируемая сессия изменилась во время DNS-разрешения; соединение отменено.");
            if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
                throw new InvalidOperationException("Локальный или пустой ответ DNS отклонён.");
            if (adapterId is not null) _policy.RememberResolvedAddresses(host, addresses);
        }

        if (port == 3001 && !_policy.IsActiveSessionHost(host))
            throw new InvalidOperationException("Порт 3001 разрешён только внутри активной маршрутизируемой GetCourse-сессии.");

        if (string.Equals(
                adapterId,
                AutoPhysicalAdapterId,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!OperatingSystem.IsWindows())
            {
                var systemStream = await ConnectAsync(
                    host,
                    port,
                    addresses,
                    adapter: null,
                    cancellationToken,
                    TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                _policy.ReportRouteConnected(host, usedSystemRoute: true);
                return systemStream;
            }

            Exception? last = null;
            if (_policy.ShouldPreferSystemRoute(host))
            {
                try
                {
                    var preferredSystemStream = await ConnectAsync(
                        host,
                        port,
                        addresses,
                        adapter: null,
                        cancellationToken,
                        TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                    _policy.ReportRouteConnected(host, usedSystemRoute: true);
                    DiagnosticHub.Log.Write(
                        "network.auto-route",
                        "succeeded",
                        "host=" + host + " adapter=system-preferred");
                    return preferredSystemStream;
                }
                catch (IOException ex)
                {
                    last = ex;
                }
            }

            foreach (var candidate in GetAdapters().Take(2))
            {
                try
                {
                    var stream = await ConnectAsync(
                        host,
                        port,
                        addresses,
                        candidate,
                        cancellationToken,
                        TimeSpan.FromSeconds(6)).ConfigureAwait(false);
                    _policy.ReportRouteConnected(host, usedSystemRoute: false);
                    DiagnosticHub.Log.Write(
                        "network.auto-route",
                        "succeeded",
                        "host=" + host
                        + " adapter=" + candidate.Name);
                    return stream;
                }
                catch (IOException ex)
                {
                    last = ex;
                }
            }

            try
            {
                var stream = await ConnectAsync(
                    host,
                    port,
                    addresses,
                    adapter: null,
                    cancellationToken,
                    TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                _policy.ReportRouteConnected(host, usedSystemRoute: true);
                DiagnosticHub.Log.Write(
                    "network.auto-route",
                    "succeeded",
                    "host=" + host
                    + " adapter=system-fallback");
                return stream;
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Автоматический маршрут не установил соединение ни через физический адаптер, ни через системный маршрут.",
                    ex.InnerException is null ? last ?? ex : ex);
            }
        }

        var adapter = adapterId is null
            ? null
            : GetAdapters().FirstOrDefault(
                a => a.Id.Equals(
                    adapterId,
                    StringComparison.OrdinalIgnoreCase));
        if (adapterId is not null && adapter is null)
            throw new InvalidOperationException(
                "Выбранный адаптер недоступен. Выберите режим «Авто — физический интернет» или другой адаптер.");
        if (adapter is not null && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Выбор адаптера требует Windows.");

        return await ConnectAsync(
            host,
            port,
            addresses,
            adapter,
            cancellationToken).ConfigureAwait(false);
    }

    public void ReportTransportFailure(string host, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var systemPreferred = _policy.ReportTransportFailure(host);
        DiagnosticHub.Log.Write(
            "network.route-health",
            "observed",
            "host=" + host.TrimEnd('.').ToLowerInvariant()
            + " error=" + error.GetType().Name
            + " next=" + (systemPreferred ? "system-first" : "physical-first"));
    }

    private static async Task<Stream> ConnectAsync(
        string host,
        int port,
        IEnumerable<IPAddress> addresses,
        RouteAdapter? adapter,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        Exception? last = null;
        foreach (var address in addresses.Where(a => adapter is null || a.AddressFamily == AddressFamily.InterNetwork).Take(4))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                if (adapter is not null)
                {
                    socket.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)31, IPAddress.HostToNetworkOrder(adapter.Index));
                    socket.Bind(new IPEndPoint(adapter.Address, 0));
                }
                await socket.ConnectAsync(new IPEndPoint(address, port), deadline.Token).ConfigureAwait(false);
                DiagnosticHub.Log.Write("network.connect", "succeeded", "host=" + host + " mode=" + (adapter is null ? "system" : "selected-interface"));
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose(); last = ex;
                if (deadline.IsCancellationRequested) break;
            }
            catch { socket.Dispose(); throw; }
        }
        cancellationToken.ThrowIfCancellationRequested();
        DiagnosticHub.Log.Write("network.connect", "failed", "host=" + host + " mode=" + (adapter is null ? "system" : "selected-interface") + " error=" + last?.GetType().Name);
        throw new IOException("Соединение по выбранному правилу не установлено; другой маршрут не использован.", last);
    }

    public static void ValidateTarget(string host, int port)
    {
        ValidatePort(port);
        if (IPAddress.TryParse(host, out _)) throw new ArgumentException("Literal IP target refused; a DNS hostname is required for routing.");
        ValidateHostname(host);
    }

    public static void ValidateProxyTarget(string host, int port)
    {
        if (port == 3001)
        {
            if (IPAddress.TryParse(host, out _)) throw new ArgumentException("GetCourse realtime port requires a DNS hostname.");
            ValidateHostname(host);
            var normalized = host.TrimEnd('.').ToLowerInvariant();
            if (normalized != "getcourse.ru" && !normalized.EndsWith(".getcourse.ru", StringComparison.Ordinal))
                throw new ArgumentException("Порт 3001 разрешён только для GetCourse realtime-хостов.");
            return;
        }
        ValidatePort(port);
        if (IPAddress.TryParse(host, out var address))
        {
            if (!IsPublic(address)) throw new ArgumentException("Private or local IP target refused.");
            return;
        }
        ValidateHostname(host);
    }

    private static void ValidatePort(int port)
    {
        if (port is not (80 or 443))
            throw new ArgumentException($"Порт {port} не разрешён; допустимы веб-порты 80 и 443.");
    }

    private static void ValidateHostname(string host)
    {
        if (SiteRouteSettings.NormalizeHost(host) != host.TrimEnd('.').ToLowerInvariant())
            throw new ArgumentException("Некорректное имя узла.");
    }

    public static bool IsPublic(IPAddress address)
        => UrlPolicy.IsPublicAddress(address);

    private static bool LooksVirtualOrVpn(NetworkInterface ni)
    {
        var text = (ni.Name + " " + ni.Description).ToLowerInvariant();
        string[] markers =
        [
            "vpn",
            "openvpn",
            "wireguard",
            "tailscale",
            "tap-windows",
            "wsl",
            "hyper-v",
            "virtual ethernet",
            "virtualbox",
            "vmware",
            "loopback"
        ];
        return markers.Any(text.Contains);
    }
}
