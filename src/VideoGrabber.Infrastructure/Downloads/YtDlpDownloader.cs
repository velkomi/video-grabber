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
        DownloadResult Fail(DownloadResult failure, DownloadWorkspace? workspace = null, int? exitCode = null)
        {
            job.Complete(false, failure.Message + "\n" + failure.Details, exitCode);
            return workspace is null ? failure : PreservedFailure(failure, workspace);
        }
        if (request.Source.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(request.Source.UserInfo))
            return Fail(new(false, "Поддерживаются HTTP/HTTPS-ссылки без встроенного пароля."));
        if (request.CookiesFile is not null && request.CookiesFromBrowser is not null)
            return Fail(new(false, "Выберите только один способ входа."));
        if (request.AudioOnly && request.HlsAudioSource is not null)
        {
            if (!IsSafeHttp(request.HlsAudioSource)) return Fail(new(false, "Некорректная HLS-аудиоссылка."));
            request = request with { Source = request.HlsAudioSource, HlsVideoSource = null, HlsAudioSource = null };
        }
        if (request.HlsVideoSource is not null || request.HlsAudioSource is not null)
        {
            if (request.HlsVideoSource is null || request.HlsAudioSource is null)
                return Fail(new(false, "Для раздельного HLS нужны и видео-, и аудиодорожка."));
            try
            {
                var splitResult = await DownloadSplitHlsAsync(request, progress, cancellationToken).ConfigureAwait(false);
                job.Complete(splitResult.Success, splitResult.Message + "\n" + splitResult.Details);
                return splitResult;
            }
            catch (OperationCanceledException)
            {
                job.Cancel("Загрузка отменена. Рабочие файлы сохранены.");
                throw;
            }
        }
        var suggestedBase = string.IsNullOrWhiteSpace(request.SuggestedBaseName)
            ? null
            : DownloadFileName.SanitizeBaseName(request.SuggestedBaseName);
        var workspace = DownloadWorkspace.Create(request.OutputDirectory, request.JobDirectory);
        var jobRoot = workspace.Root;
        string? outputPath = null;
        try
        {
        var outputTemplate = suggestedBase is null
            ? Path.Combine(jobRoot, "%(title).180B [%(id)s].%(ext)s")
            : Path.Combine(jobRoot, suggestedBase + " - downloading.%(ext)s");

        var arguments = new List<string>
        {
            "--ignore-config", "--no-overwrites", "--abort-on-unavailable-fragment",
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

        if (request.DirectManifest) arguments.AddRange(["--use-extractors", "generic"]);

        if (request.LocalProxy is not null)
        {
            if (!Uri.TryCreate(request.LocalProxy, UriKind.Absolute, out var proxy) || proxy.Scheme != "socks5"
                || proxy.Host != "127.0.0.1" || proxy.Port is < 1024 or > 65535 || !string.IsNullOrEmpty(proxy.UserInfo)
                || proxy.AbsolutePath != "/" || !string.IsNullOrEmpty(proxy.Query) || !string.IsNullOrEmpty(proxy.Fragment))
                return Fail(new(false, "Invalid local routing endpoint."), workspace);
            arguments.AddRange(["--proxy", "socks5h://127.0.0.1:" + proxy.Port]);
        }
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
            if (!File.Exists(request.CookiesFile)) return Fail(new(false, "Временная сессия не найдена. Повторите вход."), workspace);
            arguments.AddRange(["--cookies", request.CookiesFile]);
        }
        if (request.Referer is not null)
        {
            if (request.Referer.Scheme is not ("http" or "https")) return Fail(new(false, "Некорректная исходная страница."), workspace);
            arguments.AddRange(["--referer", request.Referer.AbsoluteUri]);
        }
        if (!string.IsNullOrWhiteSpace(request.UserAgent) && request.UserAgent.Length <= 1024 && request.UserAgent.IndexOfAny(['\r', '\n']) < 0)
            arguments.AddRange(["--user-agent", request.UserAgent]);
        if (request.AudioOnly)
            arguments.AddRange(["--extract-audio", "--audio-format", "mp3", "--audio-quality", "2"]);
        arguments.Add(request.Source.AbsoluteUri);

        var result = await runner.RunAsync(
            new ProcessSpec(tools.YtDlp, arguments, jobRoot),
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

        workspace.DiscoverCreatedFiles();
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess)
            return Fail(DownloadFailureFormatter.Create(request.Source, result.StandardError), workspace, result.ExitCode);
        if (!string.IsNullOrWhiteSpace(outputPath) && !workspace.Owns(outputPath))
            return Fail(new(false, "Загрузчик вернул файл вне рабочей папки задания."), workspace);
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            outputPath = workspace.OwnedFiles.Where(IsMediaOutput)
                .Where(path => workspace.Owns(path) && new FileInfo(path).Length > 0)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path)).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            return Fail(new(false, "Загрузчик завершился, но готовый непустой файл не найден."), workspace);
        }
        progress?.Report(new DownloadProgress(null, "Проверяю медиафайл…"));
        var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(outputPath, cancellationToken).ConfigureAwait(false);
        if (!DownloadOutputContract.IsSatisfied(request, media))
            return Fail(new(false, "Файл не соответствует ожидаемой длительности или дорожкам."), workspace);
        var finalBase = suggestedBase ?? DownloadFileName.SanitizeBaseName(Path.GetFileNameWithoutExtension(outputPath));
        if (media.DurationSeconds > 0) finalBase += " - " + DownloadFileName.DurationTag(media.DurationSeconds);
        var finalOutput = await PromoteVerifiedOutputAsync(workspace, outputPath, finalBase, cancellationToken).ConfigureAwait(false);
        workspace.CleanupVerifiedIntermediates();
        progress?.Report(new DownloadProgress(100, "Готовый файл проверен и сохранён."));
        job.Complete();
        return new(true, request.AudioOnly ? "MP3 загружен и проверен." : "Видео загружено и проверено.", finalOutput);
        }
        catch (OperationCanceledException ex)
        {
            job.Cancel("Загрузка отменена. Рабочие файлы сохранены.");
            ReportPreservedCancellation(workspace, progress, ex);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Fail(new(false, "Не удалось сохранить файл задания.", Details: SensitiveDataRedactor.Redact(ex.Message)), workspace);
        }
    }

    private async Task<DownloadResult> DownloadSplitHlsAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var videoSource = request.HlsVideoSource!;
        var audioSource = request.HlsAudioSource!;
        if (!IsSafeHttp(videoSource) || !IsSafeHttp(audioSource))
            return new(false, "Некорректные HLS-ссылки.");

        var workspace = DownloadWorkspace.Create(request.OutputDirectory, request.JobDirectory);
        var tempRoot = workspace.Root;
        try
        {
            progress?.Report(new DownloadProgress(null, "Загружаю видеодорожку HLS…"));
            var video = await DownloadTrackAsync(videoSource, "video", workspace, request, progress, cancellationToken).ConfigureAwait(false);
            if (!video.Success || video.OutputPath is null) return PreservedFailure(video, workspace);

            progress?.Report(new DownloadProgress(null, "Загружаю аудиодорожку HLS…"));
            var audio = await DownloadTrackAsync(audioSource, "audio", workspace, request, progress, cancellationToken).ConfigureAwait(false);
            if (!audio.Success || audio.OutputPath is null) return PreservedFailure(audio, workspace);

            var safeBase = string.IsNullOrWhiteSpace(request.SuggestedBaseName)
                ? "HLS-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]
                : DownloadFileName.SanitizeBaseName(request.SuggestedBaseName);
            var mergedTemp = Path.Combine(tempRoot, "merged.mp4");
            workspace.ValidateOwnedPath(mergedTemp);
            if (!workspace.Owns(video.OutputPath) || !workspace.Owns(audio.OutputPath))
                throw new InvalidOperationException("HLS tracks are outside the owned job directory.");
            progress?.Report(new DownloadProgress(null, "Объединяю видео и аудио…"));
            var merge = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-hide_banner", "-nostdin", "-n", "-i", video.OutputPath, "-i", audio.OutputPath,
                 "-map", "0:v:0", "-map", "1:a:0", "-c", "copy", "-movflags", "+faststart", mergedTemp],
                tempRoot), null, cancellationToken).ConfigureAwait(false);
            workspace.DiscoverCreatedFiles();
            cancellationToken.ThrowIfCancellationRequested();
            if (!merge.IsSuccess || !workspace.Owns(mergedTemp) || !File.Exists(mergedTemp) || new FileInfo(mergedTemp).Length == 0)
                return PreservedFailure(new(false, "Не удалось объединить HLS-видео и аудио.", Details: SensitiveDataRedactor.Redact(merge.StandardError)), workspace);

            progress?.Report(new DownloadProgress(null, "Проверяю итоговый медиафайл…"));
            var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(mergedTemp, cancellationToken).ConfigureAwait(false);
            if (!media.HasAudio || !DownloadOutputContract.IsSatisfied(request, media))
                return PreservedFailure(new(false, "Файл не соответствует ожидаемой длительности или дорожкам.", Details: media.Error), workspace);
            var finalOutput = await PromoteVerifiedOutputAsync(workspace, mergedTemp, safeBase, cancellationToken).ConfigureAwait(false);
            workspace.CleanupVerifiedIntermediates();
            progress?.Report(new DownloadProgress(100, "HLS-видео со звуком проверено."));
            return new(true, "Видео загружено и проверено.", finalOutput);
        }
        catch (OperationCanceledException ex)
        {
            ReportPreservedCancellation(workspace, progress, ex);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return PreservedFailure(new(false, "Не удалось сохранить HLS-файл задания.", Details: SensitiveDataRedactor.Redact(ex.Message)), workspace);
        }
    }

    private async Task<DownloadResult> DownloadTrackAsync(
        Uri source,
        string role,
        DownloadWorkspace workspace,
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempRoot = workspace.Root;
        var template = workspace.ValidateOwnedPath(Path.Combine(tempRoot, role + ".%(ext)s"));
        var arguments = BuildTrackArguments(source, template, request);
        string? outputPath = null;
        var result = await runner.RunAsync(new ProcessSpec(tools.YtDlp, arguments, tempRoot), line =>
        {
            if (line.StartsWith("filepath:", StringComparison.OrdinalIgnoreCase))
                outputPath = line["filepath:".Length..].Trim();
            if (YtDlpProgressParser.TryParse(line, out var parsed)) progress?.Report(parsed);
        }, cancellationToken).ConfigureAwait(false);
        workspace.DiscoverCreatedFiles();
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess)
            return DownloadFailureFormatter.Create(source, result.StandardError);
        if (string.IsNullOrWhiteSpace(outputPath) || !workspace.Owns(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            return new(false, $"{role}: загрузчик завершился без готового файла.");
        return new(true, role + " HLS загружен.", outputPath);
    }

    private List<string> BuildTrackArguments(Uri source, string outputTemplate, DownloadRequest request)
    {
        var args = new List<string>
        {
            "--ignore-config", "--no-overwrites", "--abort-on-unavailable-fragment", "--retries", "3", "--fragment-retries", "3", "--socket-timeout", "25",
            "--newline", "--no-playlist", "--progress", "--progress-delta", "0.25",
            "--progress-template", "download:videograbber:%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.fragment_index)s|%(progress.fragment_count)s|%(progress._speed_str)s|%(progress._eta_str)s",
            "--print", "after_move:filepath:%(filepath)s", "--ffmpeg-location", Path.GetDirectoryName(tools.Ffmpeg) ?? tools.Ffmpeg,
            "-f", "best", "--merge-output-format", "mp4", "-o", outputTemplate
        };
        if (request.DirectManifest) args.AddRange(["--use-extractors", "generic"]);
        AppendSessionArguments(args, request);
        args.Add(source.AbsoluteUri);
        return args;
    }

    private void AppendSessionArguments(List<string> arguments, DownloadRequest request)
    {
        if (request.LocalProxy is not null)
        {
            if (!Uri.TryCreate(request.LocalProxy, UriKind.Absolute, out var proxy) || proxy.Scheme != "socks5"
                || proxy.Host != "127.0.0.1" || proxy.Port is < 1024 or > 65535 || !string.IsNullOrEmpty(proxy.UserInfo))
                throw new InvalidOperationException("Invalid local routing endpoint.");
            arguments.AddRange(["--proxy", "socks5h://127.0.0.1:" + proxy.Port]);
        }
        if (!string.IsNullOrWhiteSpace(request.CookiesFile)) arguments.AddRange(["--cookies", request.CookiesFile]);
        else if (!string.IsNullOrWhiteSpace(request.CookiesFromBrowser)) arguments.AddRange(["--cookies-from-browser", request.CookiesFromBrowser]);
        if (request.Referer is not null) arguments.AddRange(["--referer", request.Referer.AbsoluteUri]);
        if (!string.IsNullOrWhiteSpace(request.UserAgent) && request.UserAgent.Length <= 1024 && request.UserAgent.IndexOfAny(['\r', '\n']) < 0)
            arguments.AddRange(["--user-agent", request.UserAgent]);
    }

    private static bool IsMediaOutput(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp4" or ".mkv" or ".webm" or ".mov" or ".m4a" or ".mp3" or ".aac" or ".opus" or ".ts";
    }

    private static string AvailableOutputPath(string directory, string baseName, string extension)
    {
        var safeBase = DownloadFileName.SanitizeBaseName(baseName);
        var candidate = Path.Combine(directory, safeBase + extension);
        if (!File.Exists(candidate)) return candidate;
        for (var i = 2; i <= 999; i++)
        {
            candidate = Path.Combine(directory, $"{safeBase} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        return Path.Combine(directory, safeBase + "-" + Guid.NewGuid().ToString("N")[..8] + extension);
    }


    private static async Task<string> PromoteVerifiedOutputAsync(
        DownloadWorkspace workspace, string source, string baseName, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!workspace.Owns(source)) throw new InvalidOperationException("Cannot promote an unowned file.");
            var destination = AvailableOutputPath(workspace.OutputDirectory, baseName, Path.GetExtension(source));
            workspace.ValidateDestination(destination);
            try
            {
                // The owned root is on the output volume; an atomic move needs no destructive copy fallback.
                File.Move(source, destination, overwrite: false);
                return destination;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(200 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static DownloadResult PreservedFailure(DownloadResult failure, DownloadWorkspace workspace)
        => failure with { Details = (failure.Details is null ? "" : failure.Details + "\n")
            + "Рабочие файлы сохранены: " + workspace.Root };

    private static void ReportPreservedCancellation(
        DownloadWorkspace workspace, IProgress<DownloadProgress>? progress, OperationCanceledException exception)
    {
        exception.Data["JobDirectory"] = workspace.Root;
        progress?.Report(new DownloadProgress(null, "Загрузка отменена. Рабочие файлы сохранены: " + workspace.Root));
    }

    private static bool IsSafeHttp(Uri uri)
        => uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);
    private static string SelectFormat(DownloadRequest request)
    {
        if (request.AudioOnly)
        {
            return "bestaudio/best";
        }

        return request.Quality switch
        {
            "360p" => "bestvideo*[height<=360]+bestaudio/best[height<=360]",
            "480p" => "bestvideo*[height<=480]+bestaudio/best[height<=480]",
            "720p" => "bestvideo*[height<=720]+bestaudio/best[height<=720]",
            "1080p" => "bestvideo*[height<=1080]+bestaudio/best[height<=1080]",
            "4K" => "bestvideo*[height<=2160]+bestaudio/best[height<=2160]",
            _ => "bestvideo*+bestaudio/best"
        };
    }

}
