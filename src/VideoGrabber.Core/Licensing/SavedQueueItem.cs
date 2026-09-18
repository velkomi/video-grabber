namespace VideoGrabber.Core.Licensing;

public sealed record SavedQueueItem(
    Guid IntentId,
    string PageOriginPath,
    string MediaIdentity,
    int Ordinal,
    string Quality,
    string OutputMode,
    string State);

public sealed record SavedQueue(
    int Version,
    Guid AccountId,
    SavedQueueItem[] Items);
