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