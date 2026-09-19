namespace VideoGrabber.Infrastructure.Networking;

public static class SiteRouteProfiles
{
    private static readonly IReadOnlyDictionary<string, string[]> LegacyDependencies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["iglyrazuma.ru"] =
            [
                "fs.getcourse.ru",
                "ws04.getcourse.ru",
                "api2.gcvh.ru",
                "vh-asset-static2.kinescopecdn.net",
                "v01.getcourse.ru",
                "vh-75-integros.kinescopecdn.net"
            ]
        };

    private static readonly string[] GenericGetCourseFamilies =
    [
        "getcourse.ru",
        "gcvh.ru",
        "kinescopecdn.net",
        "servicecdn.ru",
        "vhcdn.com",
        "trbcdn.net",
        "gcfiles.net",
        "kinescope.io"
    ];
    private static readonly IReadOnlyDictionary<string, string[]> SessionFamilies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["iglyrazuma.ru"] =
            [
                "getcourse.ru",
                "gcvh.ru",
                "kinescopecdn.net",
                "servicecdn.ru",
                "vhcdn.com",
                "trbcdn.net",
                "gcfiles.net",
                "kinescope.io"
            ]
        };

    public static IReadOnlyList<string> GetSessionFamilies(string host, bool allowCustomRoot = false)
    {
        host = SiteRouteSettings.NormalizeHost(host);
        if (SessionFamilies.TryGetValue(host, out var families))
            return families;

        // Provider/CDN hosts are children of an active GetCourse session and must
        // never become a new root session themselves.
        if (GenericGetCourseFamilies.Any(family =>
                host.Equals(family, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + family, StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<string>();

        // GetCourse schools commonly use their own domains. Once the user starts
        // a routed course session on that custom host, inherit the same route for
        // the known GetCourse/Kinescope delivery families.
        return allowCustomRoot && !string.IsNullOrWhiteSpace(host)
            ? GenericGetCourseFamilies
            : Array.Empty<string>();
    }

    public static SiteRouteSettings Merge(SiteRouteSettings current, string host, string adapterId)
    {
        host = SiteRouteSettings.NormalizeHost(host);
        var rules = current.Rules.ToList();
        if (HasCompleteLegacyAutoProfile(current, host))
        {
            var legacy = LegacyDependencies[host];
            rules.RemoveAll(rule => legacy.Contains(rule.Host, StringComparer.OrdinalIgnoreCase));
        }
        rules.RemoveAll(rule => rule.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
        rules.Add(new SiteRouteRule(host, adapterId, false));
        return new SiteRouteSettings(rules);
    }

    public static SiteRouteSettings NormalizePersisted(SiteRouteSettings current)
    {
        var rules = current.Rules.ToList();
        foreach (var profile in LegacyDependencies.Keys)
        {
            if (!HasCompleteLegacyAutoProfile(current, profile)) continue;
            var legacy = LegacyDependencies[profile];
            rules.RemoveAll(rule => legacy.Contains(rule.Host, StringComparer.OrdinalIgnoreCase));
        }
        return new SiteRouteSettings(rules);
    }

    private static bool HasCompleteLegacyAutoProfile(SiteRouteSettings current, string rootHost)
    {
        if (!LegacyDependencies.TryGetValue(rootHost, out var legacy)) return false;
        var root = current.Find(rootHost);
        if (root is null) return false;
        return legacy.All(host => current.Find(host) is { } rule
            && string.Equals(rule.AdapterId, root.AdapterId, StringComparison.OrdinalIgnoreCase));
    }
}
