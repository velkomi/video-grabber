namespace VideoGrabber.Infrastructure.Networking;

public static class DownloadRouteResolver
{
    public static string? ResolveAdapterId(SiteRoutePolicy policy, SiteRouteSettings saved, Uri source, Uri? referer)
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
            policy.ConfigureSession(sessionHost, exact.AdapterId);
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
