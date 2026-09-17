namespace VideoGrabber.Core.Licensing;

public sealed record ManagedOperation(
    Guid IntentId,
    string RequestHash,
    string Kind,
    string Executor,
    Guid? DeviceId);

public sealed record OperationPermit(
    Guid IntentId,
    Guid? ReservationId,
    bool Offline);
