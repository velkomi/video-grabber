using System.Net;
using System.Net.Sockets;

namespace VideoGrabber.Platform.Api.Jobs;

public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        => Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed class EgressProxy(IDnsResolver? dns = null)
{
    private readonly IDnsResolver _dns = dns ?? new SystemDnsResolver();

    public async Task<IReadOnlyList<IPAddress>> ValidatePublicTargetAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        ValidateUriShape(uri);
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.IdnHost, out var literal))
            addresses = [literal];
        else
            addresses = await _dns.ResolveAsync(uri.IdnHost, cancellationToken)
                .ConfigureAwait(false);
        if (addresses.Length == 0)
            throw new UnauthorizedAccessException("egress_dns_empty");
        foreach (var address in addresses)
            if (IsForbidden(address))
                throw new UnauthorizedAccessException("egress_private_target");
        return addresses;
    }

    public SocketsHttpHandler CreatePinnedHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, token) =>
            {
                var uri = new UriBuilder(
                    context.DnsEndPoint.Host.Contains(':') ? Uri.UriSchemeHttps : Uri.UriSchemeHttps,
                    context.DnsEndPoint.Host,
                    context.DnsEndPoint.Port).Uri;
                var addresses = await ValidatePublicTargetAsync(uri, token).ConfigureAwait(false);
                Exception? last = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(
                            new IPEndPoint(address, context.DnsEndPoint.Port), token).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                    {
                        socket.Dispose();
                        last = ex;
                        if (ex is OperationCanceledException) throw;
                    }
                }
                throw new HttpRequestException("Validated egress connection failed.", last);
            }
        };
    }

    private static void ValidateUriShape(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
            throw new UnauthorizedAccessException("egress_scheme_rejected");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new UnauthorizedAccessException("egress_userinfo_rejected");
        if (string.IsNullOrWhiteSpace(uri.IdnHost))
            throw new UnauthorizedAccessException("egress_host_required");
    }

    internal static bool IsForbidden(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6None))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || b[0] == 127
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || b[0] == 0
                || b[0] >= 224
                || (b[0] == 100 && b[1] is >= 64 and <= 127)
                || (b[0] == 198 && b[1] is 18 or 19);
        }

        var bytes = address.GetAddressBytes();
        return address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal
            || (bytes[0] & 0xfe) == 0xfc;
    }
}
