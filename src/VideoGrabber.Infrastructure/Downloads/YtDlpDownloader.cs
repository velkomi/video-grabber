using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Downloads;

public sealed class YtDlpDownloader(IProcessRunner runner, ToolLocator tools) : IVideoDownloader
{
    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var outputTemplate = Path.Combine(request.OutputDirectory, "%(title).180B [%(id)s].%(ext)s");

        var arguments = new List<string>
        {
            "--newline",
            "--no-playlist",
            "--windows-filenames",
            "--trim-filenames", "220",
            "--progress-delta", "0.25",
            "--progress-template", "download:videograbber:%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.fragment_index)s|%(progress.fragment_count)s|%(progress._speed_str)s|%(progress._eta_str)s",
            "--print", "after_move:filepath:%(filepath)s",
            "--progress",
            "--remote-components", "ejs:github",
            "--ffmpeg-location", Path.GetDirectoryName(tools.Ffmpeg) ?? tools.Ffmpeg,
            "-f", SelectFormat(request),
            "--merge-output-format", "mp4",
            "-o", outputTemplate
        };

        if (File.Exists(tools.Deno))
        {
            arguments.Add("--js-runtimes");
            arguments.Add($"deno:{tools.Deno}");
        }

        if (!string.IsNullOrWhiteSpace(request.CookiesFromBrowser))
        {
            arguments.Add("--cookies-from-browser");
            arguments.Add(request.CookiesFromBrowser);
        }

        arguments.Add(request.Source.AbsoluteUri);

        string? outputPath = null;
        var result = await runner.RunAsync(
            new ProcessSpec(tools.YtDlp, arguments, request.OutputDirectory),
            line =>
            {
                if (line.StartsWith("filepath:", StringComparison.OrdinalIgnoreCase))
                {
                    outputPath = line["filepath:".Length..].Trim();
                }

                if (!YtDlpProgressParser.TryParse(line, out var parsedProgress))
                {
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        progress?.Report(new DownloadProgress(null, line));
                    }
                    return;
                }
                progress?.Report(parsedProgress);
            },
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new DownloadResult(true, "Видео загружено и обработано.", outputPath)
            : new DownloadResult(false, LastMeaningfulLine(result.StandardError) ?? "Загрузка завершилась с ошибкой.");
    }

    private static string SelectFormat(DownloadRequest request)
    {
        if (request.AudioOnly)
        {
            return "bestaudio/best";
        }

        return request.Quality switch
        {
            "720p" => "bestvideo*[height<=720]+bestaudio/best[height<=720]",
            "1080p" => "bestvideo*[height<=1080]+bestaudio/best[height<=1080]",
            "4K" => "bestvideo*[height<=2160]+bestaudio/best[height<=2160]",
            _ => "bestvideo*+bestaudio/best"
        };
    }

    private static string? LastMeaningfulLine(string value) =>
        value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
}
