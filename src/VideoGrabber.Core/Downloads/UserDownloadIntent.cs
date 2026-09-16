namespace VideoGrabber.Core.Downloads;

public sealed record UserDownloadIntent(Uri SelectedSource, string Quality, bool AudioOnly,
    string OutputDirectory, string? CookieSelection, long SessionEpoch);

public sealed record PreparedDownload(Uri Source, Uri? Referer, string? CookiesFile,
    string? UserAgent, string? LocalProxy, Uri? HlsVideoSource, Uri? HlsAudioSource,
    bool DirectManifest, bool ResolvedHlsLeaf, string? SuggestedBaseName,
    double? ExpectedDurationSeconds, bool? ExpectedAudio,
    Guid? EgressCapabilityId = null, Uri? EgressEndpoint = null);

public static class DownloadRequestFactory
{
    public static DownloadRequest Create(UserDownloadIntent intent, PreparedDownload prepared)
    {
        if (!DownloadQuality.TryParse(intent.Quality, out var quality))
            throw new ArgumentException("Invalid download quality.", nameof(intent));
        return new(prepared.Source, intent.OutputDirectory, quality.ToTag(),
            intent.CookieSelection is null or "" or "embedded" ? null : intent.CookieSelection,
            intent.AudioOnly, prepared.CookiesFile, prepared.Referer, prepared.UserAgent,
            prepared.LocalProxy, prepared.HlsVideoSource, prepared.HlsAudioSource,
            prepared.DirectManifest, prepared.SuggestedBaseName,
            ExpectedDurationSeconds: prepared.ExpectedDurationSeconds,
            ExpectedAudio: intent.AudioOnly ? true : prepared.ExpectedAudio,
            ResolvedHlsLeaf: prepared.ResolvedHlsLeaf,
            EgressCapabilityId: prepared.EgressCapabilityId,
            EgressEndpoint: prepared.EgressEndpoint);
    }

    public static async Task<DownloadRequest> PrepareAsync(UserDownloadIntent intent,
        Func<UserDownloadIntent, CancellationToken, Task<PreparedDownload>> prepare,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = await prepare(intent, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Create(intent, prepared);
    }
}
