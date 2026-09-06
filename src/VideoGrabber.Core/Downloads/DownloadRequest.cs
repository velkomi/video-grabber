namespace VideoGrabber.Core.Downloads;

public sealed record DownloadRequest(
    Uri Source,
    string OutputDirectory,
    string Quality,
    string? CookiesFromBrowser = null,
    bool AudioOnly = false);

