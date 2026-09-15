namespace VideoGrabber.Core.Downloads;

public sealed record DownloadRequest(
    Uri Source,
    string OutputDirectory,
    string Quality,
    string? CookiesFromBrowser = null,
    bool AudioOnly = false,
    string? CookiesFile = null,
    Uri? Referer = null,
    string? UserAgent = null,
    string? LocalProxy = null,
    Uri? HlsVideoSource = null,
    Uri? HlsAudioSource = null,
    bool DirectManifest = false,
    string? SuggestedBaseName = null,
    string? JobDirectory = null,
    double? ExpectedDurationSeconds = null,
    bool? ExpectedAudio = null,
    bool ResolvedHlsLeaf = false);
