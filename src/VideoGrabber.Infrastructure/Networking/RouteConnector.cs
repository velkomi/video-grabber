using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.Infrastructure.Networking;

public sealed record RouteAdapter(string Id, string Name, int Index, IPAddress Address);

public sealed class RouteConnector
{
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
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
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
            dnsDeadline.CancelAfter(TimeSpan.FromSeconds(15));
            addresses = await _dnsResolver(host, dnsDeadline.Token).ConfigureAwait(false);
            if (!_policy.IsLeaseCurrent(lease, host))
                throw new InvalidOperationException("Маршрутизируемая сессия изменилась во время DNS-разрешения; соединение отменено.");
            if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
                throw new InvalidOperationException("Локальный или пустой ответ DNS отклонён.");
            if (adapterId is not null) _policy.RememberResolvedAddresses(host, addresses);
        }

        if (port == 3001 && !_policy.IsActiveSessionHost(host))
            throw new InvalidOperationException("Порт 3001 разрешён только внутри активной маршрутизируемой GetCourse-сессии.");

        var adapter = adapterId is null ? null : GetAdapters().FirstOrDefault(a => a.Id.Equals(adapterId, StringComparison.OrdinalIgnoreCase));
        if (adapterId is not null && adapter is null)
            throw new InvalidOperationException("Выбранный адаптер недоступен. Автоматическая смена подключения запрещена.");
        if (adapter is not null && !OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Выбор адаптера требует Windows.");

        return await ConnectAsync(host, port, addresses, adapter, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Stream> ConnectAsync(string host, int port, IEnumerable<IPAddress> addresses, RouteAdapter? adapter, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
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
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (b.Length == 16)
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 1 && b[2] == 0x0d && b[3] == 0xb8);
        return b[0] is > 0 and < 224 && b[0] != 10 && b[0] != 127
            && !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254)
            && !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2))
            && !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100))
            && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}
