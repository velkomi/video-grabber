using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private FrameworkElement BuildDesktopWorkerCard()
    {
        var panel = Vertical(8);
        panel.Children.Add(SectionHeading("Задания с Telegram на этом компьютере"));
        panel.Children.Add(MutedText(
            "Выключено по умолчанию. При включении сервер может назначить только задания этого аккаунта и зарегистрированного устройства. " +
            "Cookie, пароли и удалённые пути к локальным файлам не передаются."));
        _desktopWorkerToggle = new ToggleSwitch
        {
            Header = "Разрешить задания с Telegram на этом компьютере",
            IsOn = File.Exists(DesktopWorkerEnrollmentPath)
        };
        _desktopWorkerToggle.Toggled += async (_, _) =>
        {
            if (_desktopWorkerToggleBusy) return;
            await SetDesktopWorkerEnrollmentAsync(_desktopWorkerToggle.IsOn);
        };
        panel.Children.Add(_desktopWorkerToggle);
        _desktopWorkerStatus = MutedText(
            _desktopWorkerToggle.IsOn
                ? "Разрешение сохранено. Worker запустится после входа."
                : "Desktop worker выключен.");
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
                await File.WriteAllTextAsync(
                    DesktopWorkerEnrollmentPath, "enabled", _windowLifetime.Token);
                SetDesktopWorkerStatus("Desktop worker включён.");
                StartDesktopWorkerIfEnrolled();
            }
            else
            {
                try { if (File.Exists(DesktopWorkerEnrollmentPath)) File.Delete(DesktopWorkerEnrollmentPath); }
                catch (IOException) { }
                StopDesktopWorkerLoop();
                SetDesktopWorkerStatus("Desktop worker выключен. Локальные файлы не изменены.");
            }
        }
        finally { _desktopWorkerToggleBusy = false; }
    }

    private void StartDesktopWorkerIfEnrolled()
    {
        if (!File.Exists(DesktopWorkerEnrollmentPath)
            || string.IsNullOrWhiteSpace(_managedAccessToken)
            || _managedDeviceId is null
            || _desktopWorkerCts is not null)
            return;
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
                    SetDesktopWorkerStatus(
                        "Устройство отозвано или сессия недействительна. Worker остановлен.");
                    StopDesktopWorkerLoop();
                    return;
                }
                catch (HttpRequestException)
                {
                    SetDesktopWorkerStatus(
                        "Сервер недоступен. Задание остаётся waiting_for_worker.");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }

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
        catch (Exception ex)
        {
            SetDesktopWorkerStatus("Desktop worker остановлен: " + ex.Message);
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
            $"Получено задание: {lease.Work.Kind}, качество {lease.Work.Quality}. " +
            "Выберите локальный результат только после проверки источника.");

        using var leaseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = DesktopHeartbeatLoopAsync(client, lease, leaseCts);
        try
        {
            var picker = new FileOpenPicker();
            foreach (var extension in new[] { ".mp4", ".mkv", ".webm", ".mp3", ".m4a", ".wav", ".txt", ".srt" })
                picker.FileTypeFilter.Add(extension);
            InitializePicker(picker);
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                SetDesktopWorkerStatus(
                    "Файл не выбран. Lease будет освобождён сервером после истечения; кредит не фиксируется.");
                return;
            }

            var artifact = await client.UploadAsync(
                lease, file.Path, leaseCts.Token);
            var completed = await client.CompleteAsync(
                lease, artifact, leaseCts.Token);
            SetDesktopWorkerStatus(
                completed.State == "completed"
                    ? "Задание завершено и сервер подтвердил артефакт."
                    : "Сервер вернул состояние: " + completed.State);
        }
        finally
        {
            leaseCts.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
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
