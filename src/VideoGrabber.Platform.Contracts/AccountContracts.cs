namespace VideoGrabber.Platform.Contracts;

public sealed record AccountProfile(
    Guid AccountId,
    string Role,
    bool Blocked,
    string[] LinkedProviders,
    DateTimeOffset? FirstPurchaseAt);

public sealed record VerifiedIdentity(
    string Issuer,
    string Provider,
    string Subject,
    string? VerifiedEmail,
    DateTimeOffset AuthTime,
    string Assurance);

public interface IAccountStore
{
    Task<AccountProfile> ResolveAsync(
        VerifiedIdentity identity,
        CancellationToken cancellationToken);

    Task<AccountProfile?> ReadAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}
