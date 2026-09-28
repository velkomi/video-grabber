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

public sealed record SupabaseSessionRequest(string AccessToken);

public sealed record SupabaseBrowserAuthConfig(
    string Url,
    string PublishableKey,
    bool EmailEnabled,
    bool GoogleEnabled,
    Uri RedirectUri);

public sealed record DesktopSignInStartRequest(Uri ReturnUri);

public sealed record DesktopSignInStart(
    Guid FlowId,
    Uri VerificationUri,
    string State,
    DateTimeOffset ExpiresAt);

public sealed record DesktopSignInApprovalRequest(
    Guid FlowId,
    string State);

public sealed record DesktopSignInApproval(
    Uri ReturnUri,
    string Code,
    string State);

public sealed record DesktopSignInConsumeRequest(
    Guid FlowId,
    string Code,
    string State);

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
