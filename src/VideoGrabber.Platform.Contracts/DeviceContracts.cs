namespace VideoGrabber.Platform.Contracts;

public sealed record DeviceRegistration(
    string Name,
    string Platform,
    string PublicKey,
    Guid IdempotencyKey);

public sealed record DeviceReceipt(
    Guid DeviceId,
    string Name,
    bool Revoked);

public sealed record DeviceChallenge(
    Guid DeviceId,
    string Nonce,
    DateTimeOffset ExpiresAt);

public sealed record DeviceLeaseProof(
    string Nonce,
    string Signature);

public sealed record OfflineLeaseClaims(
    Guid AccountId,
    Guid DeviceId,
    Guid LeaseId,
    int Version,
    string RoleClass,
    string[] Features,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record SignedOfflineLease(string Token, string KeyId);

public sealed record LeasePublicKey(
    string KeyId,
    string Algorithm,
    string X,
    string Y);