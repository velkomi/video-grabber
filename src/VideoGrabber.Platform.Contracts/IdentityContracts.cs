namespace VideoGrabber.Platform.Contracts;

public sealed record LinkChallenge(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt);

public sealed record LinkProof(
    Guid ChallengeId,
    string FreshProviderAssertion);

public sealed record MergeRequest(
    Guid SourceAccountId,
    Guid TargetAccountId,
    string Reason,
    string SourceProof,
    string TargetProof);

public sealed record LinkedIdentity(
    Guid IdentityId,
    string Provider,
    string? VerifiedEmail,
    DateTimeOffset LinkedAt);

public sealed record BeginIdentityLink(
    Guid ChallengeId,
    string Provider,
    Uri ReturnUri,
    string ClientChallenge);

public sealed record CompleteIdentityLink(
    Guid ChallengeId,
    Guid FlowId,
    string Code,
    string State,
    string ClientVerifier);