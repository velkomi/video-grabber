using System.Collections.Concurrent;
using System.Net;

namespace VideoGrabber.Infrastructure.Networking;

public sealed record RouteLease(string? AdapterId, long SessionVersion, bool IsSessionScoped);

public sealed class SiteRoutePolicy
{
    private SiteRouteSettings _current;
    private SessionState? _session;
    private long _sessionVersion;
    private readonly object _sessionGate = new();
    private readonly ConcurrentDictionary<string, string> _resolvedSessionIps = new(StringComparer.OrdinalIgnoreCase);

    public SiteRoutePolicy(SiteRouteSettings initial)
    {
        _current = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    public SiteRouteSettings Current => Volatile.Read(ref _current);

    public SiteRouteRule? Find(string host) => Current.Find(host);

    public void Update(SiteRouteSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Volatile.Write(ref _current, settings);
        ClearSession();
    }

    public bool ConfigureSession(string sourceHost, string adapterId)
    {
        var families = SiteRouteProfiles.GetSessionFamilies(sourceHost);
        if (families.Count == 0) return false;
        if (string.IsNullOrWhiteSpace(adapterId)) throw new ArgumentException("Adapter is required.", nameof(adapterId));
        lock (_sessionGate)
        {
            _resolvedSessionIps.Clear();
            var version = Interlocked.Increment(ref _sessionVersion);
            Volatile.Write(ref _session, new SessionState(adapterId.Trim(), families.ToArray(), version));
        }
        return true;
    }

    public bool IsActiveSessionHost(string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        var session = Volatile.Read(ref _session);
        return session is not null && !IPAddress.TryParse(host, out _) && MatchesFamily(host, session.Families);
    }

    public string? ResolveAdapterId(string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        var exact = Current.Find(host);
        if (exact is not null) return exact.AdapterId;

        var session = Volatile.Read(ref _session);
        if (session is null) return null;
        if (IPAddress.TryParse(host, out var address))
        {
            lock (_sessionGate)
            {
                if (!ReferenceEquals(session, Volatile.Read(ref _session))) return null;
                return RouteConnector.IsPublic(address) && _resolvedSessionIps.TryGetValue(address.ToString(), out var adapter)
                    ? adapter : null;
            }
        }
        return MatchesFamily(host, session.Families) ? session.AdapterId : null;
    }

    public void RememberResolvedAddresses(string host, IEnumerable<IPAddress> addresses)
    {
        var session = Volatile.Read(ref _session);
        if (session is null) return;
        host = host.TrimEnd('.').ToLowerInvariant();
        if (!MatchesFamily(host, session.Families)) return;
        var publicAddresses = addresses.Where(RouteConnector.IsPublic).ToArray();
        lock (_sessionGate)
        {
            if (!ReferenceEquals(session, Volatile.Read(ref _session))) return;
            foreach (var address in publicAddresses)
                _resolvedSessionIps[address.ToString()] = session.AdapterId;
        }
    }

    public void ClearSession()
    {
        lock (_sessionGate)
        {
            Interlocked.Increment(ref _sessionVersion);
            Volatile.Write(ref _session, null);
            _resolvedSessionIps.Clear();
        }
    }

    public RouteLease CaptureLease(string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        var exact = Current.Find(host);
        if (exact is not null) return new RouteLease(exact.AdapterId, 0, false);
        var session = Volatile.Read(ref _session);
        if (session is null || IPAddress.TryParse(host, out _) || !MatchesFamily(host, session.Families))
            return new RouteLease(null, 0, false);
        return new RouteLease(session.AdapterId, session.Version, true);
    }

    public bool IsLeaseCurrent(RouteLease lease, string host)
    {
        if (!lease.IsSessionScoped) return true;
        host = host.TrimEnd('.').ToLowerInvariant();
        var session = Volatile.Read(ref _session);
        return session is not null && session.Version == lease.SessionVersion
            && string.Equals(session.AdapterId, lease.AdapterId, StringComparison.OrdinalIgnoreCase)
            && MatchesFamily(host, session.Families);
    }

    private static bool MatchesFamily(string host, IReadOnlyList<string> families)
        => families.Any(family => host.Equals(family, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + family, StringComparison.OrdinalIgnoreCase));

    private sealed record SessionState(string AdapterId, IReadOnlyList<string> Families, long Version);
}
