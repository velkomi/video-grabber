using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Media;

namespace VideoGrabber.Infrastructure.Audio;

public sealed class FfmpegAudioExtractor(IProcessRunner runner, ToolLocator tools, IMediaProbe? probe = null)
{
    private readonly IMediaProbe _probe = probe ?? new FfprobeMediaProbe(runner, tools);

    public async Task<DownloadResult> ExtractAsync(string input, string output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(input)) return new(false, "Исходный файл не найден.");
        input = Path.GetFullPath(input);
        output = Path.GetFullPath(output);
        if (input.Equals(output, StringComparison.OrdinalIgnoreCase) || File.Exists(output))
            return new(false, "Файл результата уже существует. Выберите новое имя: исходники не перезаписываются.");
        if (!Path.GetExtension(output).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return new(false, "Для аудио укажите расширение .mp3.");
        var source = await _probe.ProbeAsync(input, cancellationToken).ConfigureAwait(false);
        if (!source.IsValid || !source.HasAudio) return new(false, "В исходном файле нет доступной аудиодорожки.");
        var folder = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".vg-audio-{Guid.NewGuid():N}.mp3");
        try
        {
            var converted = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-hide_banner", "-nostdin", "-n", "-i", input, "-map", "0:a:0", "-vn", "-c:a", "libmp3lame", "-q:a", "2", temporary]),
                null, cancellationToken).ConfigureAwait(false);
            if (!converted.IsSuccess) return new(false, "FFmpeg не смог извлечь звук. Подробности — в журнале.");
            var verified = await _probe.ProbeAsync(temporary, cancellationToken).ConfigureAwait(false);
            if (!verified.IsValid || !verified.HasAudio || verified.HasVideo || verified.AudioCodec != "mp3")
                return new(false, "Проверка MP3 не пройдена; результат не выдан как успешный.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
            return new(true, "MP3 создан и проверен.", output);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
