namespace VideoGrabber.Platform.Contracts;

public sealed record DestinationChallengeRequest(long ChatId);

public sealed record DestinationRequest(
    long ChatId,
    string Title,
    string Proof);

public sealed record DestinationChallenge(
    string Proof,
    DateTimeOffset ExpiresAt);

public sealed record DeliveryDestination(
    Guid DestinationId,
    Guid AccountId,
    long ChatId,
    string Kind,
    bool Revoked);

public sealed record TelegramChatRights(
    bool UserCanPublish,
    bool BotCanPublish,
    string Kind);
public sealed record DeliveryRequest(
    Guid JobId,
    Guid ArtifactId,
    Guid DestinationId,
    Guid IdempotencyKey);

public sealed record DeliveryView(
    Guid DeliveryId,
    string State,
    long? MessageId,
    string? TelegramFileId,
    string Reason);

public sealed record DeliveryRetry(
    string AcknowledgedWarning);
public sealed record RetentionCandidate(
    Guid ArtifactId,
    string OwnedRoot,
    string Path,
    DateTimeOffset RetainedUntil,
    bool Tombstoned,
    bool ActiveDeliveryOrReaderLease);

public sealed record RetentionCleanupResult(
    Guid ArtifactId,
    bool Deleted,
    string Reason);