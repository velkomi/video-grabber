using System.Collections.Concurrent;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Networking;

public sealed class ManagedEgressSessionRegistry : IManagedEgressSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, EgressSessionLease> _leases = new();

    public EgressSessionLease Issue(Uri proxyUri, EgressPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(proxyUri);
        ArgumentNullException.ThrowIfNull(policy);
        ValidateProxyEndpoint(proxyUri);
        if (string.IsNullOrWhiteSpace(policy.Name))
            throw new ArgumentException("Egress policy name is required.", nameof(policy));

        while (true)
        {
            var lease = new EgressSessionLease(Guid.NewGuid(), proxyUri, policy);
            if (_leases.TryAdd(lease.Id, lease)) return lease;
        }
    }

    public EgressSessionLease Resolve(Guid id, Uri exactEndpoint)
    {
        ArgumentNullException.ThrowIfNull(exactEndpoint);
        if (!_leases.TryGetValue(id, out var lease))
            throw new InvalidOperationException("Unknown or revoked egress capability.");
        if (!string.Equals(lease.ProxyUri.AbsoluteUri, exactEndpoint.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Egress capability endpoint mismatch.");
        return lease;
    }

    public void Revoke(Guid id) => _leases.TryRemove(id, out _);

    public static void ValidateProxyEndpoint(Uri proxyUri)
    {
        if (!proxyUri.IsAbsoluteUri || proxyUri.Scheme != "socks5"
            || proxyUri.Host != "127.0.0.1" || proxyUri.Port is < 1024 or > 65535
            || !string.IsNullOrEmpty(proxyUri.UserInfo)
            || proxyUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(proxyUri.Query)
            || !string.IsNullOrEmpty(proxyUri.Fragment))
            throw new ArgumentException("Only an owned loopback SOCKS5 endpoint can be registered.", nameof(proxyUri));
    }
}
