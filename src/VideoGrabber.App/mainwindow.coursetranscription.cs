using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Processes;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private readonly object _courseTranscriptionGate = new();
    private readonly Queue<string> _courseTranscriptionQueue = new();
    private readonly HashSet<string> _courseTranscriptionKnown =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _courseTranscriptionCancellation;
    private Task? _courseTranscriptionWorkerTask;
    private bool _courseTranscriptionActive;
    private int _courseTranscriptionTotal;
    private int _courseTranscriptionCompleted;
    private int _courseTranscriptionFailed;
    private double _courseTranscriptionPercent;
    private string? _courseTranscriptionCurrentMedia;
    private System.Diagnostics.Stopwatch? _courseTranscriptionActiveStopwatch;
    private int _courseTranscriptionCurrentOrdinal;
    private int _courseTranscriptionCurrentAttempt;

    private Grid _courseTranscriptionProgressTrack = null!;
    private Border _courseTranscriptionProgressFill = null!;
    private TextBlock _courseTranscriptionProgressPercent = null!;
    private TextBlock _courseTranscriptionStageText = null!;
    private TextBlock _courseTranscriptionCurrentText = null!;

    private static readonly HashSet<string> CourseTranscribableVideoExtensions =
        new(
            [".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v"],
            StringComparer.OrdinalIgnoreCase);

    private bool IsCourseTranscriptionBusy
    {
        get
        {
            lock (_courseTranscriptionGate)
                return _courseTranscriptionActive
                    || _courseTranscriptionQueue.Count > 0
                    || _courseTranscriptionWorkerTask is { IsCompleted: false };
        }
    }

    private int CourseTranscriptionFailureCount
    {
        get
        {
            lock (_courseTranscriptionGate)
                return _courseTranscriptionFailed;
        }
    }

    private void InitializeCourseTranscriptionPipeline(
        string courseRoot,
        CancellationToken courseToken)
    {
        CancelCourseTranscription();

        lock (_courseTranscriptionGate)
        {
            _courseTranscriptionQueue.Clear();
            _courseTranscriptionKnown.Clear();
            _courseTranscriptionTotal = 0;
            _courseTranscriptionCompleted = 0;
            _courseTranscriptionFailed = 0;
            _courseTranscriptionActive = false;
            _courseTranscriptionWorkerTask = null;
            _courseTranscriptionCurrentMedia = null;
            _courseTranscriptionActiveStopwatch = null;
            _courseTranscriptionCurrentOrdinal = 0;
            _courseTranscriptionCurrentAttempt = 0;
            _courseTranscriptionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    _windowLifetime.Token,
                    courseToken);
        }

        if (Directory.Exists(courseRoot))
        {
            foreach (var path in Directory
                         .EnumerateFiles(
                             courseRoot,
                             "*",
                             SearchOption.AllDirectories)
                         .Where(IsCourseTranscribableVideo)
                         .OrderBy(
                             path => path,
                             StringComparer.OrdinalIgnoreCase))
                EnqueueCourseTranscription(path, startWorker: false);
        }

        UpdateCourseTranscriptionUi();
        EnsureCourseTranscriptionWorker();
    }

    private static bool IsCourseTranscribableVideo(string path)
    {
        try
        {
            if (!File.Exists(path)
                || new FileInfo(path).Length <= 0
                || !CourseTranscribableVideoExtensions.Contains(
                    Path.GetExtension(path)))
                return false;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            return !directory.Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                .Any(segment =>
                    segment.StartsWith(
                        ".vg-",
                        StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return false;
        }
    }

    private static string CourseTranscriptPath(string mediaPath)
        => Path.Combine(
            Path.GetDirectoryName(mediaPath)!,
            Path.GetFileNameWithoutExtension(mediaPath) + ".txt");

    private static bool CourseTranscriptLooksReady(string mediaPath)
    {
        try
        {
            var path = CourseTranscriptPath(mediaPath);
            if (!File.Exists(path))
                return false;
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > TranscriptTextValidator.MaxUtf8Bytes)
                return false;
            var text = File.ReadAllText(path, Encoding.UTF8);
            return TranscriptTextValidator.TryValidate(text, out _);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or DecoderFallbackException)
        {
            return false;
        }
    }

    private void EnqueueCourseTranscription(
        string mediaPath,
        bool startWorker = true)
    {
        if (!IsCourseTranscribableVideo(mediaPath))
            return;

        var fullPath = Path.GetFullPath(mediaPath);
        bool shouldStart;
        lock (_courseTranscriptionGate)
        {
            if (!_courseTranscriptionKnown.Add(fullPath))
                return;

            _courseTranscriptionTotal++;
            if (CourseTranscriptLooksReady(fullPath))
            {
                _courseTranscriptionCompleted++;
                shouldStart = false;
            }
            else
            {
                _courseTranscriptionQueue.Enqueue(fullPath);
                shouldStart = true;
            }
        }

        UpdateCourseTranscriptionUi();
        if (shouldStart && startWorker)
            EnsureCourseTranscriptionWorker();
    }

    private void EnsureCourseTranscriptionWorker()
    {
        CancellationToken token;
        lock (_courseTranscriptionGate)
        {
            if (_courseTranscriptionCancellation is null
                || _courseTranscriptionCancellation.IsCancellationRequested)
                return;
            if (_courseTranscriptionWorkerTask is { IsCompleted: false })
                return;

            token = _courseTranscriptionCancellation.Token;
            _courseTranscriptionWorkerTask =
                Task.Run(
                    () => RunCourseTranscriptionWorkerAsync(token),
                    CancellationToken.None);
        }
    }

    private async Task RunCourseTranscriptionWorkerAsync(
        CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();

            string? mediaPath;
            lock (_courseTranscriptionGate)
            {
                if (_courseTranscriptionQueue.Count == 0)
                {
                    _courseTranscriptionActive = false;
                    mediaPath = null;
                }
                else
                {
                    mediaPath = _courseTranscriptionQueue.Dequeue();
                    _courseTranscriptionActive = true;
                    _courseTranscriptionCurrentMedia = mediaPath;
                    _courseTranscriptionActiveStopwatch =
                        System.Diagnostics.Stopwatch.StartNew();
                    if (ProcessPauseRegistry.IsPaused)
                        _courseTranscriptionActiveStopwatch.Stop();
                    _courseTranscriptionCurrentOrdinal =
                        _courseTranscriptionCompleted
                        + _courseTranscriptionFailed
                        + 1;
                    _courseTranscriptionCurrentAttempt = 1;
                }
            }

            if (mediaPath is null)
            {
                UpdateCourseTranscriptionUi();
                return;
            }

            UpdateCourseTranscriptionUi();

            var success = false;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                token.ThrowIfCancellationRequested();
                lock (_courseTranscriptionGate)
                    _courseTranscriptionCurrentAttempt = attempt;
                UpdateCourseTranscriptionUi();
                if (CourseTranscriptLooksReady(mediaPath))
                {
                    success = true;
                    break;
                }

                try
                {
                    success = await TranscribeCourseVideoToTextAsync(
                        mediaPath,
                        token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    DiagnosticHub.Log.Write(
                        "course.transcription",
                        "failed",
                        ex.GetType().Name);
                    success = false;
                }

                if (success)
                    break;

                if (attempt < 2)
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        token);
            }

            lock (_courseTranscriptionGate)
            {
                if (success)
                    _courseTranscriptionCompleted++;
                else
                    _courseTranscriptionFailed++;
                _courseTranscriptionCurrentMedia = null;
                _courseTranscriptionActiveStopwatch?.Stop();
                _courseTranscriptionActiveStopwatch = null;
                _courseTranscriptionCurrentOrdinal = 0;
                _courseTranscriptionCurrentAttempt = 0;
            }

            DiagnosticHub.Log.Write(
                "course.transcription",
                success ? "succeeded" : "failed",
                "file=" + Path.GetFileName(mediaPath));
            UpdateCourseTranscriptionUi();
        }
    }

    private string ResolveCourseTranscriptionLanguage(string mediaPath)
    {
        var configured = string.IsNullOrWhiteSpace(_preferences.Language)
            ? "auto"
            : _preferences.Language.Trim();

        if (!configured.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return configured;

        var evidence = string.Join(
            " ",
            _cachedCoursePlan?.CourseTitle ?? string.Empty,
            _cachedCourseRootFolder ?? string.Empty,
            Path.GetFileName(mediaPath));

        return evidence.Any(ch => ch is >= '\u0400' and <= '\u04FF')
            ? "ru"
            : "auto";
    }

    private async Task<bool> TranscribeCourseVideoToTextAsync(
        string mediaPath,
        CancellationToken token)
    {
        var desiredText = CourseTranscriptPath(mediaPath);
        if (CourseTranscriptLooksReady(mediaPath))
            return true;

        var components = Volatile.Read(ref _componentServices);
        if (!components.Tools.WhisperAvailable)
        {
            DiagnosticHub.Log.Write(
                "course.transcription",
                "failed",
                "Bundled Whisper is unavailable");
            return false;
        }

        var directory = Path.GetDirectoryName(mediaPath)!;
        var temporaryBase = Path.Combine(
            directory,
            ".vg-course-transcript-"
            + Guid.NewGuid().ToString("N"));
        var language = ResolveCourseTranscriptionLanguage(mediaPath);

        string? generatedText = null;
        string? generatedSrt = null;
        try
        {
            var result = await components.Transcriber.TranscribeAsync(
                mediaPath,
                temporaryBase,
                components.Tools.WhisperCli,
                components.Tools.WhisperModel,
                language,
                token);

            if (!result.Success
                || string.IsNullOrWhiteSpace(result.TextPath)
                || !File.Exists(result.TextPath)
                || new FileInfo(result.TextPath).Length <= 0)
            {
                DiagnosticHub.Log.Write(
                    "course.transcription",
                    "failed",
                    result.Message);
                return false;
            }

            generatedText = result.TextPath;
            generatedSrt = result.SubtitlesPath;

            token.ThrowIfCancellationRequested();
            if (CourseTranscriptLooksReady(mediaPath))
                return true;

            File.Move(
                generatedText,
                desiredText,
                overwrite: true);
            generatedText = null;

            return File.Exists(desiredText)
                && new FileInfo(desiredText).Length > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            DiagnosticHub.Log.Write(
                "course.transcription",
                "failed",
                ex.GetType().Name);
            return false;
        }
        finally
        {
            foreach (var path in new[]
                     {
                         generatedText,
                         generatedSrt,
                         temporaryBase + ".txt",
                         temporaryBase + ".srt"
                     }
                     .Where(path =>
                         !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex) when (
                    ex is IOException
                        or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private async Task WaitForCourseTranscriptionAsync(
        CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Task? worker;
            int queued;
            bool active;
            lock (_courseTranscriptionGate)
            {
                worker = _courseTranscriptionWorkerTask;
                queued = _courseTranscriptionQueue.Count;
                active = _courseTranscriptionActive;
            }

            if (queued == 0
                && !active
                && (worker is null || worker.IsCompleted))
                break;

            if (worker is null)
            {
                EnsureCourseTranscriptionWorker();
                await Task.Delay(100, token);
                continue;
            }

            await worker.WaitAsync(token);
        }

        UpdateCourseTranscriptionUi();
    }

    private void CancelCourseTranscription()
    {
        CancellationTokenSource? cancellation;
        lock (_courseTranscriptionGate)
        {
            cancellation = _courseTranscriptionCancellation;
            _courseTranscriptionCancellation = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }


    private string SetCourseTranscriptionPauseState(bool paused)
    {
        lock (_courseTranscriptionGate)
        {
            if (_courseTranscriptionActiveStopwatch is not null)
            {
                if (paused)
                    _courseTranscriptionActiveStopwatch.Stop();
                else if (_courseTranscriptionActive)
                    _courseTranscriptionActiveStopwatch.Start();
            }

            return
                $"active={_courseTranscriptionActive} "
                + $"ordinal={_courseTranscriptionCurrentOrdinal}/{_courseTranscriptionTotal} "
                + $"queued={_courseTranscriptionQueue.Count}";
        }
    }

    private void UpdateCourseTranscriptionSelectionUi()
    {
        if (_courseTranscriptionStageText is null
            || _courseTranscriptionCurrentText is null
            || _courseTranscriptionProgressPercent is null
            || _courseTranscriptionProgressTrack is null
            || _courseTranscriptionProgressFill is null
            || IsCourseTranscriptionBusy)
            return;

        if (_courseTranscriptionCheckBox?.IsChecked == true)
        {
            _courseTranscriptionStageText.Text =
                "Фоновая транскрибация включена для следующего запуска";
            _courseTranscriptionCurrentText.Text =
                "После загрузки ролика Whisper создаст рядом TXT с таким же именем. Скачивание продолжится параллельно.";
        }
        else
        {
            _courseTranscriptionStageText.Text =
                "Фоновая транскрибация курса выключена";
            _courseTranscriptionCurrentText.Text =
                "Поставьте галочку «Транскрибировать видео курса в TXT», если нужны текстовые файлы рядом с видео.";
        }

        _courseTranscriptionPercent = 0;
        _courseTranscriptionProgressPercent.Text = "0%";
        _courseTranscriptionProgressFill.Width = 0;
    }

    private void UpdateCourseTranscriptionUi()
    {
        if (DispatcherQueue is null)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_courseTranscriptionStageText is null
                || _courseTranscriptionCurrentText is null
                || _courseTranscriptionProgressPercent is null
                || _courseTranscriptionProgressTrack is null
                || _courseTranscriptionProgressFill is null)
                return;

            int total;
            int completed;
            int failed;
            int queued;
            bool active;
            string? activeMedia;
            TimeSpan activeElapsed;
            int activeOrdinal;
            int activeAttempt;
            lock (_courseTranscriptionGate)
            {
                total = _courseTranscriptionTotal;
                completed = _courseTranscriptionCompleted;
                failed = _courseTranscriptionFailed;
                queued = _courseTranscriptionQueue.Count;
                active = _courseTranscriptionActive;
                activeMedia = _courseTranscriptionCurrentMedia;
                activeElapsed = _courseTranscriptionActiveStopwatch?.Elapsed
                    ?? TimeSpan.Zero;
                activeOrdinal = _courseTranscriptionCurrentOrdinal;
                activeAttempt = _courseTranscriptionCurrentAttempt;
            }

            var finished = completed + failed;
            _courseTranscriptionPercent = total == 0
                ? 0
                : Math.Clamp(
                    100d * finished / total,
                    0,
                    100);
            _courseTranscriptionProgressFill.Width =
                _courseTranscriptionProgressTrack.ActualWidth
                * _courseTranscriptionPercent
                / 100d;
            _courseTranscriptionProgressPercent.Text =
                total == 0
                    ? "0%"
                    : $"{_courseTranscriptionPercent:0.0}% · текстов {completed}/{total}"
                      + (active && activeOrdinal > 0 ? $" · сейчас {activeOrdinal}/{total}" : string.Empty)
                      + (failed > 0 ? $" · ошибок {failed}" : string.Empty);

            if (total == 0)
            {
                _courseTranscriptionStageText.Text =
                    "Фоновая транскрибация — ожидаю первое видео";
                _courseTranscriptionCurrentText.Text =
                    "После загрузки ролика Whisper автоматически создаст рядом TXT с таким же именем.";
            }
            else if (active || queued > 0)
            {
                _courseTranscriptionStageText.Text =
                    "Фоновая транскрибация курса";
                var elapsedText =
                    $"{(int)activeElapsed.TotalHours:00}:{activeElapsed.Minutes:00}:{activeElapsed.Seconds:00}";
                _courseTranscriptionCurrentText.Text =
                    (!string.IsNullOrWhiteSpace(activeMedia)
                        ? $"Видео {Math.Max(1, activeOrdinal)} из {total} · попытка {Math.Max(1, activeAttempt)}/2"
                          + Environment.NewLine
                          + "Сейчас: " + Path.GetFileName(activeMedia)
                          + Environment.NewLine
                          + "Прошло: " + elapsedText
                        : "Формирую и запускаю очередь по порядку.")
                    + Environment.NewLine
                    + $"В очереди: {queued}. Скачивание курса продолжается параллельно.";
            }
            else if (failed == 0)
            {
                _courseTranscriptionStageText.Text =
                    "Транскрибация курса завершена";
                _courseTranscriptionCurrentText.Text =
                    $"Готово текстов: {completed}/{total}. TXT лежат рядом с соответствующими видео.";
            }
            else
            {
                _courseTranscriptionStageText.Text =
                    "Транскрибация завершена с ошибками";
                _courseTranscriptionCurrentText.Text =
                    $"Готово: {completed}/{total}. Не удалось: {failed}. При следующем «Продолжить» отсутствующие TXT будут поставлены в очередь снова.";
            }

            UpdatePauseButtonsAvailability(
                _operations.IsBusy
                || _courseDownloadActive
                || _courseTranscriptionActive
                || queued > 0
                || _isInstallingComponents);
        });
    }
}
