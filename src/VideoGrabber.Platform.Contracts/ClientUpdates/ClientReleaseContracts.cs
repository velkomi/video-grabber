namespace VideoGrabber.Platform.Contracts.ClientUpdates;

public sealed record SignedClientRelease(string KeyId, string Payload, string Signature);

public sealed record ClientReleaseManifest(
    int SchemaVersion, string Product, string Channel, long Sequence,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string AccountRealm,
    ClientServiceEndpoints Services, ClientUpdateRelease Release);

public sealed record ClientServiceEndpoints(Uri ApiBase, Uri WebsiteBase);

public sealed record ClientUpdateRelease(
    string Version, DateTimeOffset PublishedAt, string Notes,
    ClientUpdateArtifact Installer, ClientUpdateArtifact? RollbackInstaller);

public sealed record ClientUpdateArtifact(string Version, Uri Url, long SizeBytes, string Sha256);
