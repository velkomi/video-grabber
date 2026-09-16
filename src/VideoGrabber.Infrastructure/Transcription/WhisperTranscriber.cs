using System.Text;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Media;

namespace VideoGrabber.Infrastructure.Transcription;

public sealed record TranscriptResult(bool Success, string Message, string? TextPath = null, string? SubtitlesPath = null);

public sealed class WhisperTranscriber(IProcessRunner runner, ToolLocator tools, IMediaProbe? probe = null)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

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
        var media = await (probe ?? new FfprobeMediaProbe(runner, tools))
            .ProbeAsync(input, cancellationToken).ConfigureAwait(false);
        if (!media.IsValid || !media.HasAudio) return new(false, "В файле не найдена аудиодорожка.");
        var mediaDuration = media.DurationSeconds > 0 && double.IsFinite(media.DurationSeconds)
            ? media.DurationSeconds : (double?)null;
        var directory = Path.Combine(Path.GetDirectoryName(outputBase)!, ".vg-asr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var wav = Path.Combine(directory, "audio.wav");
        var temporaryBase = Path.Combine(directory, "transcript");
        var textPath = temporaryBase + ".txt";
        var srtPath = temporaryBase + ".srt";
        bool textMoved = false, complete = false, retainArtifacts = false;
        try
        {
            var audio = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-hide_banner", "-nostdin", "-n", "-i", input, "-map", "0:a:0", "-vn",
                    "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav]),
                null, cancellationToken).ConfigureAwait(false);
            if (!audio.IsSuccess)
                return new(false, "Не удалось подготовить аудиодорожку для распознавания.");
            var result = await runner.RunAsync(new ProcessSpec(executable,
                ["-m", model, "-f", wav, "-l", language, "-otxt", "-osrt", "-of", temporaryBase,
                    "-t", Math.Min(4, Environment.ProcessorCount).ToString(), "-ng", "-np"],
                Path.GetDirectoryName(executable), SuppressOutputLogging: true), null, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || !File.Exists(textPath) || !File.Exists(srtPath))
                return new(false, "Whisper не создал оба результата. Проверьте совместимость EXE и модели.");
            if (!WithinReadLimit(textPath) || !WithinReadLimit(srtPath))
            {
                retainArtifacts = true;
                DiagnosticHub.Log.Write("transcription.validate", "failed", "kind=artifact-size");
                return PreservedFailure("Результат распознавания превышает допустимый размер.", directory);
            }

            string transcript, subtitles;
            try
            {
                transcript = await File.ReadAllTextAsync(textPath, StrictUtf8, cancellationToken).ConfigureAwait(false);
                subtitles = await File.ReadAllTextAsync(srtPath, StrictUtf8, cancellationToken).ConfigureAwait(false);
            }
            catch (DecoderFallbackException)
            {
                retainArtifacts = true;
                DiagnosticHub.Log.Write("transcription.validate", "failed", "kind=utf8");
                return PreservedFailure("Результат распознавания содержит некорректный UTF-8.", directory);
            }
            if (string.IsNullOrWhiteSpace(transcript))
            {
                retainArtifacts = true;
                DiagnosticHub.Log.Write("transcription.validate", "failed", "kind=empty-text");
                return PreservedFailure("Речь не распознана; пустой текст не считается успешным результатом.", directory);
            }
            if (!SrtValidator.TryValidate(subtitles, mediaDuration, out var validationError))
            {
                retainArtifacts = true;
                DiagnosticHub.Log.Write("transcription.validate", "failed",
                    "kind=" + (validationError ?? "unknown"));
                return PreservedFailure("Субтитры Whisper не прошли проверку временных меток.", directory);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(textPath, outputBase + ".txt", overwrite: false);
            textMoved = true;
            File.Move(srtPath, outputBase + ".srt", overwrite: false);
            complete = true;
            job.Complete();
            return new(true, "Текст и субтитры созданы локально.", outputBase + ".txt", outputBase + ".srt");
        }
        catch (OperationCanceledException ex)
        {
            retainArtifacts = true;
            ex.Data["AsrDirectory"] = directory;
            job.Cancel();
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            retainArtifacts = true;
            DiagnosticHub.Log.Write("transcription.promote", "failed", "kind=" + ex.GetType().Name);
            return PreservedFailure("Не удалось безопасно сохранить оба результата распознавания.", directory);
        }
        finally
        {
            if (textMoved && !complete && File.Exists(outputBase + ".txt"))
                File.Delete(outputBase + ".txt");
            if (!retainArtifacts && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static bool WithinReadLimit(string path)
    {
        var length = new FileInfo(path).Length;
        return length is > 0 and <= SrtValidator.MaxUtf8Bytes;
    }

    private static TranscriptResult PreservedFailure(string message, string directory)
        => new(false, message + " Рабочие файлы сохранены: " + directory);
}
