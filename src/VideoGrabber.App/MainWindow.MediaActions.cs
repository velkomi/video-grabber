using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Core.Security;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Audio;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Processes;
using VideoGrabber.Infrastructure.Transcription;
using Windows.Storage.Pickers;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private TextBox _localMediaBox = null!;
    private TextBox _localOutputBaseBox = null!;
    private TextBlock _localMediaStatus = null!;
    private TextBox _whisperExeBox = null!;
    private TextBox _whisperModelBox = null!;
    private ComboBox _languageBox = null!;
    private Button _mp3Button = null!;
    private Button _textButton = null!;
    private UiPreferences _preferences = ReadPreferences();
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber", "preferences.json");
    private sealed class UiPreferences
    {
        public string WhisperExecutable { get; set; } = "";
        public string WhisperModel { get; set; } = "";
        public string Language { get; set; } = "auto";
        public LogMode LogMode { get; set; } = LogMode.Full;
    }

    private Border BuildMediaActionsCard()
    {
        var panel = Vertical(12);
        panel.Children.Add(SectionHeading("Звук и текст"));
        panel.Children.Add(MutedText("Работает с последним скачанным роликом или любым вашим локальным видео/аудиофайлом. Исходник не изменяется."));
        _localMediaBox = new TextBox { Header = "Видео или аудиофайл", PlaceholderText = "Вставьте путь к файлу или выберите его" };
        AttachPasteContextMenu(_localMediaBox);
        _localOutputBaseBox = new TextBox { Header = "Путь результата без расширения" };
        AttachPasteContextMenu(_localOutputBaseBox);
        var choose = SecondaryButton("Выбрать файл…");
        choose.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker();
                foreach (var extension in new[] { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".mp3", ".wav", ".m4a", ".ogg", ".flac" }) picker.FileTypeFilter.Add(extension);
                InitializePicker(picker);
                var file = await picker.PickSingleFileAsync();
                if (file is null) return;
                _localMediaBox.Text = file.Path;
                _localOutputBaseBox.Text = Path.Combine(Path.GetDirectoryName(file.Path)!, Path.GetFileNameWithoutExtension(file.Path) + "-result");
            }
            catch (Exception ex) { _localMediaStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
        };
        _mp3Button = PrimaryButton("Извлечь MP3");
        _textButton = SecondaryButton("Получить текст + SRT");
        _mp3Button.Click += async (_, _) => await RunLocalMediaAsync(false);
        _textButton.Click += async (_, _) => await RunLocalMediaAsync(true);
        var cancel = SecondaryButton("Отменить обработку");
        cancel.Click += (_, _) => CancelOperation();
        _localMediaStatus = MutedText("Для текста сначала укажите whisper.cpp и модель в разделе «Компоненты». Передачи аудио в облако нет.");
        panel.Children.Add(TwoColumn(_localMediaBox, choose, secondAuto: true));
        panel.Children.Add(_localOutputBaseBox);
        panel.Children.Add(Horizontal(_mp3Button, _textButton));
        panel.Children.Add(cancel);
        panel.Children.Add(_localMediaStatus);
        return Card(panel);
    }

    private async Task RunLocalMediaAsync(bool text)
    {
        if (_operations.IsBusy || _isInstallingComponents)
        {
            _localMediaStatus.Text = "Другая операция уже выполняется. Сначала завершите или отмените её.";
            return;
        }
        var input = _localMediaBox.Text.Trim().Trim('"');
        var output = _localOutputBaseBox.Text.Trim().Trim('"');
        if (!File.Exists(input) || string.IsNullOrWhiteSpace(output))
        {
            _localMediaStatus.Text = "Проверьте исходный файл и путь результата.";
            return;
        }
        if (!_operations.TryBegin()) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
        var outcome = OperationOutcome.Failed;
        using var job = DiagnosticHub.Begin(text ? "ui.transcription" : "ui.audio");
        _operation = operation;
        SetOperationControls(true);
        var components = Volatile.Read(ref _componentServices);
        _localMediaStatus.Text = text ? "Распознаю речь локально…" : "Извлекаю и проверяю MP3…";
        try
        {
            var runner = new ProcessRunner();
            if (text)
            {
                var language = (_languageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
                var result = await components.Transcriber.TranscribeAsync(input, output,
                    _whisperExeBox.Text.Trim().Trim('"'), _whisperModelBox.Text.Trim().Trim('"'), language, operation.Token);
                _localMediaStatus.Text = result.Message + (result.Success ? "\n" + result.TextPath + "\n" + result.SubtitlesPath : "");
                operation.Token.ThrowIfCancellationRequested();
                outcome = result.Success ? OperationOutcome.Succeeded : OperationOutcome.Failed;
                job.Complete(result.Success);
            }
            else
            {
                var result = await new FfmpegAudioExtractor(runner, components.Tools).ExtractAsync(input, output + ".mp3", operation.Token);
                _localMediaStatus.Text = result.Message + (result.Success ? "\n" + result.OutputPath : "");
                operation.Token.ThrowIfCancellationRequested();
                outcome = result.Success ? OperationOutcome.Succeeded : OperationOutcome.Failed;
                job.Complete(result.Success);
            }
        }
        catch (OperationCanceledException) { outcome = OperationOutcome.Cancelled; job.Cancel(); _localMediaStatus.Text = "Обработка отменена."; }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write("ui.media", "failed", ex.Message, jobId: job.Id);
            _localMediaStatus.Text = SensitiveDataRedactor.Redact(ex.Message);
        }
        finally
        {
            CompleteOperation(_operations.Complete(outcome));
        }
    }

    private Border BuildTranscriptionSettingsCard()
    {
        var panel = Vertical(12);
        panel.Children.Add(SectionHeading("Локальное распознавание речи"));
        _whisperExeBox = new TextBox { Header = "Путь к whisper-cli.exe", Text = _preferences.WhisperExecutable };
        _whisperModelBox = new TextBox { Header = "Путь к модели ggml (.bin)", Text = _preferences.WhisperModel };
        AttachPasteContextMenu(_whisperExeBox);
        AttachPasteContextMenu(_whisperModelBox);
        var exe = SecondaryButton("Выбрать EXE…");
        var model = SecondaryButton("Выбрать модель…");
        exe.Click += async (_, _) => await PickToolAsync(_whisperExeBox, ".exe");
        model.Click += async (_, _) => await PickToolAsync(_whisperModelBox, ".bin");
        _languageBox = new ComboBox { Header = "Язык речи", HorizontalAlignment = HorizontalAlignment.Stretch };
        _languageBox.Items.Add(ComboItem("Определять автоматически", "auto"));
        _languageBox.Items.Add(ComboItem("Русский", "ru"));
        _languageBox.Items.Add(ComboItem("Английский", "en"));
        _languageBox.SelectedIndex = _preferences.Language == "ru" ? 1 : _preferences.Language == "en" ? 2 : 0;
        var save = PrimaryButton("Сохранить настройки");
        save.Click += (_, _) => SavePreferences();
        panel.Children.Add(TwoColumn(_whisperExeBox, exe, true));
        panel.Children.Add(TwoColumn(_whisperModelBox, model, true));
        panel.Children.Add(_languageBox);
        panel.Children.Add(MutedText("Модель устанавливается отдельно. Для русского языка нужна многоязычная модель, не .en. Результат распознавания требует проверки человеком."));
        panel.Children.Add(save);
        return Card(panel);
    }

    private async Task PickToolAsync(TextBox target, string extension)
    {
        try
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(extension); InitializePicker(picker);
            var file = await picker.PickSingleFileAsync();
            if (file is not null) target.Text = file.Path;
        }
        catch (Exception ex) { _logStatus.Text = SensitiveDataRedactor.Redact(ex.Message); }
    }

    private static UiPreferences ReadPreferences()
    {
        try
        {
            if (File.Exists(PreferencesPath) && new FileInfo(PreferencesPath).Length <= 65536)
                return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(PreferencesPath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            DiagnosticHub.Log.Write("preferences", "failed", "Cannot read saved preferences: " + ex.GetType().Name);
        }
        return new();
    }

    private void SavePreferences()
    {
        try
        {
            _preferences.WhisperExecutable = _whisperExeBox.Text.Trim().Trim('"');
            _preferences.WhisperModel = _whisperModelBox.Text.Trim().Trim('"');
            _preferences.Language = (_languageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
            _preferences.LogMode = DiagnosticHub.Log.Mode;
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true }));
            _logStatus.Text = "Настройки сохранены. Пароли и cookies в настройки не записываются.";
        }
        catch (Exception ex) { _logStatus.Text = "Не удалось сохранить настройки: " + SensitiveDataRedactor.Redact(ex.Message); }
    }
}
