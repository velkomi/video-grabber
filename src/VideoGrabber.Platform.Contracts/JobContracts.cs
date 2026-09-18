namespace VideoGrabber.Platform.Contracts;

public sealed record CreateJob(
    Guid IntentId,
    string RequestHash,
    string Kind,
    string Executor,
    Guid? DeviceId,
    string SourceId,
    string Quality,
    Guid[] InputArtifactIds,
    long? TrimStartMs,
    long? TrimDurationMs);

public sealed record JobView(
    Guid JobId,
    Guid AccountId,
    Guid IntentId,
    string State,
    string Executor,
    Guid? ArtifactId,
    string Reason);

public sealed record AttemptLease(
    Guid JobId,
    Guid AttemptId,
    long Fence,
    DateTimeOffset LeaseUntil,
    string CapabilityToken,
    CreateJob Work);

public sealed record ArtifactReceipt(
    Guid ArtifactId,
    string Sha256,
    long Bytes,
    string MediaType,
    string VerificationEvidenceId);

public sealed record AttemptCompletion(
    Guid JobId,
    Guid AttemptId,
    long Fence,
    string Outcome,
    ArtifactReceipt? Artifact,
    string EvidenceId);

public sealed record WorkerClaim(Guid WorkerId);
public sealed record AnalyzedMedia(
    string SourceId,
    string MediaId,
    string Title,
    long? DurationMs,
    string[] Qualities,
    string RequiredExecutor);

public sealed record WorkerSourceDescriptor(
    string SourceId,
    Uri Source,
    string FormatSelector,
    int? Width,
    int? Height,
    string MediaType,
    DateTimeOffset ExpiresAt);
public sealed record AnalyzeSourceRequest(Uri Source);

public sealed record UploadTicketRequest(
    AttemptLease Lease,
    long Length,
    string Sha256,
    string MediaType);

public sealed record UploadTicket(
    Guid UploadId,
    Guid JobId,
    Guid AttemptId,
    long Fence,
    long MaximumBytes,
    DateTimeOffset ExpiresAt);
public sealed record DesktopCompletionRequest(AttemptLease Lease, ArtifactReceipt Artifact);
