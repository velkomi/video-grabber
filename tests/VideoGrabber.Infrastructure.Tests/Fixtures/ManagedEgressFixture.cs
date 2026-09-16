using System.Net;
using System.Net.Sockets;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

internal sealed class ManagedEgressFixture : IDisposable
{
    private readonly DownloadEgressSession session;
    private int disposed;

    private ManagedEgressFixture(string name, Uri listener, ManagedEgressSessionRegistry registry)
    {
        Registry = registry;
        SessionName = name;
        Listener = listener;
        session = new DownloadEgressSession(registry, ConnectAsync,
            new EgressPolicy("test:" + name, PublicOnly: false), ValidateTarget);
    }

    public string SessionName { get; }
    public Uri Listener { get; }
    public ManagedEgressSessionRegistry Registry { get; }
    public EgressSessionLease Lease => session.Lease;

    public static ManagedEgressFixture RegisterListener(string name, Uri listener,
        ManagedEgressSessionRegistry? registry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(listener);
        if (!listener.IsAbsoluteUri || listener.Scheme is not ("http" or "https")
            || listener.Port is < 1 or > 65535 || !IPAddress.TryParse(listener.Host, out var address)
            || !IPAddress.IsLoopback(address))
            throw new ArgumentException("Fixture listener must be an explicit owned loopback HTTP endpoint.", nameof(listener));
        return new ManagedEgressFixture(name, listener, registry ?? new ManagedEgressSessionRegistry());
    }

    public DownloadRequest Apply(DownloadRequest request) => request with
    {
        LocalProxy = Lease.ProxyUri.AbsoluteUri,
        EgressCapabilityId = Lease.Id,
        EgressEndpoint = Lease.ProxyUri
    };

    private void ValidateTarget(string host, int port)
    {
        if (port != Listener.Port || !IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address))
            throw new InvalidOperationException("Fixture capability attempted to reach an unregistered target.");
    }

    private async Task<Stream> ConnectAsync(string host, int port, CancellationToken token)
    {
        if (port != Listener.Port || !IPAddress.TryParse(host, out var address)
            || !IPAddress.IsLoopback(address))
            throw new InvalidOperationException("Fixture capability attempted to reach an unregistered target.");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Listener.Port), token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        session.Dispose();
    }
}
