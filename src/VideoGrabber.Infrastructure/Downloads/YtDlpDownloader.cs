using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Downloads;

public sealed class YtDlpDownloader(IProcessRunner runner, ToolLocator tools, IMediaProbe? probe = null) : IVideoDownloader
{
    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var job = DiagnosticHub.Begin("download", request.Source.Host);
        using var cancelLog = cancellationToken.Register(job.Cancel);
        if (request.Source.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(request.Source.UserInfo))
            return new(false, "Поддерживаются HTTP/HTTPS-ссылки без встроенного пароля.");
        if (request.CookiesFile is not null && request.CookiesFromBrowser is not null)
            return new(false, "Выберите только один способ входа.");
        Directory.CreateDirectory(request.OutputDirectory);
        var outputTemplate = Path.Combine(request.OutputDirectory, "%(title).180B [%(id)s].%(ext)s");

        var arguments = new List<string>
        {
            "--ignore-config", "--no-overwrites",
            "--retries", "3", "--fragment-retries", "3", "--socket-timeout", "25",
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

        if (!string.IsNullOrWhiteSpace(request.CookiesFile))
        {
            if (!File.Exists(request.CookiesFile)) return new(false, "Временная сессия не найдена. Повторите вход.");
            arguments.AddRange(["--cookies", request.CookiesFile]);
        }
        if (request.Referer is not null)
        {
            if (request.Referer.Scheme is not ("http" or "https")) return new(false, "Некорректная исходная страница.");
            arguments.AddRange(["--referer", request.Referer.AbsoluteUri]);
        }
        if (!string.IsNullOrWhiteSpace(request.UserAgent) && request.UserAgent.Length <= 1024 && request.UserAgent.IndexOfAny(['\r', '\n']) < 0)
            arguments.AddRange(["--user-agent", request.UserAgent]);
        if (request.AudioOnly)
            arguments.AddRange(["--extract-audio", "--audio-format", "mp3", "--audio-quality", "2"]);
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
                        progress?.Report(new DownloadProgress(null, SensitiveDataRedactor.Redact(line)));
                    }
                    return;
                }
                progress?.Report(parsedProgress);
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var failure = DownloadFailureFormatter.Create(request.Source, result.StandardError);
            DiagnosticHub.Log.Write("download.failure", "failed", failure.Message + "\n" + failure.Details,
                jobId: job.Id, exitCode: result.ExitCode);
            return failure;
        }
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            return new(false, "Загрузчик завершился, но готовый непустой файл не найден.");
        progress?.Report(new DownloadProgress(null, "Проверяю медиафайл…"));
        var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(outputPath, cancellationToken).ConfigureAwait(false);
        if (!media.IsValid || (request.AudioOnly && (!media.HasAudio || media.HasVideo || media.AudioCodec != "mp3")))
            return new(false, "Файл получен, но проверка медиапотоков не пройдена.");
        job.Complete();
        return new(true, request.AudioOnly ? "MP3 загружен и проверен." : "Видео загружено и проверено.", outputPath);
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

}
