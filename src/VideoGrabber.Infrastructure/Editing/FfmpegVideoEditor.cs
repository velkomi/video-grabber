using System.Globalization;
using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Editing;

public sealed class FfmpegVideoEditor(IProcessRunner runner, ToolLocator tools) : IVideoEditor
{
    public async Task EditAsync(VideoEditRequest request, CancellationToken cancellationToken)
    {
        if (request.Inputs.Count == 0)
        {
            throw new ArgumentException("Не выбран исходный видеофайл.", nameof(request));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath) ?? Environment.CurrentDirectory);
        ProcessResult result;

        if (request.Mode == VideoEditMode.FastTrim)
        {
            if (request.Start is null || request.Duration is null || request.Duration <= TimeSpan.Zero)
            {
                throw new ArgumentException("Для обрезки укажите начало и длительность.", nameof(request));
            }

            result = await runner.RunAsync(
                new ProcessSpec(tools.Ffmpeg, new[]
                {
                    "-hide_banner", "-y",
                    "-ss", FormatTime(request.Start.Value),
                    "-i", request.Inputs[0],
                    "-t", FormatTime(request.Duration.Value),
                    "-map", "0",
                    "-c", "copy",
                    "-movflags", "+faststart",
                    request.OutputPath
                }), null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var listPath = Path.Combine(Path.GetTempPath(), $"VideoGrabber-{Guid.NewGuid():N}.txt");
            try
            {
                await File.WriteAllLinesAsync(
                    listPath,
                    request.Inputs.Select(path => $"file '{path.Replace("'", "'\\''", StringComparison.Ordinal)}'"),
                    cancellationToken).ConfigureAwait(false);

                result = await runner.RunAsync(
                    new ProcessSpec(tools.Ffmpeg, new[]
                    {
                        "-hide_banner", "-y", "-f", "concat", "-safe", "0",
                        "-i", listPath, "-c", "copy", "-movflags", "+faststart", request.OutputPath
                    }), null, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                File.Delete(listPath);
            }
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "FFmpeg завершился с ошибкой.");
        }

        var probe = await runner.RunAsync(
            new ProcessSpec(tools.Ffprobe, new[]
            {
                "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", request.OutputPath
            }), null, cancellationToken).ConfigureAwait(false);

        if (!probe.IsSuccess || !double.TryParse(probe.StandardOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration <= 0)
        {
            throw new InvalidOperationException("Файл создан, но проверка FFprobe не подтвердила корректное видео.");
        }
    }

    private static string FormatTime(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
