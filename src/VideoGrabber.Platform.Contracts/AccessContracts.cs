namespace VideoGrabber.Platform.Contracts;

public sealed record AccessSnapshot(
    bool CanDownload,
    bool CanEdit,
    bool Unlimited,
    DateTimeOffset? ValidUntil,
    long RemainingDownloads,
    DateTimeOffset? OfflineUntil,
    string Reason);

public sealed record GrantRequest(
    Guid AccountId,
    string Kind,
    int Days,
    long Credits,
    DateTimeOffset? ExpiresAt,
    string Reason,
    Guid IdempotencyKey);

public sealed record GrantReceipt(Guid GrantId, Guid AccountId, string Source);
