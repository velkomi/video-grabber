namespace VideoGrabber.Platform.Contracts;

public sealed record AccountProfile(
    Guid AccountId,
    string Role,
    bool Blocked,
    string[] LinkedProviders,
    DateTimeOffset? FirstPurchaseAt)
{
    public string? PrimaryAuthProvider { get; init; }
}

public sealed record VerifiedIdentity(
    string Issuer,
    string Provider,
    string Subject,
    string? VerifiedEmail,
    DateTimeOffset AuthTime,
    string Assurance)
{
    public bool AllowProviderCoalescing { get; init; }
}

public interface IAccountStore
{
    Task<AccountProfile> ResolveAsync(
        VerifiedIdentity identity,
        CancellationToken cancellationToken);

    Task<AccountProfile?> ReadAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}
