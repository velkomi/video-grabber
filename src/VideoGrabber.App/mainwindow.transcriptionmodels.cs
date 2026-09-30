using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private readonly List<ComboBox> _whisperModelSelectors = [];
    private readonly List<TextBlock> _whisperModelStatusLabels = [];
    private readonly SemaphoreSlim _whisperModelDownloadGate = new(1, 1);
    private CancellationTokenSource? _whisperModelDownloadCancellation;
    private bool _whisperModelSelectionSyncing;
    private string? _whisperModelDownloadProfileId;
    private int _whisperModelDownloadPercent;

    private static string WhisperModelCacheDirectory
        => Path.Combine(AppDataRoot, "models", "whisper");

    private ComboBox CreateWhisperModelSelector(string header = "Модель распознавания")
    {
        var box = new ComboBox
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var profile in WhisperModelCatalog.Profiles)
            box.Items.Add(ComboItem(profile.DisplayName, profile.Id));

        var selected = WhisperModelCatalog.Get(_preferences.WhisperModelProfile);
        box.SelectedIndex = WhisperModelCatalog.Profiles
            .Select((profile, index) => (profile, index))
            .First(tuple => tuple.profile.Id == selected.Id)
            .index;

        box.SelectionChanged += async (_, _) =>
        {
            if (_whisperModelSelectionSyncing)
                return;

            var id = (box.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                     ?? WhisperModelCatalog.DefaultProfileId;
            _preferences.WhisperModelProfile = id;
            PersistUiPreferences();
            SyncWhisperModelSelectors(id);
            RequestCourseTranscriptionRestartForSettingsChange(
                "Модель транскрибации изменена.");
            UpdateWhisperModelUi();

            var profile = WhisperModelCatalog.Get(id);
            if (!WhisperModelLooksReady(profile))
            {
                try
                {
                    _whisperModelDownloadCancellation?.Cancel();
                    _whisperModelDownloadCancellation?.Dispose();
                    _whisperModelDownloadCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            _windowLifetime.Token);
                    await EnsureWhisperModelAvailableAsync(
                        _whisperModelDownloadCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    DiagnosticHub.Log.Write(
                        "transcription.model",
                        "failed",
                        ex.GetType().Name);
                    UpdateWhisperModelUi(
                        "Не удалось скачать модель. Проверьте интернет и повторите выбор.");
                }
            }
        };

        _whisperModelSelectors.Add(box);
        return box;
    }

    private TextBlock CreateWhisperModelStatusText()
    {
        var text = MutedText(string.Empty);
        _whisperModelStatusLabels.Add(text);
        UpdateWhisperModelUi();
        return text;
    }

    private void SyncWhisperModelSelectors(string profileId)
    {
        _whisperModelSelectionSyncing = true;
        try
        {
            for (var selectorIndex = 0;
                 selectorIndex < _whisperModelSelectors.Count;
                 selectorIndex++)
            {
                var selector = _whisperModelSelectors[selectorIndex];
                for (var itemIndex = 0;
                     itemIndex < selector.Items.Count;
                     itemIndex++)
                {
                    if (selector.Items[itemIndex] is ComboBoxItem item
                        && string.Equals(
                            item.Tag?.ToString(),
                            profileId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selector.SelectedIndex = itemIndex;
                        break;
                    }
                }
            }
        }
        finally
        {
            _whisperModelSelectionSyncing = false;
        }
    }

    private WhisperModelProfile SelectedWhisperModelProfile()
        => WhisperModelCatalog.Get(_preferences.WhisperModelProfile);

    private string WhisperModelPath(WhisperModelProfile profile)
    {
        if (profile.Bundled)
            return Volatile.Read(ref _componentServices).Tools.WhisperModel;

        return Path.Combine(
            WhisperModelCacheDirectory,
            profile.FileName);
    }

    private static string WhisperModelVerificationPath(string modelPath)
        => modelPath + ".verified.sha256";

    private bool WhisperModelLooksReady(WhisperModelProfile profile)
    {
        try
        {
            var path = WhisperModelPath(profile);
            if (!File.Exists(path)
                || new FileInfo(path).Length != profile.ExpectedBytes)
                return false;

            if (profile.Bundled)
                return true;

            var verification = WhisperModelVerificationPath(path);
            return File.Exists(verification)
                   && string.Equals(
                       File.ReadAllText(verification).Trim(),
                       profile.ExpectedSha256,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return false;
        }
    }

    private async Task<string> EnsureWhisperModelAvailableAsync(
        CancellationToken token)
    {
        var profile = SelectedWhisperModelProfile();
        if (WhisperModelLooksReady(profile))
            return WhisperModelPath(profile);

        await _whisperModelDownloadGate.WaitAsync(token);
        try
        {
            if (WhisperModelLooksReady(profile))
                return WhisperModelPath(profile);

            var path = WhisperModelPath(profile);
            if (profile.Bundled)
                throw new FileNotFoundException(
                    "Встроенная модель распознавания отсутствует.",
                    path);

            Directory.CreateDirectory(WhisperModelCacheDirectory);

            if (File.Exists(path)
                && new FileInfo(path).Length == profile.ExpectedBytes)
            {
                UpdateWhisperModelUi("Проверяю уже загруженную модель…");
                var existingHash = await ComputeFileSha256Async(path, token);
                if (string.Equals(
                        existingHash,
                        profile.ExpectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    await File.WriteAllTextAsync(
                        WhisperModelVerificationPath(path),
                        profile.ExpectedSha256,
                        token);
                    UpdateWhisperModelUi();
                    return path;
                }

                File.Delete(path);
            }

            var root = Path.GetPathRoot(WhisperModelCacheDirectory);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                var reserve = 128L * 1024 * 1024;
                if (drive.IsReady
                    && drive.AvailableFreeSpace < profile.ExpectedBytes + reserve)
                    throw new IOException(
                        $"Недостаточно места на диске. Нужно примерно "
                        + WhisperModelCatalog.FormatSize(profile.ExpectedBytes + reserve)
                        + ".");
            }

            var temporary = path + ".download";
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);

                _whisperModelDownloadProfileId = profile.Id;
                _whisperModelDownloadPercent = 0;
                UpdateWhisperModelUi();

                long total;
                string actualSha;
                using (var http = new HttpClient
                       {
                           Timeout = Timeout.InfiniteTimeSpan
                       })
                using (var response = await http.GetAsync(
                           profile.DownloadUrl,
                           HttpCompletionOption.ResponseHeadersRead,
                           token))
                {
                    response.EnsureSuccessStatusCode();

                    await using var input =
                        await response.Content.ReadAsStreamAsync(token);
                    await using var output = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        1024 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var hash =
                        IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                    var buffer = new byte[1024 * 1024];
                    total = 0;
                    var lastPercent = -1;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, token);
                        if (read <= 0)
                            break;

                        await output.WriteAsync(
                            buffer.AsMemory(0, read),
                            token);
                        hash.AppendData(buffer, 0, read);
                        total += read;

                        var percent = (int)Math.Clamp(
                            total * 100L / profile.ExpectedBytes,
                            0,
                            100);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            _whisperModelDownloadPercent = percent;
                            UpdateWhisperModelUi();
                        }
                    }

                    await output.FlushAsync(token);
                    actualSha = Convert.ToHexString(
                            hash.GetHashAndReset())
                        .ToLowerInvariant();
                }

                if (total != profile.ExpectedBytes)
                    throw new IOException(
                        $"Размер модели не совпал: {total} байт вместо "
                        + $"{profile.ExpectedBytes}.");
                if (!string.Equals(
                        actualSha,
                        profile.ExpectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new IOException(
                        "Контрольная сумма модели не совпала.");

                File.Move(temporary, path, overwrite: true);
                await File.WriteAllTextAsync(
                    WhisperModelVerificationPath(path),
                    profile.ExpectedSha256,
                    token);

                DiagnosticHub.Log.Write(
                    "transcription.model",
                    "succeeded",
                    $"profile={profile.Id} bytes={profile.ExpectedBytes}");
                return path;
            }
            finally
            {
                _whisperModelDownloadProfileId = null;
                _whisperModelDownloadPercent = 0;
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                }

                UpdateWhisperModelUi();
            }
        }
        finally
        {
            _whisperModelDownloadGate.Release();
        }
    }

    private static async Task<string> ComputeFileSha256Async(
        string path,
        CancellationToken token)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void UpdateWhisperModelUi(string? overrideStatus = null)
    {
        if (DispatcherQueue is null)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            var profile = SelectedWhisperModelProfile();
            var ready = WhisperModelLooksReady(profile);
            var downloading = string.Equals(
                _whisperModelDownloadProfileId,
                profile.Id,
                StringComparison.OrdinalIgnoreCase);

            var status = overrideStatus;
            if (string.IsNullOrWhiteSpace(status))
            {
                if (downloading)
                {
                    status =
                        $"Скачиваю модель: {_whisperModelDownloadPercent}% · "
                        + $"{WhisperModelCatalog.FormatSize(profile.ExpectedBytes)}. "
                        + "После загрузки файл будет проверен автоматически.";
                }
                else
                {
                    var location = profile.Bundled
                        ? "уже входит в VideoGrabber"
                        : ready
                            ? "уже скачана на этот компьютер и используется из локального кэша"
                            : "будет скачана автоматически из открытого источника при выборе";
                    var cacheNote = profile.Bundled
                        ? string.Empty
                        : ready
                            ? " Повторно скачивать её не нужно."
                            : " Модель скачивается один раз и сохраняется между обновлениями приложения.";
                    status =
                        $"Качество: {profile.QualityLabel} · "
                        + $"Скорость: {profile.SpeedLabel} · "
                        + $"Место: {WhisperModelCatalog.FormatSize(profile.ExpectedBytes)} · "
                        + $"{location}.{cacheNote}{Environment.NewLine}"
                        + profile.Recommendation
                        + " Скорость зависит от процессора и длительности записи.";
                }
            }

            var editableCourseSettings =
            IsCourseTranscriptionBusy && CourseTranscriptionSettingsCanChange;
            var selectorEnabled = !downloading
                && (editableCourseSettings
                    || (!_courseDownloadActive
                        && !_operations.IsBusy
                        && !IsCourseTranscriptionBusy));
            foreach (var selector in _whisperModelSelectors)
                selector.IsEnabled = selectorEnabled;

            foreach (var label in _whisperModelStatusLabels)
                label.Text = status;

        });
    }
}
