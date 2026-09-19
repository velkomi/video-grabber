using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Networking;

public static class DownloadRouteResolver
{
    public static string? ResolveAdapterId(SiteRoutePolicy policy, SiteRouteSettings saved, Uri source, Uri? referer,
        SiteRoutePolicy.SessionScope? sessionScope = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(source);

        var sourceRule = saved.Find(source.IdnHost);
        var refererRule = referer is null ? null : saved.Find(referer.IdnHost);
        var exact = sourceRule ?? refererRule;
        if (exact is not null)
        {
            var sessionUri = sourceRule is not null ? source : referer!;
            var sessionHost = sessionUri.IdnHost;
            var allowCustomRoot = LooksLikeGetCoursePage(sessionUri);
            if (sessionScope is null) policy.ConfigureSession(sessionHost, exact.AdapterId, allowCustomRoot);
            else sessionScope.ConfigureSession(sessionHost, exact.AdapterId, allowCustomRoot);
            return exact.AdapterId;
        }

        return policy.ResolveAdapterId(source.IdnHost)
            ?? (referer is null ? null : policy.ResolveAdapterId(referer.IdnHost));
    }

    public static bool RequiresProxy(SiteRouteSettings saved, Uri source, Uri? referer)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(source);
        return saved.Find(source.IdnHost) is not null
            || (referer is not null && saved.Find(referer.IdnHost) is not null);
    }

    public static bool WouldConfigureSession(SiteRouteSettings saved, Uri source, Uri? referer)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(source);
        if (saved.Find(source.IdnHost) is not null
            && SiteRouteProfiles.GetSessionFamilies(source.IdnHost, LooksLikeGetCoursePage(source)).Count > 0)
            return true;
        return referer is not null
            && saved.Find(source.IdnHost) is null
            && saved.Find(referer.IdnHost) is not null
            && SiteRouteProfiles.GetSessionFamilies(referer.IdnHost, LooksLikeGetCoursePage(referer)).Count > 0;
    }

    private static bool LooksLikeGetCoursePage(Uri uri)
        => uri.AbsolutePath.Contains("/teach/control/", StringComparison.OrdinalIgnoreCase);
}

public sealed class DownloadRouteScope : IDisposable
{
    private readonly SiteRoutePolicy _policy;
    private readonly SiteRoutePolicy.SessionScope _sessionScope;
    private readonly SiteRouteProxy? _browserProxy;
    private SiteRouteProxy? _ownedProxy;
    private DownloadEgressSession? _ownedEgress;
    private bool _disposed;

    public DownloadRouteScope(SiteRoutePolicy policy, SiteRouteProxy? browserProxy = null)
    {
        _policy = policy;
        _sessionScope = policy.BeginSessionScope();
        _browserProxy = browserProxy;
    }

    public SiteRouteProxy? ResolveProxy(SiteRouteSettings saved, Uri source, Uri? referer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (DownloadRouteResolver.ResolveAdapterId(_policy, saved, source, referer, _sessionScope) is null) return null;
        return _browserProxy ?? (_ownedProxy ??= new SiteRouteProxy(new RouteConnector(_policy).OpenAsync));
    }

    public EgressSessionLease ResolveEgress(IManagedEgressSessionRegistry registry,
        SiteRouteSettings saved, Uri source, Uri? referer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = DownloadRouteResolver.ResolveAdapterId(_policy, saved, source, referer, _sessionScope);
        _ownedEgress ??= new DownloadEgressSession(registry, new RouteConnector(_policy).OpenAsync,
            new EgressPolicy("download", PublicOnly: true));
        return _ownedEgress.Lease;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _ownedEgress?.Dispose(); }
        finally
        {
            try { _ownedProxy?.Dispose(); }
            finally { _sessionScope.Dispose(); }
        }
    }
}
