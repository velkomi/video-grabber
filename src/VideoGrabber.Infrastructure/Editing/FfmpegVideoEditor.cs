using System.Globalization;
using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Media;

namespace VideoGrabber.Infrastructure.Editing;

public sealed class FfmpegVideoEditor(IProcessRunner runner, ToolLocator tools) : IVideoEditor
{
    public async Task EditAsync(VideoEditRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var job = DiagnosticHub.Begin("edit");
        using var cancelLog = cancellationToken.Register(job.Cancel);
        if (request.Inputs.Count == 0 || request.Inputs.Any(p => !File.Exists(p)))
            throw new ArgumentException("Исходные видеофайлы не найдены.", nameof(request));
        var output = Path.GetFullPath(request.OutputPath);
        if (File.Exists(output) || request.Inputs.Any(p => Path.GetFullPath(p).Equals(output, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Результат уже существует или совпадает с исходником. Выберите новое имя.", nameof(request));
        if (request.Mode == VideoEditMode.FastTrim && (request.Inputs.Count != 1 || request.Start is null || request.Start < TimeSpan.Zero || request.Duration is null || request.Duration <= TimeSpan.Zero))
            throw new ArgumentException("Для обрезки нужны один файл, неотрицательное начало и положительная длительность.", nameof(request));
        if (request.Mode == VideoEditMode.Join && request.Inputs.Count < 2)
            throw new ArgumentException("Для склейки нужны минимум два файла.", nameof(request));
        if (request.Mode is not (VideoEditMode.FastTrim or VideoEditMode.Join))
            throw new ArgumentException("Неизвестный режим редактирования.", nameof(request));
        var directory = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".vg-edit-" + Guid.NewGuid().ToString("N") + Path.GetExtension(output));
        var listPath = Path.Combine(directory, ".vg-concat-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var arguments = new List<string> { "-hide_banner", "-nostdin", "-n" };
            if (request.Mode == VideoEditMode.FastTrim)
            {
                arguments.AddRange(["-ss", FormatTime(request.Start!.Value), "-i", request.Inputs[0],
                    "-t", FormatTime(request.Duration!.Value), "-map", "0", "-c", "copy", "-movflags", "+faststart", temporary]);
            }
            else
            {
                if (request.Inputs.Any(p => p.IndexOfAny(['\r', '\n']) >= 0))
                    throw new ArgumentException("Недопустимое имя файла для склейки.", nameof(request));
                await File.WriteAllLinesAsync(listPath, request.Inputs.Select(p =>
                    $"file '{Path.GetFullPath(p).Replace("'", "'\\''", StringComparison.Ordinal)}'"), cancellationToken).ConfigureAwait(false);
                arguments.AddRange(["-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", "-movflags", "+faststart", temporary]);
            }
            var result = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg, arguments), null, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
                throw new InvalidOperationException(SensitiveDataRedactor.Redact(result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "FFmpeg завершился с ошибкой."));
            var media = await new FfprobeMediaProbe(runner, tools).ProbeAsync(temporary, cancellationToken).ConfigureAwait(false);
            if (!media.IsValid || !media.HasVideo) throw new InvalidOperationException("FFprobe не подтвердил готовое видео.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
            job.Complete();
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(listPath)) File.Delete(listPath);
        }
    }
    private static string FormatTime(TimeSpan value) => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
