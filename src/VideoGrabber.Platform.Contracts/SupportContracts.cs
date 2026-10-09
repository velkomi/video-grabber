using System.Text.Json.Serialization;

namespace VideoGrabber.Platform.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SupportRequest(
    Guid RequestId, string Topic, string Contact, string Message, string? Altcha = null, string? Website = null);

public sealed record SupportAccepted(Guid TicketId, string Status = "accepted");
