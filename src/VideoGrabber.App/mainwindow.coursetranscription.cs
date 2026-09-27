using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoGrabber.Infrastructure.Diagnostics;

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
                EnqueueCourseTranscription(path);
        }

        UpdateCourseTranscriptionUi();
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
            return File.Exists(path)
                && new FileInfo(path).Length > 0;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return false;
        }
    }

    private void EnqueueCourseTranscription(string mediaPath)
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
        if (shouldStart)
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
                }
            }

            if (mediaPath is null)
            {
                UpdateCourseTranscriptionUi();
                return;
            }

            UpdateCourseTranscriptionUi(mediaPath);

            var success = false;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                token.ThrowIfCancellationRequested();
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
            }

            DiagnosticHub.Log.Write(
                "course.transcription",
                success ? "succeeded" : "failed",
                "file=" + Path.GetFileName(mediaPath));
            UpdateCourseTranscriptionUi(mediaPath);
        }
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
        var language = string.IsNullOrWhiteSpace(_preferences.Language)
            ? "auto"
            : _preferences.Language;

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
            if (File.Exists(desiredText))
            {
                if (new FileInfo(desiredText).Length > 0)
                    return true;
                File.Delete(desiredText);
            }

            File.Move(
                generatedText,
                desiredText,
                overwrite: false);
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

    private void UpdateCourseTranscriptionUi(
        string? currentMedia = null)
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
            lock (_courseTranscriptionGate)
            {
                total = _courseTranscriptionTotal;
                completed = _courseTranscriptionCompleted;
                failed = _courseTranscriptionFailed;
                queued = _courseTranscriptionQueue.Count;
                active = _courseTranscriptionActive;
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
                _courseTranscriptionCurrentText.Text =
                    (string.IsNullOrWhiteSpace(currentMedia)
                        ? "Обрабатываю очередь по порядку."
                        : "Сейчас: "
                          + Path.GetFileName(currentMedia))
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
