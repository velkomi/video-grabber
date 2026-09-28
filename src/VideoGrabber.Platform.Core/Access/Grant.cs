namespace VideoGrabber.Platform.Core.Access;

public sealed record Grant(
    Guid Id,
    string Kind,
    string Source,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    long Available,
    long Reserved,
    bool Revoked,
    DateTimeOffset CreatedAt)
{
    public string? PlanId { get; init; }
}
