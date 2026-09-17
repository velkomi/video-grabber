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
