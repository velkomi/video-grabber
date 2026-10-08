using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;
using Windows.Storage.Pickers;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
#if VIDEOGRABBER_MANAGED
    private ToggleSwitch _desktopWorkerToggle = null!;
    private TextBlock _desktopWorkerStatus = null!;
    private CancellationTokenSource? _desktopWorkerCts;
    private bool _desktopWorkerToggleBusy;

    private string DesktopWorkerEnrollmentPath
        => Path.Combine(AppDataRoot, "desktop-worker.enabled");
    private string DesktopWorkerOptOutPath
        => Path.Combine(AppDataRoot, "desktop-worker.disabled");

    private FrameworkElement BuildDesktopWorkerCard()
    {
        var panel = Vertical(8);
        panel.Children.Add(SectionHeading("Задания с сайта и Telegram на этом компьютере"));
        panel.Children.Add(MutedText(
            "Пока приложение открыто, оно принимает задания, отправленные именно на этот компьютер. " +
            "Приём можно отключить. Прямые загрузки на сайте продолжат работать."));
        _desktopWorkerToggle = new ToggleSwitch
        {
            Header = "Принимать задания, отправленные на этот компьютер",
            IsOn = !File.Exists(DesktopWorkerOptOutPath)
        };
        _desktopWorkerToggle.Toggled += async (_, _) =>
        {
            if (_desktopWorkerToggleBusy) return;
            await SetDesktopWorkerEnrollmentAsync(_desktopWorkerToggle.IsOn);
        };
        panel.Children.Add(_desktopWorkerToggle);
        _desktopWorkerStatus = MutedText(
            _desktopWorkerToggle.IsOn
                ? "Приём заданий включён. После входа этот компьютер доступен для отправки заданий."
                : "Приём заданий на этот компьютер отключён.");
        panel.Children.Add(_desktopWorkerStatus);
        return Card(panel);
    }

    private async Task SetDesktopWorkerEnrollmentAsync(bool enabled)
    {
        _desktopWorkerToggleBusy = true;
        try
        {
            Directory.CreateDirectory(AppDataRoot);
            if (enabled)
            {
                try { if (File.Exists(DesktopWorkerOptOutPath)) File.Delete(DesktopWorkerOptOutPath); }
                catch (IOException) { }
                await File.WriteAllTextAsync(
                    DesktopWorkerEnrollmentPath, "enabled", _windowLifetime.Token);
                SetDesktopWorkerStatus("Приём заданий на этот компьютер включён.");
                StartDesktopWorkerIfEnrolled();
            }
            else
            {
                await File.WriteAllTextAsync(
                    DesktopWorkerOptOutPath, "disabled", _windowLifetime.Token);
                try { if (File.Exists(DesktopWorkerEnrollmentPath)) File.Delete(DesktopWorkerEnrollmentPath); }
                catch (IOException) { }
                StopDesktopWorkerLoop();
                SetDesktopWorkerStatus("Приём заданий отключён. Прямые загрузки на сайте продолжат работать.");
            }
        }
        finally { _desktopWorkerToggleBusy = false; }
    }

    private void StartDesktopWorkerIfEnrolled()
    {
        if (File.Exists(DesktopWorkerOptOutPath)
            || string.IsNullOrWhiteSpace(_managedAccessToken)
            || _managedDeviceId is null
            || _desktopWorkerCts is not null)
            return;
        try
        {
            Directory.CreateDirectory(AppDataRoot);
            if (!File.Exists(DesktopWorkerEnrollmentPath))
                File.WriteAllText(DesktopWorkerEnrollmentPath, "enabled");
        }
        catch (IOException) { }
        _desktopWorkerCts = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
        _ = DesktopWorkerLoopAsync(_desktopWorkerCts.Token);
    }

    private void StopDesktopWorkerLoop()
    {
        var cts = Interlocked.Exchange(ref _desktopWorkerCts, null);
        if (cts is null) return;
        cts.Cancel();
        cts.Dispose();
    }

    private async Task DesktopWorkerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var unauthorizedRefreshAttempted = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                var secret = await ReadManagedDeviceSecretAsync();
                if (secret is null
                    || _managedDeviceId != secret.DeviceId
                    || string.IsNullOrWhiteSpace(_managedAccessToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                var client = CreateDesktopWorkerClient(secret);
                AttemptLease? lease;
                try
                {
                    lease = await client.PollAsync(secret.DeviceId, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    if (!unauthorizedRefreshAttempted)
                    {
                        unauthorizedRefreshAttempted = true;
                        try
                        {
                            SetDesktopWorkerStatus("Обновляю вход…");
                            await RefreshManagedSensitiveSessionAsync();
                            continue;
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or HttpRequestException)
                        {
                            // Fall through to the terminal message below.
                        }
                    }
                    SetDesktopWorkerStatus(
                        "Нужно войти снова или подключить этот компьютер в аккаунте.");
                    StopDesktopWorkerLoop();
                    return;
                }
                catch (HttpRequestException)
                {
                    SetDesktopWorkerStatus(
                        "Не удалось подключиться. Задание будет ждать этот компьютер.");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }

                unauthorizedRefreshAttempted = false;
                if (lease is null)
                {
                    SetDesktopWorkerStatus("Ожидаю задания…");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                await HandleDesktopWorkerLeaseAsync(client, lease, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            SetDesktopWorkerStatus("Приём заданий остановился. Проверьте интернет и перезапустите приложение.");
        }
        finally
        {
            if (_desktopWorkerCts?.Token == cancellationToken)
            {
                _desktopWorkerCts.Dispose();
                _desktopWorkerCts = null;
            }
        }
    }

    private DesktopWorkerClient CreateDesktopWorkerClient(ManagedDeviceSecret secret)
        => new(
            _managedHttp,
            () => _managedAccessToken,
            nonce =>
            {
                using var key = ECDsa.Create();
                var privateBytes = Convert.FromBase64String(secret.PrivateKeyPkcs8);
                try
                {
                    key.ImportPkcs8PrivateKey(privateBytes, out _);
                    return key.SignData(
                        Encoding.UTF8.GetBytes(nonce),
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }
                finally { CryptographicOperations.ZeroMemory(privateBytes); }
            });

    private async Task HandleDesktopWorkerLeaseAsync(
        DesktopWorkerClient client,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        SetDesktopWorkerStatus(
            "Получено задание для этого компьютера. Начинаю обработку; файл останется здесь.");

        using var leaseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = DesktopHeartbeatLoopAsync(client, lease, leaseCts);
        try
        {
            if (lease.Work.Kind is "download" or "mp3")
            {
                await HandleAutomaticDesktopDownloadAsync(
                    client, lease, leaseCts.Token);
                return;
            }

            if (lease.Work.Kind == "course_download")
            {
                await HandleAutomaticDesktopCourseAsync(
                    client, lease, leaseCts.Token);
                return;
            }

            await HandleLegacyDesktopArtifactAsync(
                client, lease, leaseCts.Token);
        }
        catch (OperationCanceledException) when (leaseCts.IsCancellationRequested)
        {
            SetDesktopWorkerStatus("Локальное задание остановлено.");
        }
        catch (Exception ex)
        {
            SetDesktopWorkerStatus("Не удалось выполнить задание. Проверьте доступ к источнику и повторите его с сайта.");
            try
            {
                await client.CompleteLocalAsync(
                    lease,
                    "failed",
                    "local-failed:" + HashManagedRequest(ex.GetType().Name)[..24],
                    CancellationToken.None);
            }
            catch { }
        }
        finally
        {
            leaseCts.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
    }

    private async Task HandleAutomaticDesktopDownloadAsync(
        DesktopWorkerClient client,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.Work.DeviceId is not Guid deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");

        var source = await client.ResolveSourceAsync(
            deviceId,
            lease.Work.SourceId,
            lease.Work.Quality,
            cancellationToken);

        var configured = _outputFolderBox?.Text;
        var outputDirectory = TryEnsureDownloadFolder(configured, out var saved)
            ? saved
            : ResolveInitialDownloadFolder();

        SetDesktopWorkerStatus(
            lease.Work.Kind == "mp3" ? "Сохраняю MP3 в выбранную папку…" : "Скачиваю видео в выбранную папку…");

        var progress = new Progress<DownloadProgress>(value =>
        {
            var percent = value.Percent is double p
                ? $" {Math.Clamp(p, 0, 100):0}%"
                : string.Empty;
            SetDesktopWorkerStatus(value.Status + percent);
        });

        var result = await _downloader.DownloadAsync(
            new DownloadRequest(
                source.Source,
                outputDirectory,
                lease.Work.Quality,
                AudioOnly: lease.Work.Kind == "mp3",
                ResumeKey: "web-" + lease.JobId.ToString("N")),
            progress,
            cancellationToken);

        if (!result.Success
            || string.IsNullOrWhiteSpace(result.OutputPath)
            || !File.Exists(result.OutputPath))
        {
            await client.CompleteLocalAsync(
                lease,
                "failed",
                "local-download-failed:" + lease.JobId.ToString("N"),
                cancellationToken);
            SetDesktopWorkerStatus(
                "Загрузка не завершена. Задание оставлено для безопасного повтора.");
            return;
        }

        var evidence = await LocalFileEvidenceAsync(
            result.OutputPath, cancellationToken);
        var completed = await client.CompleteLocalAsync(
            lease, "success", evidence, cancellationToken);

        SetDesktopWorkerStatus(
            completed.State == "completed"
                ? "Готово. Файл сохранён на этом компьютере."
                : "Файл готов, но завершение задания пока не подтверждено. Обновите историю на сайте.");
    }

    private async Task HandleAutomaticDesktopCourseAsync(
        DesktopWorkerClient client,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.Work.DeviceId is not Guid deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");

        var source = await client.ResolveSourceAsync(
            deviceId,
            lease.Work.SourceId,
            lease.Work.Quality,
            cancellationToken);

        if (_mediaBrowser?.CoreWebView2 is null)
        {
            await client.CompleteLocalAsync(
                lease,
                "failed",
                "course-browser-session-required:" + lease.JobId.ToString("N"),
                cancellationToken);
            SetDesktopWorkerStatus(
                "Для курса откройте встроенный браузер VideoGrabber и войдите в GetCourse. После этого задание повторится.");
            return;
        }

        var configured = _outputFolderBox?.Text;
        var outputDirectory = TryEnsureDownloadFolder(configured, out var saved)
            ? saved
            : ResolveInitialDownloadFolder();
        if (_outputFolderBox is not null)
            _outputFolderBox.Text = outputDirectory;

        SetDesktopWorkerStatus(
            "Открываю курс во встроенном браузере и начинаю скачивание…");
        if (!await NavigateCoursePageAsync(source.Source, cancellationToken))
            throw new InvalidOperationException("Не удалось открыть страницу курса.");

        _courseActiveQuality = lease.Work.Quality;
        await RunWholeGetCourseAsync(resume: false);

        var plan = _cachedCoursePlan;
        var completedCount = _courseCompletedLessons.Count;
        if (plan is null || plan.Lessons.Length == 0
            || completedCount < plan.Lessons.Length)
        {
            await client.CompleteLocalAsync(
                lease,
                "failed",
                "course-incomplete:" + lease.JobId.ToString("N"),
                cancellationToken);
            SetDesktopWorkerStatus(
                "Курс сохранён не полностью. Готовые уроки оставлены локально; задание можно повторить.");
            return;
        }

        var canonical = string.Join(
            "|",
            lease.JobId.ToString("N"),
            completedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.Lessons.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var evidence = "local-course:" + HashManagedRequest(canonical);
        var completed = await client.CompleteLocalAsync(
            lease, "success", evidence, cancellationToken);

        SetDesktopWorkerStatus(
            completed.State == "completed"
                ? $"Курс сохранён на этом компьютере. Уроков: {completedCount} из {plan.Lessons.Length}."
                : "Файл готов, но завершение задания пока не подтверждено. Обновите историю на сайте.");
    }

    private async Task HandleLegacyDesktopArtifactAsync(
        DesktopWorkerClient client,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        SetDesktopWorkerStatus(
            "Для этого задания выберите готовый файл на компьютере.");

        var picker = new FileOpenPicker();
        foreach (var extension in new[] { ".mp4", ".mkv", ".webm", ".mp3", ".m4a", ".wav", ".txt", ".srt" })
            picker.FileTypeFilter.Add(extension);
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            SetDesktopWorkerStatus(
                "Файл не выбран. Задание можно будет повторить позже.");
            return;
        }

        var evidence = await LocalFileEvidenceAsync(file.Path, cancellationToken);
        var completed = await client.CompleteLocalAsync(
            lease, "success", evidence, cancellationToken);
        SetDesktopWorkerStatus(
            completed.State == "completed"
                ? "Задание завершено. Файл остался на этом компьютере."
                : "Файл готов, но завершение задания пока не подтверждено. Обновите историю на сайте.");
    }

    private static async Task<string> LocalFileEvidenceAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return "local-sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task DesktopHeartbeatLoopAsync(
        DesktopWorkerClient client,
        AttemptLease lease,
        CancellationTokenSource leaseCts)
    {
        while (!leaseCts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), leaseCts.Token);
            if (!await client.HeartbeatAsync(
                    lease.Work.DeviceId!.Value, lease, leaseCts.Token))
            {
                leaseCts.Cancel();
                return;
            }
        }
    }

    private void SetDesktopWorkerStatus(string message)
        => AccountUi(() =>
        {
            if (_desktopWorkerStatus is not null)
                _desktopWorkerStatus.Text = message;
        });
#endif
}
