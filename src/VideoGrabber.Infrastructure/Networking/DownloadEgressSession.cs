using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Networking;

public sealed class DownloadEgressSession : IDisposable
{
    private readonly IManagedEgressSessionRegistry _registry;
    private readonly SiteRouteProxy _proxy;
    private int _disposed;

    public DownloadEgressSession(
        IManagedEgressSessionRegistry registry,
        Func<string, int, CancellationToken, Task<Stream>> connect,
        EgressPolicy policy,
        Action<string, int>? validateTarget = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(policy);

        _proxy = new SiteRouteProxy(connect, validateTarget);
        try
        {
            Lease = _registry.Issue(new Uri(_proxy.ProxyUrl), policy);
        }
        catch
        {
            _proxy.Dispose();
            throw;
        }
    }

    public EgressSessionLease Lease { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _registry.Revoke(Lease.Id); }
        finally { _proxy.Dispose(); }
    }
}
