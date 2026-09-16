namespace VideoGrabber.Core.Media;

public sealed record MediaStreamInfo(
    string Type, string Codec, int? Width, int? Height, string? PixelFormat, string? TimeBase,
    int? SampleRate, int? Channels);

public sealed record MediaProbeResult(
    bool IsValid, bool HasAudio, bool HasVideo, string? AudioCodec,
    string? Error = null, double DurationSeconds = 0,
    IReadOnlyList<MediaStreamInfo>? Streams = null);

public interface IMediaProbe
{
    Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken);
}
