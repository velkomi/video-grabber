namespace VideoGrabber.Platform.Contracts;

public sealed record BeginSignIn(
    string Provider,
    Uri ReturnUri,
    string ClientChallenge);

public sealed record SignInStart(
    Guid FlowId,
    Uri AuthorizationUri,
    DateTimeOffset ExpiresAt);

public sealed record CompleteSignIn(
    Guid FlowId,
    string Code,
    string State,
    string ClientVerifier);

public sealed record RefreshSession(string RefreshToken);

public sealed record ApiSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public interface IBrokerTokenValidator
{
    Task<VerifiedIdentity> ValidateAsync(
        string token,
        string expectedProvider,
        string expectedNonce,
        CancellationToken cancellationToken);
}
