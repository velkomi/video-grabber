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
        if (request.AudioOnly && request.HlsAudioSource is not null)
        {
            if (!IsSafeHttp(request.HlsAudioSource)) return new(false, "Некорректная HLS-аудиоссылка.");
            request = request with { Source = request.HlsAudioSource, HlsVideoSource = null, HlsAudioSource = null };
        }
        if (request.HlsVideoSource is not null || request.HlsAudioSource is not null)
        {
            if (request.HlsVideoSource is null || request.HlsAudioSource is null)
                return new(false, "Для раздельного HLS нужны и видео-, и аудиодорожка.");
            var splitResult = await DownloadSplitHlsAsync(request, progress, cancellationToken).ConfigureAwait(false);
            if (splitResult.Success) job.Complete();
            return splitResult;
        }
        var suggestedBase = string.IsNullOrWhiteSpace(request.SuggestedBaseName)
            ? null
            : DownloadFileName.SanitizeBaseName(request.SuggestedBaseName);
        var explicitJobDirectory = !string.IsNullOrWhiteSpace(request.JobDirectory);
        var jobRoot = explicitJobDirectory
            ? Path.GetFullPath(request.JobDirectory!)
            : Path.GetFullPath(request.OutputDirectory);
        var jobExisted = Directory.Exists(jobRoot);
        var ownsJobDirectory = explicitJobDirectory && !jobExisted;
        Directory.CreateDirectory(jobRoot);
        var beforeOutputFiles = SnapshotOutputFiles(request.OutputDirectory);
        var cleanupJob = true;
        try
        {
        var outputTemplate = suggestedBase is null
            ? Path.Combine(jobRoot, "%(title).180B [%(id)s].%(ext)s")
            : Path.Combine(jobRoot, suggestedBase + " - downloading.%(ext)s");
        var beforeFiles = SnapshotOutputFiles(jobRoot);

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

        if (request.DirectManifest) arguments.AddRange(["--use-extractors", "generic"]);

        if (request.LocalProxy is not null)
        {
            if (!Uri.TryCreate(request.LocalProxy, UriKind.Absolute, out var proxy) || proxy.Scheme != "socks5"
                || proxy.Host != "127.0.0.1" || proxy.Port is < 1024 or > 65535 || !string.IsNullOrEmpty(proxy.UserInfo)
                || proxy.AbsolutePath != "/" || !string.IsNullOrEmpty(proxy.Query) || !string.IsNullOrEmpty(proxy.Fragment))
                return new(false, "Invalid local routing endpoint.");
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

        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            outputPath = RecoverOutputPath(jobRoot, beforeFiles, suggestedBase);
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            outputPath = RecoverOutputPath(request.OutputDirectory, beforeOutputFiles, suggestedBase);
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            var failure = result.IsSuccess
                ? new DownloadResult(false, "Загрузчик завершился, но готовый непустой файл не найден.")
                : DownloadFailureFormatter.Create(request.Source, result.StandardError);
            DiagnosticHub.Log.Write("download.failure", "failed", failure.Message + "\n" + failure.Details,
                jobId: job.Id, exitCode: result.ExitCode);
            return failure;
        }
        progress?.Report(new DownloadProgress(null, "Проверяю медиафайл…"));
        var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(outputPath, cancellationToken).ConfigureAwait(false);
        if (!media.IsValid || (request.AudioOnly && (!media.HasAudio || media.HasVideo || media.AudioCodec != "mp3")))
        {
            TryDelete(outputPath);
            if (!result.IsSuccess)
            {
                var failure = DownloadFailureFormatter.Create(request.Source, result.StandardError);
                DiagnosticHub.Log.Write("download.failure", "failed", failure.Message + "\n" + failure.Details,
                    jobId: job.Id, exitCode: result.ExitCode);
                return failure;
            }
            return new(false, "Файл получен, но проверка медиапотоков не пройдена.");
        }
        if (!result.IsSuccess)
            DiagnosticHub.Log.Write("download.recovered", "succeeded", "Valid media recovered after downloader finalization error", jobId: job.Id);
        var outputParent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        var targetParent = Path.GetFullPath(request.OutputDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string finalOutput;
        if (suggestedBase is null
            && string.Equals(outputParent?.TrimEnd(Path.DirectorySeparatorChar), targetParent, StringComparison.OrdinalIgnoreCase))
        {
            finalOutput = outputPath;
        }
        else
        {
            var finalBase = suggestedBase ?? DownloadFileName.SanitizeBaseName(Path.GetFileNameWithoutExtension(outputPath));
            if (media.DurationSeconds > 0) finalBase += " - " + DownloadFileName.DurationTag(media.DurationSeconds);
            finalOutput = AvailableOutputPath(request.OutputDirectory, finalBase, Path.GetExtension(outputPath));
            finalOutput = await PromoteVerifiedOutputAsync(outputPath, finalOutput, cancellationToken).ConfigureAwait(false);
        }
        progress?.Report(new DownloadProgress(100, "Готовый файл проверен и сохранён."));
        job.Complete();
        return new(true, request.AudioOnly ? "MP3 загружен и проверен." : "Видео загружено и проверено.", finalOutput);
        }
        catch (OperationCanceledException)
        {
            cleanupJob = false;
            throw;
        }
        finally
        {
            if (cleanupJob && ownsJobDirectory) TryDeleteDirectory(jobRoot);
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

        var tempRoot = Path.Combine(request.OutputDirectory, ".videograbber-split-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            progress?.Report(new DownloadProgress(null, "Загружаю видеодорожку HLS…"));
            var video = await DownloadTrackAsync(videoSource, "video", tempRoot, request, progress, cancellationToken).ConfigureAwait(false);
            if (!video.Success || video.OutputPath is null) return video;

            progress?.Report(new DownloadProgress(null, "Загружаю аудиодорожку HLS…"));
            var audio = await DownloadTrackAsync(audioSource, "audio", tempRoot, request, progress, cancellationToken).ConfigureAwait(false);
            if (!audio.Success || audio.OutputPath is null) return audio;

            var safeBase = string.IsNullOrWhiteSpace(request.SuggestedBaseName)
                ? "HLS-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]
                : DownloadFileName.SanitizeBaseName(request.SuggestedBaseName);
            var finalOutput = AvailableOutputPath(request.OutputDirectory, safeBase, ".mp4");
            var mergedTemp = Path.Combine(tempRoot, "merged.mp4");
            progress?.Report(new DownloadProgress(null, "Объединяю видео и аудио…"));
            var merge = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
                ["-hide_banner", "-nostdin", "-n", "-i", video.OutputPath, "-i", audio.OutputPath,
                 "-map", "0:v:0", "-map", "1:a:0", "-c", "copy", "-movflags", "+faststart", mergedTemp],
                tempRoot), null, cancellationToken).ConfigureAwait(false);
            if (!merge.IsSuccess || !File.Exists(mergedTemp) || new FileInfo(mergedTemp).Length == 0)
                return new(false, "Не удалось объединить HLS-видео и аудио.", Details: SensitiveDataRedactor.Redact(merge.StandardError));

            progress?.Report(new DownloadProgress(null, "Проверяю итоговый медиафайл…"));
            var media = await (probe ?? new FfprobeMediaProbe(runner, tools)).ProbeAsync(mergedTemp, cancellationToken).ConfigureAwait(false);
            if (!media.IsValid || !media.HasVideo || !media.HasAudio)
                return new(false, "Итоговый HLS-файл не прошёл проверку наличия видео и звука.", Details: media.Error);
            File.Move(mergedTemp, finalOutput, overwrite: false);
            progress?.Report(new DownloadProgress(100, "HLS-видео со звуком проверено."));
            return new(true, "Видео загружено и проверено.", finalOutput);
        }
        finally
        {
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private async Task<DownloadResult> DownloadTrackAsync(
        Uri source,
        string role,
        string tempRoot,
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var template = Path.Combine(tempRoot, role + ".%(ext)s");
        var arguments = BuildTrackArguments(source, template, request);
        string? outputPath = null;
        var result = await runner.RunAsync(new ProcessSpec(tools.YtDlp, arguments, tempRoot), line =>
        {
            if (line.StartsWith("filepath:", StringComparison.OrdinalIgnoreCase))
                outputPath = line["filepath:".Length..].Trim();
            if (YtDlpProgressParser.TryParse(line, out var parsed)) progress?.Report(parsed);
        }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DownloadFailureFormatter.Create(source, result.StandardError);
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            return new(false, $"{role}: загрузчик завершился без готового файла.");
        return new(true, role + " HLS загружен.", outputPath);
    }

    private List<string> BuildTrackArguments(Uri source, string outputTemplate, DownloadRequest request)
    {
        var args = new List<string>
        {
            "--ignore-config", "--no-overwrites", "--retries", "3", "--fragment-retries", "3", "--socket-timeout", "25",
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

    private static Dictionary<string, (long Length, DateTime LastWriteUtc)> SnapshotOutputFiles(string directory)
    {
        if (!Directory.Exists(directory)) return new(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(directory)
            .Where(IsMediaOutput)
            .Select(path => new FileInfo(path))
            .ToDictionary(info => info.FullName, info => (info.Length, info.LastWriteTimeUtc), StringComparer.OrdinalIgnoreCase);
    }

    private static string? RecoverOutputPath(
        string directory,
        IReadOnlyDictionary<string, (long Length, DateTime LastWriteUtc)> before,
        string? suggestedBase)
    {
        if (!Directory.Exists(directory)) return null;
        var files = Directory.EnumerateFiles(directory).Where(IsMediaOutput).Select(path => new FileInfo(path)).Where(info => info.Length > 0).ToArray();
        var changed = files.Where(info => !before.TryGetValue(info.FullName, out var old)
                || old.Length != info.Length || old.LastWriteUtc != info.LastWriteTimeUtc)
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ThenByDescending(info => info.Length)
            .FirstOrDefault();
        if (changed is not null) return changed.FullName;
        if (string.IsNullOrWhiteSpace(suggestedBase)) return null;
        return files.Where(info => string.Equals(Path.GetFileNameWithoutExtension(info.Name), suggestedBase, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(info => info.LastWriteTimeUtc).Select(info => info.FullName).FirstOrDefault();
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


    private static async Task<string> PromoteVerifiedOutputAsync(string source, string destination, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(source, destination, overwrite: false);
                return destination;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2) break;
                await Task.Delay(200 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }
        var temp = destination + ".videograbber-copy-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, temp, overwrite: false);
            File.Move(temp, destination, overwrite: false);
            TryDelete(source);
            return destination;
        }
        finally { TryDelete(temp); }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private static bool IsSafeHttp(Uri uri)
        => uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

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
