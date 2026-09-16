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
            ["-v", "error", "-show_entries", "stream=codec_type,codec_name,width,height,pix_fmt,time_base,sample_rate,channels:format=duration", "-of", "json", path]),
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
            var streamInfo = streams.Select(ReadStream).ToArray();
            var valid = (hasAudio || hasVideo) && double.IsFinite(duration) && duration > 0;
            return new(valid, hasAudio, hasVideo, codec,
                valid ? null : "Не найдены потоки положительной длительности.", duration, streamInfo);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(false, false, false, null, "Некорректный ответ FFprobe.");
        }
    }

    private static MediaStreamInfo ReadStream(JsonElement stream)
    {
        string Text(string name) => stream.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
        int? Int(string name) => stream.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
        int? SampleRate()
            => stream.TryGetProperty("sample_rate", out var value)
                && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate) ? rate : null;
        return new MediaStreamInfo(Text("codec_type"), Text("codec_name"), Int("width"), Int("height"),
            Text("pix_fmt") is { Length: > 0 } pixel ? pixel : null,
            Text("time_base") is { Length: > 0 } timeBase ? timeBase : null,
            SampleRate(), Int("channels"));
    }
}