namespace VideoGrabber.Core.Downloads;

public sealed record DownloadProgress(double? Percent, string Status, string? Speed = null, string? Eta = null);

