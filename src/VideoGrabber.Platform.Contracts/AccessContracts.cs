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

public sealed record ReservationRequest(
    Guid IntentId,
    string RequestHash,
    string Operation,
    string Executor,
    Guid? DeviceId);

public sealed record ReservationReceipt(
    Guid ReservationId,
    Guid IntentId,
    string State,
    bool UsesCredit,
    DateTimeOffset ExpiresAt);

public sealed record FinalizeReservation(
    Guid ReservationId,
    Guid AttemptId,
    long Fence,
    string Outcome,
    string EvidenceId);
