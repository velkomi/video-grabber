namespace VideoGrabber.Core.Media;

public sealed record MediaProbeResult(
    bool IsValid, bool HasAudio, bool HasVideo, string? AudioCodec,
    string? Error = null, double DurationSeconds = 0);

public interface IMediaProbe
{
    Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken);
}
