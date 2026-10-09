namespace VideoGrabber.Platform.Contracts;

public sealed record ProductDocument(string Id, string Title, string Version, string Sha256, string Url);
public sealed record ConsentRequest(string DocumentId, string Version, string DocumentHash,
    string Decision, string Purpose, Guid IntentId, Guid? OperationId = null);
public sealed record ConsentEvent(long Sequence, string DocumentId, string Version, string DocumentHash,
    string Decision, string Purpose, Guid IntentId, Guid? OperationId, DateTimeOffset RecordedAt);
