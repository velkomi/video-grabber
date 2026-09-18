namespace VideoGrabber.Platform.Contracts;

public sealed record SubscriptionView(
    Guid SubscriptionId,
    Guid AccountId,
    string State,
    bool AutoRenew,
    DateTimeOffset PaidThrough,
    string Provider);

public sealed record RenewalEvent(
    string Provider,
    string Environment,
    string ChargeId,
    Guid SubscriptionId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    Money Amount);

public sealed record CancelSubscriptionRequest(Guid IdempotencyKey);
