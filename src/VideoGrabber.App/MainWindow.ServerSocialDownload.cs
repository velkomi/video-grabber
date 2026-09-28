using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private sealed record ManagedDownloadLink(string Url, DateTimeOffset ExpiresAt);

    private static bool IsPublicSocialSource(Uri source)
    {
        var host = source.Host.TrimEnd('.').ToLowerInvariant();
        return host is "youtu.be"
            or "youtube.com"
            or "www.youtube.com"
            or "m.youtube.com"
            or "music.youtube.com"
            or "instagram.com"
            or "www.instagram.com"
            or "tiktok.com"
            or "www.tiktok.com"
            or "vm.tiktok.com"
            or "vt.tiktok.com"
            or "pinterest.com"
            or "www.pinterest.com"
            or "pin.it"
            || host.EndsWith(".youtube.com", StringComparison.Ordinal)
            || host.EndsWith(".instagram.com", StringComparison.Ordinal)
            || host.EndsWith(".tiktok.com", StringComparison.Ordinal)
            || host.EndsWith(".pinterest.com", StringComparison.Ordinal);
    }

    private static bool UsesExplicitSiteSession(UserDownloadIntent intent)
        => intent.CookieSelection is not null
            and not ""
            and not "embedded";

    private bool ShouldUseManagedSocialServer(UserDownloadIntent intent)
        => IsPublicSocialSource(intent.SelectedSource)
            && !UsesExplicitSiteSession(intent);

    private async Task<OperationOutcome> RunManagedSocialServerDownloadAsync(
        UserDownloadIntent intent)
    {
        if (_operations.IsBusy || _isInstallingComponents || _courseDownloadActive)
        {
            SetDownloadState(
                "Другая операция уже выполняется.",
                "Дождитесь завершения или отмените текущую операцию.",
                true);
            return OperationOutcome.Failed;
        }

        if (string.IsNullOrWhiteSpace(intent.OutputDirectory))
        {
            SetDownloadState("Выберите папку сохранения.", null, true);
            return OperationOutcome.Failed;
        }

        Directory.CreateDirectory(intent.OutputDirectory);

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(
            _windowLifetime.Token);
        _operation = operation;
        SetOperationControls(true);
        _lastLoggedProgressBucket = -1;
        SetProgress(null);
        SetDownloadState(
            "Проверяю публичную ссылку…",
            "YouTube / Shorts / Instagram / TikTok / Pinterest скачиваются через защищённый серверный social-поток без cookies.");

        try
        {
            var analyzed = await ManagedSendJsonWithRefreshAsync<AnalyzedMedia[]>(
                HttpMethod.Post,
                "/v1/sources/analyze",
                new AnalyzeSourceRequest(intent.SelectedSource),
                operation.Token);

            if (analyzed.Length == 0)
                throw new InvalidDataException("source_analysis_failed");

            var source = analyzed[0];
            var quality = ChooseManagedServerQuality(source.Qualities, intent.Quality);
            var kind = intent.AudioOnly ? "mp3" : "download";
            var request = new CreateJob(
                Guid.NewGuid(),
                string.Empty,
                kind,
                "server_worker",
                null,
                source.SourceId,
                quality,
                [],
                null,
                null);
            request = request with { RequestHash = HashManagedServerJob(request) };

            SetDownloadState(
                "Источник найден. Ставлю загрузку в очередь…",
                source.Title);

            var created = await ManagedSendJsonWithRefreshAsync<JobView>(
                HttpMethod.Post,
                "/v1/jobs",
                request,
                operation.Token);

            SetDownloadState(
                "Скачиваю через social-сервер…",
                "Windows-приложению не нужны cookies сайта. Готовый файл будет сохранён в выбранную папку.");

            var completed = await WaitForManagedServerJobAsync(
                created.JobId,
                operation.Token);

            if (completed.State != "completed" || completed.ArtifactId is null)
            {
                var detail = FriendlyServerSocialFailure(completed.Reason);
                SetDownloadState(
                    "Загрузить видео не удалось.",
                    detail,
                    true);
                SetProgress(0);
                return OperationOutcome.Failed;
            }

            SetDownloadState(
                "Файл готов. Получаю результат…",
                source.Title);
            SetProgress(95);

            var link = await ManagedSendJsonWithRefreshAsync<ManagedDownloadLink>(
                HttpMethod.Post,
                $"/v1/jobs/{completed.JobId:D}/download-link",
                new { },
                operation.Token);

            var path = await DownloadManagedServerArtifactAsync(
                link.Url,
                intent.OutputDirectory,
                completed.JobId,
                intent.AudioOnly,
                operation.Token);

            SetProgress(100);
            SetDownloadState("Готово.", path);
            _localMediaBox.Text = path;
            _localOutputBaseBox.Text = Path.Combine(
                Path.GetDirectoryName(path)!,
                Path.GetFileNameWithoutExtension(path) + "-text");
            RegisterDownloadedMedia(path);

            try { await LoadManagedAccountAsync(); }
            catch { }

            return OperationOutcome.Succeeded;
        }
        catch (OperationCanceledException)
        {
            SetDownloadState(
                "Загрузка отменена.",
                "Незавершённый локальный файл удалён.");
            return OperationOutcome.Cancelled;
        }
        catch (UnauthorizedAccessException ex)
        {
            SetDownloadState(
                "Сессия VideoGrabber требует обновления.",
                ex.Message,
                true);
            return OperationOutcome.Failed;
        }
        catch (Exception ex)
        {
            var code = ex.Message;
            SetDownloadState(
                "Не удалось скачать видео.",
                FriendlyServerSocialFailure(code),
                true);
            SetProgress(0);
            return OperationOutcome.Failed;
        }
        finally
        {
            _operation = null;
            SetOperationControls(false);
        }
    }

    private async Task<JobView> WaitForManagedServerJobAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < 1800; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            var job = await ManagedSendJsonWithRefreshAsync<JobView>(
                HttpMethod.Get,
                $"/v1/jobs/{jobId:D}",
                body: null,
                cancellationToken);

            if (job.State == "completed"
                || job.State == "failed"
                || job.State == "cancelled"
                || job.State == "review_required")
                return job;

            var progress = Math.Min(90, 5 + pass / 4);
            SetProgress(progress);
            SetDownloadState(
                "Скачиваю через social-сервер…",
                $"Статус: {job.State}. Результат автоматически сохранится на этот компьютер.");
        }

        throw new TimeoutException("server_social_timeout");
    }

    private async Task<T> ManagedSendJsonWithRefreshAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = ManagedRequest(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body);

            using var response = await _managedHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized
                && attempt == 0)
            {
                await RefreshManagedSensitiveSessionAsync(cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var code = await ReadManagedApiErrorCodeAsync(
                    response,
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException(code);
                throw new InvalidOperationException(code);
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("empty_server_response");
        }

        throw new UnauthorizedAccessException("managed_session_expired");
    }

    private static async Task<string> ReadManagedApiErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await response.Content.ReadFromJsonAsync<
                Dictionary<string, object?>>(
                cancellationToken: cancellationToken);
            if (value is not null)
            {
                foreach (var key in new[] { "detail", "code", "title" })
                {
                    if (value.TryGetValue(key, out var raw)
                        && raw is not null)
                    {
                        var text = raw.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
        }
        catch { }

        return "HTTP_" + (int)response.StatusCode;
    }

    private async Task<string> DownloadManagedServerArtifactAsync(
        string relativeUrl,
        string outputDirectory,
        Guid jobId,
        bool audioOnly,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(relativeUrl, UriKind.Relative, out var relative)
            || relative.OriginalString.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("download_link_invalid");

        using var response = await _managedHttp.GetAsync(
            relative,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var headerName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        var proposed = string.IsNullOrWhiteSpace(headerName)
            ? $"VideoGrabber-{jobId:N}" + (audioOnly ? ".mp3" : ".mp4")
            : headerName.Trim().Trim('"');
        proposed = Path.GetFileName(proposed);
        if (string.IsNullOrWhiteSpace(proposed))
            proposed = $"VideoGrabber-{jobId:N}" + (audioOnly ? ".mp3" : ".mp4");

        var finalPath = UniqueOutputPath(outputDirectory, proposed);
        var temporaryPath = finalPath + ".part-" + Guid.NewGuid().ToString("N");

        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, 1024 * 1024, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, finalPath);
            return finalPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch { }
            throw;
        }
    }

    private static string UniqueOutputPath(
        string directory,
        string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 2; index < 10_000; index++)
        {
            path = Path.Combine(
                directory,
                $"{stem} ({index}){extension}");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(
            directory,
            stem + "-" + DateTimeOffset.Now.ToUnixTimeMilliseconds() + extension);
    }

    private static string ChooseManagedServerQuality(
        string[] available,
        string requested)
    {
        var values = available
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (values.Length == 0)
            return "best";
        if (requested == "best")
            return values[0];
        if (requested == "4K")
            requested = "2160p";
        if (values.Contains(requested, StringComparer.Ordinal))
            return requested;

        var requestedHeight = ParseQualityHeight(requested);
        var candidates = values
            .Select(x => new { Value = x, Height = ParseQualityHeight(x) })
            .Where(x => x.Height is not null)
            .OrderByDescending(x => x.Height)
            .ToArray();

        if (requestedHeight is int ceiling)
            return candidates.FirstOrDefault(
                x => x.Height <= ceiling)?.Value
                ?? candidates.LastOrDefault()?.Value
                ?? values[0];

        return values[0];
    }

    private static int? ParseQualityHeight(string value)
    {
        if (value.EndsWith('p')
            && int.TryParse(value[..^1], out var height)
            && height > 0)
            return height;
        return null;
    }

    private static string HashManagedServerJob(CreateJob request)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("intentId", request.IntentId.ToString("D").ToLowerInvariant());
            writer.WriteString("kind", request.Kind);
            writer.WriteString("executor", request.Executor);
            if (request.DeviceId is Guid deviceId)
                writer.WriteString("deviceId", deviceId.ToString("D").ToLowerInvariant());
            else
                writer.WriteNull("deviceId");
            writer.WriteString("sourceId", request.SourceId);
            writer.WriteString("quality", request.Quality);
            writer.WritePropertyName("inputArtifactIds");
            writer.WriteStartArray();
            foreach (var artifactId in request.InputArtifactIds ?? [])
                writer.WriteStringValue(artifactId.ToString("D").ToLowerInvariant());
            writer.WriteEndArray();
            if (request.TrimStartMs is long trimStart)
                writer.WriteNumber("trimStartMs", trimStart);
            else
                writer.WriteNull("trimStartMs");
            if (request.TrimDurationMs is long trimDuration)
                writer.WriteNumber("trimDurationMs", trimDuration);
            else
                writer.WriteNull("trimDurationMs");
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
    }

    private static string FriendlyServerSocialFailure(string code)
        => code switch
        {
            "source_unavailable" =>
                "Площадка сообщает, что этот ролик недоступен или удалён.",
            "source_bot_check" =>
                "YouTube временно включил bot-check для серверного запроса. Это не требование входа в YouTube; повторите попытку позже.",
            "source_login_required" =>
                "Источник действительно закрыт или приватный. Для обычного публичного ролика вход не требуется.",
            "source_rate_limited" =>
                "Площадка временно ограничила серверные запросы. Повторите позже.",
            "source_runtime_incomplete" =>
                "Серверный social-модуль временно недоступен.",
            "access_unavailable" =>
                "Лимит текущего тарифа исчерпан или функция не входит в тариф.",
            "job_source_unavailable" =>
                "Источник или выбранное качество больше недоступны.",
            "review_required" =>
                "Сервер не получил подтверждённый результат и не будет считать это успешной загрузкой.",
            "server_social_timeout" =>
                "Сервер слишком долго готовит файл. Задание останется в очереди.",
            _ when code.StartsWith("HTTP_", StringComparison.Ordinal) =>
                "Сервер вернул ошибку " + code[5..] + ".",
            _ => code
        };
#endif
}
