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
            var sessionHost = sourceRule is not null ? source.IdnHost : referer!.IdnHost;
            if (sessionScope is null) policy.ConfigureSession(sessionHost, exact.AdapterId);
            else sessionScope.ConfigureSession(sessionHost, exact.AdapterId);
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
        if (saved.Find(source.IdnHost) is not null && SiteRouteProfiles.GetSessionFamilies(source.IdnHost).Count > 0) return true;
        return referer is not null && saved.Find(source.IdnHost) is null
            && saved.Find(referer.IdnHost) is not null
            && SiteRouteProfiles.GetSessionFamilies(referer.IdnHost).Count > 0;
    }
}

public sealed class DownloadRouteScope : IDisposable
{
    private readonly SiteRoutePolicy _policy;
    private readonly SiteRoutePolicy.SessionScope _sessionScope;
    private readonly SiteRouteProxy? _browserProxy;
    private SiteRouteProxy? _ownedProxy;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _ownedProxy?.Dispose(); }
        finally { _sessionScope.Dispose(); }
    }
}
