using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Core.Access;

public static class AccessEvaluator
{
    public static AccessSnapshot Evaluate(
        AccountProfile account,
        IReadOnlyList<Grant> grants,
        DateTimeOffset now)
    {
        if (account.Blocked)
            return new(false, false, false, null, 0, null, "account_blocked");

        if (account.Role == "owner_admin")
            return new(true, true, true, null, 0, now.AddHours(72), "owner_unlimited");

        var active = grants.Where(grant => !grant.Revoked
            && grant.StartsAt <= now
            && (grant.EndsAt is null || now < grant.EndsAt)).ToArray();
        var permanent = active.Any(grant => grant.Kind == "permanent");
        var timeEnd = ContiguousTimeEnd(grants, now);
        var credits = active.Where(grant => grant.Kind is "credits" or "hybrid")
            .Sum(grant => Math.Max(0, grant.Available));

        var timed = permanent || timeEnd > now;
        var validUntil = permanent ? null : timeEnd;
        DateTimeOffset? offline = null;
        if (timed)
        {
            var maximum = now.AddHours(24);
            offline = timeEnd is { } end && end < maximum ? end : maximum;
        }

        if (permanent)
            return new(true, true, true, null, credits, offline, "permanent_access");
        if (timed)
            return new(true, true, false, validUntil, credits, offline, "time_access");
        if (credits > 0)
            return new(true, false, false, null, credits, null, "online_credit_required");
        return new(false, false, false, null, 0, null, "no_grant");
    }

    private static DateTimeOffset? ContiguousTimeEnd(IReadOnlyList<Grant> grants, DateTimeOffset now)
    {
        var cursor = now;
        var found = false;
        foreach (var grant in grants.Where(g => g.Kind == "time" && !g.Revoked
                     && g.EndsAt is not null && g.EndsAt > now)
                 .OrderBy(g => g.StartsAt).ThenBy(g => g.EndsAt))
        {
            if (grant.StartsAt > cursor) break;
            if (grant.EndsAt > cursor) cursor = grant.EndsAt.Value;
            found = true;
        }
        return found ? cursor : null;
    }
}
