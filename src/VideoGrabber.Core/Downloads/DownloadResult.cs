namespace VideoGrabber.Core.Downloads;

public sealed record DownloadResult(bool Success, string Message, string? OutputPath = null);

