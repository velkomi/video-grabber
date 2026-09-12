using System.Globalization;
using System.Text.Json;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Media;

public sealed class FfprobeMediaProbe(IProcessRunner runner, ToolLocator tools) : IMediaProbe
{
    public async Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            return new(false, false, false, null, "Файл отсутствует или пуст.");
        var result = await runner.RunAsync(new ProcessSpec(tools.Ffprobe,
            ["-v", "error", "-show_entries", "stream=codec_type,codec_name:format=duration", "-of", "json", path]),
            null, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess) return new(false, false, false, null, "FFprobe не подтвердил медиафайл.");
        try
        {
            using var json = JsonDocument.Parse(result.StandardOutput);
            var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            var audio = streams.FirstOrDefault(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio");
            var hasAudio = audio.ValueKind == JsonValueKind.Object;
            var hasVideo = streams.Any(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "video");
            var duration = json.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var d)
                && double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
            var codec = hasAudio && audio.TryGetProperty("codec_name", out var c) ? c.GetString() : null;
            var valid = (hasAudio || hasVideo) && double.IsFinite(duration) && duration > 0;
            return new(valid, hasAudio, hasVideo, codec, valid ? null : "Не найдены потоки положительной длительности.", duration);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(false, false, false, null, "Некорректный ответ FFprobe.");
        }
    }
}
