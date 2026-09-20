using System.Collections.Concurrent;
using System.Net;

namespace VideoGrabber.Infrastructure.Networking;

public sealed record RouteLease(string? AdapterId, long SessionVersion, bool IsSessionScoped);

public sealed class SiteRoutePolicy
{
    private static readonly TimeSpan SystemRoutePreferenceDuration = TimeSpan.FromMinutes(5);
    private SiteRouteSettings _current;
    private SessionState? _session;
    private long _sessionVersion;
    private readonly object _sessionGate = new();
    private readonly ConcurrentDictionary<string, string> _resolvedSessionIps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _preferSystemUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _lastRouteWasSystem = new(StringComparer.OrdinalIgnoreCase);

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
        _preferSystemUntil.Clear();
        _lastRouteWasSystem.Clear();
        ClearSession();
    }

    public bool ShouldPreferSystemRoute(string host)
    {
        host = NormalizeHost(host);
        if (!string.Equals(
                ResolveAdapterId(host),
                RouteConnector.AutoPhysicalAdapterId,
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (!_preferSystemUntil.TryGetValue(host, out var until))
            return false;
        if (until > DateTimeOffset.UtcNow)
            return true;

        _preferSystemUntil.TryRemove(host, out _);
        return false;
    }

    public void ReportRouteConnected(string host, bool usedSystemRoute)
    {
        host = NormalizeHost(host);
        if (!string.Equals(
                ResolveAdapterId(host),
                RouteConnector.AutoPhysicalAdapterId,
                StringComparison.OrdinalIgnoreCase))
            return;
        _lastRouteWasSystem[host] = usedSystemRoute;
    }

    public bool ReportTransportFailure(string host)
    {
        host = NormalizeHost(host);
        if (!string.Equals(
                ResolveAdapterId(host),
                RouteConnector.AutoPhysicalAdapterId,
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (_lastRouteWasSystem.TryGetValue(host, out var usedSystemRoute)
            && usedSystemRoute)
        {
            _preferSystemUntil.TryRemove(host, out _);
            return false;
        }

        _preferSystemUntil[host] =
            DateTimeOffset.UtcNow + SystemRoutePreferenceDuration;
        return true;
    }

    public bool ConfigureSession(string sourceHost, string adapterId, bool allowCustomRoot = false)
    {
        var families = SiteRouteProfiles.GetSessionFamilies(sourceHost, allowCustomRoot);
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

    public SessionScope BeginSessionScope() => new(this);

    public sealed class SessionScope : IDisposable
    {
        private readonly SiteRoutePolicy _policy;
        private long? _ownedVersion;
        private bool _disposed;

        internal SessionScope(SiteRoutePolicy policy) => _policy = policy;

        public bool ConfigureSession(string sourceHost, string adapterId, bool allowCustomRoot = false)
        {
            lock (_policy._sessionGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                // A browser or another owner may already hold (or replace) the session.
                if (_policy._session is { } current && current.Version != _ownedVersion) return false;
                if (!_policy.ConfigureSession(sourceHost, adapterId, allowCustomRoot)) return false;
                _ownedVersion = _policy._session!.Version;
                return true;
            }
        }

        public void Dispose()
        {
            lock (_policy._sessionGate)
            {
                if (_disposed) return;
                _disposed = true;
                if (_ownedVersion is not null && _policy._session?.Version == _ownedVersion)
                    _policy.ClearSession();
            }
        }
    }

    private static bool MatchesFamily(string host, IReadOnlyList<string> families)
        => families.Any(family => host.Equals(family, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + family, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeHost(string host)
        => (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    private sealed record SessionState(string AdapterId, IReadOnlyList<string> Families, long Version);
}
