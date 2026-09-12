using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Media;

namespace VideoGrabber.Infrastructure.Transcription;

public sealed record TranscriptResult(bool Success, string Message, string? TextPath = null, string? SubtitlesPath = null);

public sealed class WhisperTranscriber(IProcessRunner runner, ToolLocator tools, IMediaProbe? probe = null)
{
    public async Task<TranscriptResult> TranscribeAsync(string input, string outputBase, string executable,
        string model, string language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var job = DiagnosticHub.Begin("transcription");
        if (!File.Exists(input)) return new(false, "Исходный видео- или аудиофайл не найден.");
        if (!File.Exists(executable) || !File.Exists(model))
            return new(false, "Укажите whisper-cli.exe и модель ggml в разделе «Компоненты». Распознавание выполняется локально.");
        if (language != "auto" && (language.Length is < 2 or > 3 || language.Any(c => c is < 'a' or > 'z')))
            return new(false, "Некорректный код языка.");
        outputBase = Path.GetFullPath(outputBase);
        if (File.Exists(outputBase + ".txt") || File.Exists(outputBase + ".srt"))
            return new(false, "Текст или субтитры с таким именем уже существуют. Выберите новое имя.");
        var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(input, cancellationToken).ConfigureAwait(false);
        if (!media.IsValid || !media.HasAudio) return new(false, "В файле не найдена аудиодорожка.");
        var directory = Path.Combine(Path.GetDirectoryName(outputBase)!, ".vg-asr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var wav = Path.Combine(directory, "audio.wav");
        var temporaryBase = Path.Combine(directory, "transcript");
        bool textMoved = false, complete = false;
        try
        {
            var audio = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-hide_banner", "-nostdin", "-n", "-i", input, "-map", "0:a:0", "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav]),
                null, cancellationToken).ConfigureAwait(false);
            if (!audio.IsSuccess) return new(false, "Не удалось подготовить аудиодорожку для распознавания.");
            var result = await runner.RunAsync(new ProcessSpec(executable,
                ["-m", model, "-f", wav, "-l", language, "-otxt", "-osrt", "-of", temporaryBase,
                    "-t", Math.Min(4, Environment.ProcessorCount).ToString(), "-ng", "-np"],
                Path.GetDirectoryName(executable), SuppressOutputLogging: true), null, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || !File.Exists(temporaryBase + ".txt") || !File.Exists(temporaryBase + ".srt"))
                return new(false, "Whisper не создал оба результата. Проверьте совместимость EXE и модели.");
            if (string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(temporaryBase + ".txt", cancellationToken).ConfigureAwait(false)))
                return new(false, "Речь не распознана; пустой текст не считается успешным результатом.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryBase + ".txt", outputBase + ".txt", overwrite: false);
            textMoved = true;
            File.Move(temporaryBase + ".srt", outputBase + ".srt", overwrite: false);
            complete = true;
            job.Complete();
            return new(true, "Текст и субтитры созданы локально.", outputBase + ".txt", outputBase + ".srt");
        }
        catch (OperationCanceledException) { job.Cancel(); throw; }
        finally
        {
            if (textMoved && !complete) File.Delete(outputBase + ".txt");
            Directory.Delete(directory, recursive: true);
        }
    }
}
