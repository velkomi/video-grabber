using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Media;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private Button _courseDownloadButton = null!;
    private Button _courseResumeButton = null!;
    private Button _courseClearCacheButton = null!;
    private ComboBox _courseQualityBox = null!;
    private Grid _courseProgressTrack = null!;
    private Border _courseProgressFill = null!;
    private TextBlock _courseProgressPercent = null!;
    private double _courseOverallPercent;
    private TextBlock _courseStageText = null!;
    private TextBlock _courseCurrentText = null!;
    private TextBlock _courseEtaText = null!;
    private TextBlock _courseElapsedText = null!;
    private Border _courseNetworkWarning = null!;
    private TextBlock _courseNetworkWarningText = null!;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _courseElapsedTimer;
    private DateTimeOffset _courseProcessStartedUtc;
    private Button _transcribeDownloadedButton = null!;
    private CancellationTokenSource? _courseCancellation;
    private bool _courseDownloadActive;
    private bool _courseResumeAvailable;
    private GetCourseCoursePlan? _cachedCoursePlan;
    private string? _cachedCourseRootFolder;
    private Uri? _cachedCourseRoot;
    private DateTimeOffset _courseStateCreatedUtc;
    private string _courseActiveQuality = "best";
    private int _courseResumeLessonIndex;
    private readonly HashSet<string> _courseCompletedLessons = new(StringComparer.Ordinal);
    private DateTimeOffset _courseWorkStartedUtc;
    private int _courseCompletedAtRunStart;
    private int _courseCurrentLessonIndex;
    private int _courseTotalLessons;
    private string? _courseCurrentVideoName;
    private string? _lastDownloadedMediaPath;

    private void RegisterDownloadedMedia(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        _lastDownloadedMediaPath = path;
        if (_transcribeDownloadedButton is not null)
        {
            _transcribeDownloadedButton.IsEnabled =
                !_operations.IsBusy && !_courseDownloadActive;
            _transcribeDownloadedButton.Content =
                "Транскрибировать скачанное";
        }
    }

    private async Task TranscribeLastDownloadedAsync()
    {
        if (!await EnsureFeatureAccessAsync(
                FeatureAccessKind.PaidTools,
                "Транскрибация доступна на платных тарифах"))
            return;

        var path = _lastDownloadedMediaPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _browserHint.Text = "Сначала успешно скачайте видео.";
            await ExplainMissingDownloadedMediaAsync();
            return;
        }

        _localMediaBox.Text = path;
        _localOutputBaseBox.Text = Path.Combine(
            Path.GetDirectoryName(path)!,
            Path.GetFileNameWithoutExtension(path) + "-text");
        await RunLocalMediaAsync(text: true);
    }

    private Task DownloadWholeGetCourseAsync()
        => RunManagedWholeCourseAsync(resume: false);

    private async Task ResumeWholeGetCourseAsync()
    {
        if (_cachedCoursePlan is null
            || _cachedCourseRootFolder is null
            || _cachedCourseRoot is null)
        {
            if (!await LoadCourseResumeProjectAsync())
                return;
        }

        await RunManagedWholeCourseAsync(resume: true);
    }

    private async Task RunManagedWholeCourseAsync(bool resume)
    {
        if (!await EnsureFeatureAccessAsync(
                FeatureAccessKind.FullCourse,
                "Скачивание полного курса доступно на Full Course"))
            return;

        var source = (resume ? _cachedCourseRoot : _browserPageUri)?.AbsoluteUri ?? "course";
        var operation = CreateLocalOperation(
            "course_download",
            source,
            SelectedCourseQuality(),
            resume ? "resume" : "new");
        try
        {
            await _managedCoordinator.RunAsync(
                operation,
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    await RunWholeGetCourseAsync(resume);
                    return OperationOutcome.Succeeded;
                },
                ManagedReport,
                _windowLifetime.Token);
        }
        catch (UnauthorizedAccessException ex)
        {
            _browserHint.Text = ex.Message == "managed_sign_in_required"
                ? "Сначала войдите в VideoGrabber-аккаунт. Полный курс доступен только после авторизации и на тарифе Full Course."
                : "Скачивание полного курса недоступно на текущем тарифе. Нужен Full Course.";
            await ShowFeatureAccessDialogAsync(
                FeatureAccessKind.FullCourse,
                "Скачивание полного курса недоступно",
                ex.Message);
        }
        catch (OperationCanceledException) { }
    }

    private async Task<bool> LoadCourseResumeProjectAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return false;

        var selectedRoot = Path.GetFullPath(folder.Path);
        try
        {
            var resolved = await ResolveCourseResumeStateAsync(
                selectedRoot,
                _windowLifetime.Token);
            if (resolved is not null)
            {
                ApplyLoadedCourseState(
                    resolved.Value.State,
                    resolved.Value.Root);
                _browserHint.Text =
                    $"Проект курса восстановлен с диска. Готово уроков: {_courseCompletedLessons.Count}/{_cachedCoursePlan!.Lessons.Length}.";
                UpdateCourseControls();
                return true;
            }
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException)
        {
            _browserHint.Text =
                "Не удалось прочитать проект курса: "
                + VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                    ex.Message);
            return false;
        }

        if (_mediaBrowser?.CoreWebView2 is null
            || _browserPageUri is null)
        {
            _browserHint.Text =
                "В выбранной папке ещё нет VideoGrabber.course.json. " +
                "Откройте исходную страницу курса во встроенном браузере, войдите на сайт и снова нажмите «Продолжить».";
            return false;
        }

        return await BootstrapCourseResumeProjectAsync(
            selectedRoot);
    }

    private async Task<(CourseDownloadState State, string Root)?>
        ResolveCourseResumeStateAsync(
            string selectedRoot,
            CancellationToken token)
    {
        var requested = RequestedCourseRoot();
        var candidates = new List<string>();

        void AddIfStateExists(string root)
        {
            if (File.Exists(
                    CourseDownloadStateStore.StatePath(root)))
                candidates.Add(root);
        }

        AddIfStateExists(selectedRoot);

        try
        {
            foreach (var child in Directory.EnumerateDirectories(
                         selectedRoot,
                         "*",
                         SearchOption.TopDirectoryOnly)
                     .Take(100))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if ((File.GetAttributes(child)
                        & FileAttributes.ReparsePoint) != 0)
                        continue;
                    AddIfStateExists(child);
                }
                catch (Exception ex) when (
                    ex is IOException
                        or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
        }

        if (candidates.Count == 0)
            return null;

        CourseDownloadState? firstValid = null;
        string? firstValidRoot = null;
        foreach (var root in candidates.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var state = await CourseDownloadStateStore.LoadAsync(
                root,
                token);
            firstValid ??= state;
            firstValidRoot ??= root;

            if (requested is null)
                continue;

            var stateRoot = new Uri(
                state.RootUrl,
                UriKind.Absolute);
            if (string.Equals(
                    GetCourseCourseStructure.CanonicalKey(stateRoot),
                    GetCourseCourseStructure.CanonicalKey(requested),
                    StringComparison.Ordinal))
                return (state, root);
        }

        if (requested is null
            && firstValid is not null
            && firstValidRoot is not null)
            return (firstValid, firstValidRoot);

        if (requested is not null)
            throw new InvalidDataException(
                "Выбрана папка другого курса. Откройте нужный курс во встроенном браузере и выберите его собственную папку; VideoGrabber не будет продолжать чужой проект.");

        return null;
    }

    private Uri? RequestedCourseRoot()
    {
        if (_urlBox is not null
            && UrlPolicy.TryValidate(
                _urlBox.Text,
                out var fromInput,
                out _)
            && fromInput is not null
            && GetCourseCourseStructure.IsTrainingUri(fromInput))
            return fromInput;

        if (_browserPageUri is { } browser
            && GetCourseCourseStructure.IsTrainingUri(browser))
            return browser;

        return null;
    }

    private async Task<bool> BootstrapCourseResumeProjectAsync(
        string selectedRoot)
    {
        if (_courseDownloadActive || _browserPageUri is null)
            return false;

        SetCourseQualitySelection(_courseActiveQuality);

        var originalCookieIndex = _cookiesBox.SelectedIndex;
        SelectEmbeddedBrowserSession();
        _courseDownloadActive = true;
        _courseCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                _windowLifetime.Token);
        _courseProcessStartedUtc = DateTimeOffset.UtcNow;
        _courseWorkStartedUtc = DateTimeOffset.UtcNow;
        StartCourseElapsedTimer();
        UpdateCourseElapsed();
        UpdateCourseControls();

        try
        {
            var token = _courseCancellation.Token;
            BeginCourseStructureProgress();
            _browserHint.Text =
                "Старый архив найден без файла состояния. Один раз перечитываю только структуру курса — существующие файлы скачиваться заново не будут.";

            var plan = await BuildCoursePlanAsync(
                _browserPageUri,
                token);
            if (plan.Lessons.Length == 0)
            {
                _browserHint.Text =
                    "Не удалось восстановить план курса: уроки не найдены.";
                return false;
            }

            _cachedCoursePlan = plan;
            _cachedCourseRoot = plan.Root;
            _cachedCourseRootFolder =
                Path.GetFullPath(selectedRoot);
            var createdUtc = Directory.GetCreationTimeUtc(
                _cachedCourseRootFolder);
            _courseStateCreatedUtc =
                createdUtc > DateTime.UnixEpoch
                    && createdUtc <= DateTime.UtcNow
                    ? new DateTimeOffset(createdUtc, TimeSpan.Zero)
                    : DateTimeOffset.UtcNow;
            _courseProcessStartedUtc = _courseStateCreatedUtc;

            _courseCompletedLessons.Clear();
            ReconcileCompletedLessonsFromDisk(
                plan,
                _cachedCourseRootFolder);
            _courseResumeLessonIndex =
                FirstIncompleteLessonIndex(plan);
            _courseTotalLessons = plan.Lessons.Length;
            _courseResumeAvailable =
                _courseResumeLessonIndex < plan.Lessons.Length;

            await PersistCourseStateAsync(token);
            _browserHint.Text =
                $"Структура восстановлена: {plan.Lessons.Length} уроков. " +
                "Сейчас продолжу с проверкой уже имеющихся файлов.";
            return true;
        }
        catch (OperationCanceledException)
        {
            _browserHint.Text =
                "Восстановление структуры курса отменено.";
            return false;
        }
        catch (Exception ex)
        {
            _browserHint.Text =
                "Не удалось восстановить проект курса: "
                + VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                    ex.Message);
            return false;
        }
        finally
        {
            StopCourseElapsedTimer();
            UpdateCourseElapsed();
            ResetPauseState();
            _courseCancellation?.Dispose();
            _courseCancellation = null;
            _courseDownloadActive = false;
            _browserSession.RunProgrammaticSelectionCleanup(
                () => _cookiesBox.SelectedIndex = originalCookieIndex);
            UpdateCourseControls();
        }
    }

    private void ApplyLoadedCourseState(
        CourseDownloadState state,
        string courseRoot)
    {
        var plan = CourseDownloadStateStore.ToPlan(state);
        _cachedCoursePlan = plan;
        _cachedCourseRoot = plan.Root;
        _cachedCourseRootFolder =
            Path.GetFullPath(courseRoot);
        if (_urlBox is not null)
            _urlBox.Text = plan.Root.AbsoluteUri;
        if (_outputFolderBox is not null
            && Path.GetDirectoryName(_cachedCourseRootFolder) is { } parent)
            _outputFolderBox.Text = parent;
        _courseStateCreatedUtc = state.CreatedUtc;
        _courseProcessStartedUtc = state.CreatedUtc;
        _courseActiveQuality = string.IsNullOrWhiteSpace(state.Quality) ? "best" : state.Quality;
        SetCourseQualitySelection(_courseActiveQuality);
        _courseCompletedLessons.Clear();

        var knownCompleted = new HashSet<string>(
            state.CompletedLessonKeys,
            StringComparer.Ordinal);
        foreach (var lesson in plan.Lessons)
        {
            var key =
                GetCourseCourseStructure.CanonicalKey(
                    lesson.Uri);
            if ((knownCompleted.Contains(key)
                    || CourseLessonLooksCompleteOnDisk(
                        _cachedCourseRootFolder,
                        lesson))
                && CourseLessonLooksCompleteOnDisk(
                    _cachedCourseRootFolder,
                    lesson))
                _courseCompletedLessons.Add(key);
        }

        _courseResumeLessonIndex =
            FirstIncompleteLessonIndex(plan);
        _courseTotalLessons =
            plan.Lessons.Length;
        _courseResumeAvailable =
            _courseResumeLessonIndex
                < plan.Lessons.Length;
    }

    private async Task InitializeCourseStateForPlanAsync(
        GetCourseCoursePlan plan,
        string courseRoot,
        CancellationToken token)
    {
        _cachedCoursePlan = plan;
        _cachedCourseRoot = plan.Root;
        _cachedCourseRootFolder =
            Path.GetFullPath(courseRoot);
        _courseCompletedLessons.Clear();
        _courseResumeLessonIndex = 0;
        _courseStateCreatedUtc =
            DateTimeOffset.UtcNow;

        var statePath =
            CourseDownloadStateStore.StatePath(
                _cachedCourseRootFolder);
        if (File.Exists(statePath))
        {
            try
            {
                var existing =
                    await CourseDownloadStateStore.LoadAsync(
                        _cachedCourseRootFolder,
                        token);
                var sameCourse =
                    string.Equals(
                        GetCourseCourseStructure.CanonicalKey(
                            new Uri(existing.RootUrl)),
                        GetCourseCourseStructure.CanonicalKey(
                            plan.Root),
                        StringComparison.Ordinal);
                if (sameCourse)
                {
                    _courseStateCreatedUtc =
                        existing.CreatedUtc;
                    var known = plan.Lessons.ToDictionary(
                        lesson =>
                            GetCourseCourseStructure.CanonicalKey(
                                lesson.Uri),
                        StringComparer.Ordinal);
                    foreach (var key in existing.CompletedLessonKeys)
                    {
                        if (known.TryGetValue(
                                key,
                                out var lesson)
                            && CourseLessonLooksCompleteOnDisk(
                                _cachedCourseRootFolder,
                                lesson))
                            _courseCompletedLessons.Add(key);
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException)
            {
                DiagnosticHub.Log.Write(
                    "course.state",
                    "failed",
                    "Existing state ignored: "
                    + ex.GetType().Name);
            }
        }

        ReconcileCompletedLessonsFromDisk(
            plan,
            _cachedCourseRootFolder);
        _courseResumeLessonIndex =
            FirstIncompleteLessonIndex(plan);
        _courseTotalLessons =
            plan.Lessons.Length;
        _courseResumeAvailable =
            _courseResumeLessonIndex
                < plan.Lessons.Length;

        await PersistCourseStateAsync(token);
    }

    private async Task PersistCourseStateAsync(
        CancellationToken token)
    {
        if (_cachedCoursePlan is null
            || string.IsNullOrWhiteSpace(
                _cachedCourseRootFolder))
            return;

        if (_courseStateCreatedUtc == default)
            _courseStateCreatedUtc =
                DateTimeOffset.UtcNow;

        var state = CourseDownloadStateStore.Create(
            _cachedCoursePlan,
            _courseCompletedLessons,
            _courseResumeLessonIndex,
            _courseStateCreatedUtc,
            _courseActiveQuality);
        await CourseDownloadStateStore.SaveAtomicAsync(
            _cachedCourseRootFolder,
            state,
            token);
        DiagnosticHub.Log.Write(
            "course.state",
            "succeeded",
            $"completed={_courseCompletedLessons.Count}/{_cachedCoursePlan.Lessons.Length} next={_courseResumeLessonIndex}");
    }

    private async Task PersistCourseStateSafeAsync()
    {
        try
        {
            await PersistCourseStateAsync(
                CancellationToken.None);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
        {
            DiagnosticHub.Log.Write(
                "course.state",
                "failed",
                ex.GetType().Name);
        }
    }

    private static string CourseLessonFolderPath(
        string courseRoot,
        GetCourseLessonPlan lesson)
    {
        var moduleFolder =
            lesson.ModuleFolders.Aggregate(
                Path.GetFullPath(courseRoot),
                Path.Combine);
        return Path.Combine(
            moduleFolder,
            CourseLessonArchive.LessonFolder(
                lesson.LessonOrdinal,
                lesson.Title));
    }

    private int FirstIncompleteLessonIndex(
        GetCourseCoursePlan plan)
    {
        var index = Array.FindIndex(
            plan.Lessons,
            lesson => !_courseCompletedLessons.Contains(
                GetCourseCourseStructure.CanonicalKey(
                    lesson.Uri)));
        return index < 0
            ? plan.Lessons.Length
            : index;
    }

    private void ReconcileCompletedLessonsFromDisk(
        GetCourseCoursePlan plan,
        string courseRoot)
    {
        var before = _courseCompletedLessons.Count;
        var diskCompleted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lesson in plan.Lessons)
        {
            if (!CourseLessonLooksCompleteOnDisk(
                    courseRoot,
                    lesson))
                continue;
            diskCompleted.Add(
                GetCourseCourseStructure.CanonicalKey(
                    lesson.Uri));
        }

        _courseCompletedLessons.Clear();
        _courseCompletedLessons.UnionWith(diskCompleted);

        DiagnosticHub.Log.Write(
            "course.state.reconcile",
            "succeeded",
            $"disk-complete={_courseCompletedLessons.Count}/{plan.Lessons.Length} " +
            $"previous={before} delta={_courseCompletedLessons.Count - before}");
    }

    private async Task<bool> FinalVerifyCourseAsync(
        GetCourseCoursePlan plan,
        string rootFolder,
        CancellationToken token)
    {
        await WaitIfPausedAsync(token);
        _courseStageText.Text = "Финальная перепроверка курса…";
        _browserHint.Text = "Перепроверяю все уроки, видео, изображения и вложения по сохранённым manifest-файлам.";

        var normalizedImages = NormalizeCourseImageExtensionsUnderRoot(rootFolder);
        var recovered = await RecoverCompletedCourseJobsAsync(rootFolder, token);
        _courseCompletedLessons.Clear();
        ReconcileCompletedLessonsFromDisk(plan, rootFolder);
        _courseResumeLessonIndex = FirstIncompleteLessonIndex(plan);
        await PersistCourseStateSafeAsync();

        if (_courseCompletedLessons.Count < plan.Lessons.Length)
        {
            var missing = plan.Lessons.Length - _courseCompletedLessons.Count;
            _courseStageText.Text = $"После перепроверки обнаружено незавершённых уроков: {missing}. Идёт повторное скачивание.";
            _courseCurrentText.Text = $"Первый незавершённый урок: {_courseResumeLessonIndex + 1}/{plan.Lessons.Length}.";
            _browserHint.Text = $"Финальная проверка нашла {missing} незавершённых уроков. VideoGrabber продолжает скачивание автоматически.";
            DiagnosticHub.Log.Write("course.final-verify", "observed",
                $"missing={missing} normalizedImages={normalizedImages} recovered={recovered}");
            return false;
        }

        var lessonEntries = plan.Lessons
            .Select(lesson => (
                LessonFolder: CourseLessonFolderPath(rootFolder, lesson),
                LessonUri: lesson.Uri))
            .ToArray();
        var lessonManifestsDeleted =
            await CourseLessonVerificationManifestStore.ConsolidateCourseAsync(
                rootFolder,
                lessonEntries,
                token);
        var removed = PurgeVerifiedCourseCache(rootFolder);
        DiagnosticHub.Log.Write("course.final-verify", "succeeded",
            $"lessons={plan.Lessons.Length} normalizedImages={normalizedImages} recovered={recovered} " +
            $"lessonManifestsDeleted={lessonManifestsDeleted} cacheRemoved={removed}");
        ClearCourseNetworkWarning();
        _browserHint.Text =
            $"Финальная проверка завершена: все {plan.Lessons.Length} уроков подтверждены. " +
            "Служебные VG.lesson.json собраны в один VG.verify.json в корне курса и удалены из папок уроков; временный кэш очищен.";
        return true;
    }

    private static int PurgeVerifiedCourseCache(string rootFolder)
    {
        if (!Directory.Exists(rootFolder)) return 0;
        var removed = 0;
        foreach (var job in Directory.EnumerateDirectories(rootFolder, ".vg-job-*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length).ToArray())
        {
            try
            {
                Directory.Delete(job, recursive: true);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        foreach (var file in Directory.EnumerateFiles(rootFolder, "*", SearchOption.AllDirectories).ToArray())
        {
            if (!IsCourseTemporaryFile(file)) continue;
            try { File.Delete(file); removed++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return removed;
    }
    private static bool CourseLessonLooksCompleteOnDisk(
        string courseRoot,
        GetCourseLessonPlan lesson)
        => CourseLessonLooksCompleteOnDisk(
            courseRoot,
            lesson,
            out _);

    private static bool CourseLessonLooksCompleteOnDisk(
        string courseRoot,
        GetCourseLessonPlan lesson,
        out string reason)
    {
        reason = "complete";
        var folder =
            CourseLessonFolderPath(
                courseRoot,
                lesson);
        if (!Directory.Exists(folder))
        {
            reason = "lesson-folder-missing";
            return false;
        }

        static bool NonEmpty(string path)
        {
            try
            {
                return File.Exists(path)
                    && new FileInfo(path).Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        if (!NonEmpty(Path.Combine(
                folder,
                "Урок.docx"))
            || !NonEmpty(Path.Combine(
                folder,
                "Страница.html")))
        {
            reason = "archive-file-missing-or-empty";
            return false;
        }

        try
        {
            if (Directory.EnumerateFiles(
                    folder,
                    "*",
                    SearchOption.AllDirectories)
                .Any(IsCourseTemporaryFile))
            {
                reason = "temporary-file-present";
                return false;
            }

            if (!CourseLessonVerificationManifestStore.TryLoadForLesson(
                    courseRoot,
                    folder,
                    lesson.Uri,
                    out var manifest)
                || manifest is null)
            {
                reason = "verification-manifest-missing";
                return false;
            }

            if (!Uri.TryCreate(manifest.LessonUrl, UriKind.Absolute, out var manifestLesson)
                || !string.Equals(
                    GetCourseCourseStructure.CanonicalKey(manifestLesson),
                    GetCourseCourseStructure.CanonicalKey(lesson.Uri),
                    StringComparison.Ordinal))
            {
                reason = "verification-manifest-mismatch";
                return false;
            }

            var mediaExtensions = new HashSet<string>(
                [".mp4", ".mkv", ".webm", ".mov", ".m4a", ".mp3", ".aac", ".opus", ".ts"],
                StringComparer.OrdinalIgnoreCase);
            var readyMedia = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => mediaExtensions.Contains(Path.GetExtension(path))
                    && new FileInfo(path).Length > 0)
                .ToArray();
            if (readyMedia.Length < manifest.ExpectedVideoCount)
            {
                reason = $"media-missing:{readyMedia.Length}/{manifest.ExpectedVideoCount}";
                return false;
            }

            if (manifest.ExpectedVideoCount > 1)
            {
                for (var ordinal = 1; ordinal <= manifest.ExpectedVideoCount; ordinal++)
                {
                    var marker = $" - Видео {ordinal:00} - ";
                    if (!readyMedia.Any(path =>
                            Path.GetFileNameWithoutExtension(path)
                                .Contains(marker, StringComparison.OrdinalIgnoreCase)))
                    {
                        reason = $"media-ordinal-missing:{ordinal}/{manifest.ExpectedVideoCount}";
                        return false;
                    }
                }
            }

            var readyAssets = 0;
            foreach (var assetFolderName in new[] { "Изображения", "Вложения" })
            {
                var assetFolder = Path.Combine(folder, assetFolderName);
                if (!Directory.Exists(assetFolder)) continue;
                readyAssets += Directory.EnumerateFiles(assetFolder, "*", SearchOption.TopDirectoryOnly)
                    .Count(path => !IsCourseTemporaryFile(path) && new FileInfo(path).Length > 0);
            }
            if (readyAssets < manifest.ExpectedAssetCount)
            {
                reason = $"asset-missing:{readyAssets}/{manifest.ExpectedAssetCount}";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            reason = "verification-read-error:" + ex.GetType().Name;
            return false;
        }
    }

    private async Task RunWholeGetCourseAsync(bool resume)
    {
        if (_courseDownloadActive) return;
        if (_mediaBrowser?.CoreWebView2 is null)
        {
            _browserHint.Text =
                "Сначала откройте курс во встроенном браузере и войдите на GetCourse.";
            return;
        }

        if (!resume
            && string.IsNullOrWhiteSpace(_outputFolderBox.Text))
        {
            _browserHint.Text = "Сначала выберите папку сохранения.";
            return;
        }

        if (resume && (_cachedCoursePlan is null
            || _cachedCourseRootFolder is null
            || _cachedCourseRoot is null))
        {
            _browserHint.Text =
                "Нет сохранённого плана курса для продолжения. Нажмите «Скачать весь курс».";
            _courseResumeAvailable = false;
            UpdateCourseControls();
            return;
        }

        var root = resume ? _cachedCourseRoot : _browserPageUri;
        if (root is null)
        {
            _browserHint.Text = "Страница курса ещё не открыта.";
            return;
        }

        if (!resume)
            _courseActiveQuality = SelectedCourseQuality();
        SetCourseQualitySelection(_courseActiveQuality);

        var originalCookieIndex = _cookiesBox.SelectedIndex;
        SelectEmbeddedBrowserSession();
        _courseDownloadActive = true;
        _courseCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _windowLifetime.Token);
        _courseWorkStartedUtc = DateTimeOffset.UtcNow;
        if (!resume || _courseProcessStartedUtc == default)
            _courseProcessStartedUtc = DateTimeOffset.UtcNow;
        StartCourseElapsedTimer();
        UpdateCourseElapsed();
        UpdateCourseControls();

        try
        {
            var token = _courseCancellation.Token;
            GetCourseCoursePlan plan;
            string rootFolder;

            if (!resume)
            {
                _courseResumeAvailable = false;
                _courseCompletedLessons.Clear();
                _courseResumeLessonIndex = 0;
                _cachedCoursePlan = null;
                _cachedCourseRootFolder = null;
                _cachedCourseRoot = root;

                BeginCourseStructureProgress();
                plan = await BuildCoursePlanAsync(root, token);
                if (plan.Lessons.Length == 0)
                {
                    _browserHint.Text =
                        "В структуре курса не найдено доступных уроков.";
                    SetCourseProgressFinished(
                        "Структура курса считана, но доступных уроков не найдено.");
                    return;
                }

                rootFolder = Path.Combine(
                    Path.GetFullPath(_outputFolderBox.Text),
                    GetCourseCourseStructure.CourseArchiveFolder(plan.CourseTitle));
                Directory.CreateDirectory(rootFolder);
                await InitializeCourseStateForPlanAsync(
                    plan,
                    rootFolder,
                    token);
            }
            else
            {
                plan = _cachedCoursePlan!;
                rootFolder = _cachedCourseRootFolder!;
            }

            _courseTotalLessons = plan.Lessons.Length;
            ClearCourseNetworkWarning();
            _courseCurrentLessonIndex = Math.Clamp(
                _courseResumeLessonIndex,
                0,
                Math.Max(0, _courseTotalLessons - 1));

            var recovered = await RecoverCompletedCourseJobsAsync(
                rootFolder,
                token);
            var normalizedImages = NormalizeCourseImageExtensionsUnderRoot(rootFolder);
            _courseCompletedLessons.Clear();
            ReconcileCompletedLessonsFromDisk(plan, rootFolder);
            _courseResumeLessonIndex = FirstIncompleteLessonIndex(plan);
            await PersistCourseStateSafeAsync();
            if (recovered > 0 || normalizedImages > 0)
                _browserHint.Text =
                    $"Перед запуском перепроверен архив: восстановлено видео {recovered}, исправлено расширений изображений {normalizedImages}.";

            BeginCourseDownloadProgress(plan, resume);

            await DownloadCoursePlanWithAutomaticRecoveryAsync(
                plan,
                rootFolder,
                token);

            if (_courseCompletedLessons.Count >= plan.Lessons.Length)
            {
                _courseResumeAvailable = false;
                _courseResumeLessonIndex = plan.Lessons.Length;
                SetCourseProgressFinished(
                    $"Готово: полностью сохранено {plan.Lessons.Length} из {plan.Lessons.Length} уроков.");
                ScheduleCompletionActionAfterDownloads("course");
            }
            else
            {
                _courseResumeAvailable = true;
                SetCourseProgressError(
                    $"Первый проход завершён: полностью готово {_courseCompletedLessons.Count}/{plan.Lessons.Length}. " +
                    "Уроки с ошибками вложений или видео можно повторить кнопкой «Продолжить».");
            }
        }
        catch (OperationCanceledException)
        {
            _courseResumeAvailable = _cachedCoursePlan is not null;
            _browserHint.Text =
                "Скачивание курса остановлено. Готовые файлы сохранены; можно нажать «Продолжить».";
            SetCourseProgressPaused();
        }
        catch (Exception ex)
        {
            _courseResumeAvailable = _cachedCoursePlan is not null;
            DiagnosticHub.Log.Write(
                "course.download",
                "failed",
                ex.GetType().Name);
            _browserHint.Text =
                "Не удалось обработать курс: " +
                VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                    ex.Message);
            SetCourseProgressError(
                "Процесс остановлен с ошибкой. Можно исправить причину и нажать «Продолжить».");
        }
        finally
        {
            _courseCurrentVideoName = null;
            StopCourseElapsedTimer();
            UpdateCourseElapsed();
            await PersistCourseStateSafeAsync();
            ResetPauseState();
            _courseCancellation?.Dispose();
            _courseCancellation = null;
            _courseDownloadActive = false;
            _browserSession.RunProgrammaticSelectionCleanup(
                () => _cookiesBox.SelectedIndex = originalCookieIndex);
            UpdateCourseControls();
        }
    }

    private async Task DownloadCoursePlanWithAutomaticRecoveryAsync(
        GetCourseCoursePlan plan,
        string rootFolder,
        CancellationToken token)
    {
        var failureStreak = 0;
        var completedAtLastAttempt = _courseCompletedLessons.Count;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(token);
            try
            {
                var completedBeforePass =
                    _courseCompletedLessons.Count;

                await DownloadCoursePlanAsync(
                    plan,
                    rootFolder,
                    token);

                if (_courseCompletedLessons.Count
                    >= plan.Lessons.Length)
                {
                    if (await FinalVerifyCourseAsync(plan, rootFolder, token))
                        return;
                }

                var madeProgress =
                    _courseCompletedLessons.Count
                    > completedBeforePass;
                failureStreak = madeProgress
                    ? 0
                    : failureStreak + 1;
                if (madeProgress)
                    ClearCourseNetworkWarning();
                completedAtLastAttempt =
                    _courseCompletedLessons.Count;

                await WaitForIncompleteCourseRetryAsync(
                    plan,
                    failureStreak,
                    madeProgress,
                    token);
            }
            catch (OperationCanceledException)
                when (!token.IsCancellationRequested)
            {
                failureStreak = await WaitForCourseNetworkRecoveryAsync(
                    plan,
                    failureStreak,
                    completedAtLastAttempt,
                    new TimeoutException(
                        "Временная отмена сетевой операции."),
                    token);
                completedAtLastAttempt =
                    _courseCompletedLessons.Count;
            }
            catch (Exception ex)
                when (IsTransientCourseFailure(ex))
            {
                failureStreak = await WaitForCourseNetworkRecoveryAsync(
                    plan,
                    failureStreak,
                    completedAtLastAttempt,
                    ex,
                    token);
                completedAtLastAttempt =
                    _courseCompletedLessons.Count;
            }
        }
    }

    private async Task WaitForIncompleteCourseRetryAsync(
        GetCourseCoursePlan plan,
        int failureStreak,
        bool madeProgress,
        CancellationToken token)
    {
        _courseResumeLessonIndex =
            FirstIncompleteLessonIndex(plan);
        await PersistCourseStateSafeAsync();

        var remaining =
            Math.Max(
                0,
                plan.Lessons.Length
                - _courseCompletedLessons.Count);
        var delaySeconds = madeProgress
            ? 10
            : Math.Min(
                300,
                30 * Math.Max(1, failureStreak));

        UpdateCourseNetworkWarning(
            plan,
            failureStreak,
            error: null);

        DiagnosticHub.Log.Write(
            "course.auto-resume",
            "observed",
            $"incomplete-pass remaining={remaining} " +
            $"delay={delaySeconds}s progress={madeProgress}");

        for (var seconds = delaySeconds;
             seconds > 0;
             seconds--)
        {
            token.ThrowIfCancellationRequested();
            _courseStageText.Text =
                $"Остались незавершённые уроки/видео: {remaining}. " +
                $"Повтор через {seconds} сек.";
            _courseCurrentText.Text =
                $"Готово {_courseCompletedLessons.Count}/{plan.Lessons.Length}. " +
                $"Первый незавершённый: {_courseResumeLessonIndex + 1}/{plan.Lessons.Length}.";
            _courseEtaText.Text =
                "VideoGrabber автоматически повторит только незавершённые элементы.";
            _browserHint.Text =
                $"Осталось незавершённых уроков: {remaining}. " +
                $"Автоматический повтор через {seconds} сек.";
            await Task.Delay(
                TimeSpan.FromSeconds(1),
                token);
        }

        DiagnosticHub.Log.Write(
            "course.auto-resume",
            "started",
            $"incomplete-retry next={_courseResumeLessonIndex + 1}/{plan.Lessons.Length}");
    }

    private async Task<int> WaitForCourseNetworkRecoveryAsync(
        GetCourseCoursePlan plan,
        int failureStreak,
        int completedAtLastAttempt,
        Exception error,
        CancellationToken token)
    {
        if (_courseCompletedLessons.Count > completedAtLastAttempt)
            failureStreak = 0;

        failureStreak++;
        var delays = new[] { 5, 10, 20, 30, 60, 120 };
        var delaySeconds =
            delays[Math.Min(failureStreak - 1, delays.Length - 1)];

        _courseResumeLessonIndex =
            FirstIncompleteLessonIndex(plan);
        await PersistCourseStateSafeAsync();

        var safeError =
            VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                error.Message);
        UpdateCourseNetworkWarning(
            plan,
            failureStreak,
            safeError);
        DiagnosticHub.Log.Write(
            "course.auto-resume",
            "observed",
            $"transient={error.GetType().Name} streak={failureStreak} " +
            $"delay={delaySeconds}s completed={_courseCompletedLessons.Count}/{plan.Lessons.Length}");

        for (var remaining = delaySeconds;
             remaining > 0;
             remaining--)
        {
            token.ThrowIfCancellationRequested();
            _courseStageText.Text =
                $"Временная ошибка сети. Автопродолжение через {remaining} сек.";
            _courseCurrentText.Text =
                $"Готово {_courseCompletedLessons.Count}/{plan.Lessons.Length}. " +
                $"Следующий урок: {_courseResumeLessonIndex + 1}/{plan.Lessons.Length}.";
            _courseEtaText.Text =
                "VideoGrabber сам повторит текущий урок; нажимать «Продолжить» не нужно.";
            _browserHint.Text =
                "Временная ошибка соединения: " + safeError
                + $". Автоматический повтор через {remaining} сек.";
            await Task.Delay(
                TimeSpan.FromSeconds(1),
                token);
        }

        DiagnosticHub.Log.Write(
            "course.auto-resume",
            "started",
            $"retry={failureStreak} next={_courseResumeLessonIndex + 1}/{plan.Lessons.Length}");

        return failureStreak;
    }

    private void UpdateCourseNetworkWarning(
        GetCourseCoursePlan plan,
        int failureStreak,
        string? error)
    {
        if (_courseNetworkWarning is null
            || _courseNetworkWarningText is null)
            return;
        if (failureStreak < 2
            || plan.Lessons.Length == 0)
        {
            _courseNetworkWarning.Visibility = Visibility.Collapsed;
            return;
        }

        var index = Math.Clamp(
            _courseResumeLessonIndex,
            0,
            plan.Lessons.Length - 1);
        var lesson = plan.Lessons[index];
        var title = $"Урок {index + 1} пока не скачан полностью";
        var message =
            $"{lesson.Title}. VideoGrabber автоматически чередует прямой физический и системный маршрут Windows. " +
            "Если именно этот урок в браузере открывается только при включённом VPN, включите VPN — повтор продолжится сам, без перезапуска программы." +
            (string.IsNullOrWhiteSpace(error)
                ? string.Empty
                : " Последняя ошибка: " + error);
        _courseNetworkWarningText.Text = title + Environment.NewLine + message;
        _courseNetworkWarning.Visibility = Visibility.Visible;
    }

    private void ClearCourseNetworkWarning()
    {
        if (_courseNetworkWarning is not null)
            _courseNetworkWarning.Visibility = Visibility.Collapsed;
    }

    private static bool IsTransientCourseFailure(Exception ex)
    {
        if (ex is HttpRequestException http)
        {
            if (http.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
                return false;
            return true;
        }

        if (ex is IOException
            or TimeoutException
            or System.Net.Sockets.SocketException)
            return true;

        if (ex is TaskCanceledException
            or OperationCanceledException)
            return true;

        return ex.InnerException is not null
            && !ReferenceEquals(ex.InnerException, ex)
            && IsTransientCourseFailure(ex.InnerException);
    }

    private async Task<GetCourseCoursePlan> BuildCoursePlanAsync(
        Uri root,
        CancellationToken token)
    {
        if (!await NavigateCoursePageAsync(root, token))
            throw new InvalidOperationException(
                "Главная страница курса не загрузилась.");

        var rootPage = await WaitForCoursePageAsync(token)
            ?? throw new InvalidDataException(
                "Не удалось распознать структуру курса.");

        var scannedModules = 0;
        return await GetCourseCoursePlanner.BuildAsync(
            root,
            rootPage,
            async (uri, cancellationToken) =>
            {
                if (!await NavigateCoursePageAsync(uri, cancellationToken))
                    return null;
                return await WaitForCoursePageAsync(cancellationToken);
            },
            title =>
            {
                scannedModules++;
                _browserHint.Text = $"Считываю модуль: {title}";
                UpdateCourseStructureProgress(title, scannedModules);
            },
            token);
    }

    private async Task DownloadCoursePlanAsync(
        GetCourseCoursePlan plan,
        string rootFolder,
        CancellationToken token)
    {
        var visitedLessons = 0;
        var archivedLessons = 0;
        var completedVideos = 0;
        var assetsSaved = 0;
        var lessonErrors = 0;
        var assetErrors = 0;
        var videoErrors = 0;

        for (var lessonIndex = 0;
             lessonIndex < plan.Lessons.Length;
             lessonIndex++)
        {
            token.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(token);
            var lesson = plan.Lessons[lessonIndex];
            var lessonKey = GetCourseCourseStructure.CanonicalKey(lesson.Uri);
            if (_courseCompletedLessons.Contains(lessonKey))
            {
                UpdateCourseLessonProgress(
                    lessonIndex,
                    plan.Lessons.Length,
                    lesson,
                    "Уже готов — пропускаю");
                continue;
            }

            _courseCurrentLessonIndex = lessonIndex;
            _courseResumeLessonIndex = lessonIndex;
            _courseCurrentVideoName = null;
            UpdateCourseLessonProgress(
                lessonIndex,
                plan.Lessons.Length,
                lesson,
                "Открываю страницу урока");

            if (!await NavigateCoursePageAsync(lesson.Uri, token))
            {
                lessonErrors++;
                UpdateCourseLessonProgress(
                    lessonIndex,
                    plan.Lessons.Length,
                    lesson,
                    "Не удалось открыть страницу — перейду дальше");
                continue;
            }
            visitedLessons++;

            var moduleFolder = lesson.ModuleFolders.Aggregate(
                rootFolder,
                Path.Combine);
            var lessonFolder = Path.Combine(
                moduleFolder,
                CourseLessonArchive.LessonFolder(
                    lesson.LessonOrdinal,
                    lesson.Title));
            Directory.CreateDirectory(lessonFolder);

            UpdateCourseLessonProgress(
                lessonIndex,
                plan.Lessons.Length,
                lesson,
                "Сохраняю Word/HTML, изображения и вложения");

            LessonArchiveResult archive;
            try
            {
                archive = await SaveCourseLessonArchiveAsync(
                    lesson,
                    lessonFolder,
                    token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DiagnosticHub.Log.Write(
                    "course.archive",
                    "failed",
                    ex.GetType().Name);
                archive = new(false, 0, 0);
            }

            if (archive.PageSaved) archivedLessons++;
            else lessonErrors++;
            assetsSaved += archive.AssetsSaved;
            assetErrors += archive.AssetErrors;

            UpdateCourseLessonProgress(
                lessonIndex,
                plan.Lessons.Length,
                lesson,
                "Ищу доступные видео");

            var candidates = await DiscoverCourseMediaFromDomAsync(
                lesson.Uri,
                token);
            if (candidates.Count == 0)
                candidates = await WaitForCourseMediaAsync(token);

            var quality = CourseQuality();
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(
                lessonFolder,
                new CourseLessonVerificationManifest(
                    CourseLessonVerificationManifestStore.CurrentSchemaVersion,
                    lesson.Uri.AbsoluteUri,
                    candidates.Count,
                    archive.ExpectedAssets,
                    quality,
                    DateTimeOffset.UtcNow),
                token);

            if (candidates.Count == 0)
            {

                if (archive.PageSaved && archive.AssetErrors == 0)
                    await MarkCourseLessonCompletedAsync(
                        lessonKey,
                        lessonIndex,
                        plan.Lessons.Length,
                        rootFolder,
                        lesson);

                DiagnosticHub.Log.Write(
                    "course.lesson",
                    archive.PageSaved ? "succeeded" : "observed",
                    "Lesson archived without downloadable video");
                continue;
            }

            var lessonVideoErrors = 0;
            for (var videoIndex = 0;
                 videoIndex < candidates.Count;
                 videoIndex++)
            {
                token.ThrowIfCancellationRequested();
                await WaitIfPausedAsync(token);
                var candidate = candidates[videoIndex];
                var baseName =
                    GetCourseCourseStructure.VideoBaseName(
                        lesson.LessonOrdinal,
                        lesson.Title,
                        videoIndex + 1,
                        candidates.Count,
                        quality);

                _courseCurrentVideoName = baseName;

                if (CourseOutputExists(lessonFolder, baseName))
                {
                    completedVideos++;
                    continue;
                }

                var outcome = await DownloadCourseVideoWithRetryAsync(
                    lesson,
                    lessonIndex,
                    plan.Lessons.Length,
                    lessonFolder,
                    videoIndex,
                    candidates.Count,
                    quality,
                    candidate,
                    baseName,
                    token);

                if (outcome == OperationOutcome.Cancelled)
                    throw new OperationCanceledException(token);
                if (outcome != OperationOutcome.Succeeded)
                {
                    videoErrors++;
                    lessonVideoErrors++;
                    continue;
                }
                completedVideos++;
            }

            _courseCurrentVideoName = null;
            if (archive.PageSaved
                && archive.AssetErrors == 0
                && lessonVideoErrors == 0)
                await MarkCourseLessonCompletedAsync(
                    lessonKey,
                    lessonIndex,
                    plan.Lessons.Length,
                    rootFolder,
                    lesson);
        }

        try
        {
            await NavigateCoursePageAsync(plan.Root, token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Finished course files take precedence over restoring the UI page.
        }

        var errors = lessonErrors + assetErrors + videoErrors;
        _browserHint.Text =
            $"Курс обработан. Страниц открыто: {visitedLessons}/{plan.Lessons.Length}. " +
            $"Word/HTML сохранено: {archivedLessons}. Вложений: {assetsSaved}. " +
            $"Видео: {completedVideos}. Ошибок: {errors}.";
    }

    private async Task<OperationOutcome> DownloadCourseVideoWithRetryAsync(
        GetCourseLessonPlan lesson,
        int lessonIndex,
        int totalLessons,
        string lessonFolder,
        int videoIndex,
        int videoCount,
        string quality,
        MediaCandidate initialCandidate,
        string baseName,
        CancellationToken token)
    {
        const int maxAttempts = 4;
        var requestedResumeKey =
            CourseDownloadStateStore.StableMediaResumeKey(
                lesson.Uri,
                videoIndex + 1,
                quality);
        MigrateLegacyCourseJob(
            lessonFolder,
            baseName,
            requestedResumeKey);

        var candidate = initialCandidate;
        for (var attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            token.ThrowIfCancellationRequested();

            if (CourseOutputExists(
                    lessonFolder,
                    baseName))
                return OperationOutcome.Succeeded;

            if (attempt > 1)
            {
                var delaySeconds =
                    Math.Min(8, attempt * 2);
                UpdateCourseLessonProgress(
                    lessonIndex,
                    totalLessons,
                    lesson,
                    $"Видео {videoIndex + 1}/{videoCount}: ошибка сети/CDN. " +
                    $"Обновляю ссылку и повторяю попытку {attempt}/{maxAttempts} через {delaySeconds} сек.");
                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    token);

                var refreshed =
                    await DiscoverCourseMediaFromDomAsync(
                        lesson.Uri,
                        token);
                if (refreshed.Count > videoIndex)
                    candidate = refreshed[videoIndex];
            }
            else
            {
                UpdateCourseLessonProgress(
                    lessonIndex,
                    totalLessons,
                    lesson,
                    $"Скачиваю видео {videoIndex + 1} из {videoCount}: {baseName}");
            }

            var attemptQuality =
                CourseRetryQuality(
                    candidate,
                    quality,
                    attempt);

            if (!string.Equals(
                    attemptQuality,
                    quality,
                    StringComparison.Ordinal))
            {
                UpdateCourseLessonProgress(
                    lessonIndex,
                    totalLessons,
                    lesson,
                    $"Видео {videoIndex + 1}/{videoCount}: верхний HLS-вариант недоступен. " +
                    $"Пробую рабочий вариант {attemptQuality}.");
                DiagnosticHub.Log.Write(
                    "course.video.retry",
                    "observed",
                    $"quality-fallback={attemptQuality} attempt={attempt}/{maxAttempts}");
            }

            var attemptResumeKey =
                CourseDownloadStateStore.StableMediaResumeKey(
                    lesson.Uri,
                    videoIndex + 1,
                    attemptQuality);

            var intent = CaptureDownloadIntent(
                candidate.Source,
                attemptQuality) with
            {
                OutputDirectory = lessonFolder,
                AudioOnly = false
            };

            var outcome = await RunDownloadOperationAsync(
                intent,
                new BrowserDownloadPreparation(
                    this,
                    candidate,
                    candidate.PageOrdinal
                        ?? videoIndex + 1,
                    queueContext: null,
                    suggestedBaseNameOverride: baseName,
                    resumeKeyOverride: attemptResumeKey),
                resetCookieSelectionAfterUse: false,
                managedKind: "course_download");

            if (outcome == OperationOutcome.Cancelled)
                return outcome;
            if (outcome == OperationOutcome.Succeeded)
                return outcome;

            DiagnosticHub.Log.Write(
                "course.video.retry",
                attempt < maxAttempts
                    ? "observed"
                    : "failed",
                $"lesson={lessonIndex + 1}/{totalLessons} " +
                $"video={videoIndex + 1}/{videoCount} " +
                $"attempt={attempt}/{maxAttempts}");
        }

        return OperationOutcome.Failed;
    }

    private static string CourseRetryQuality(
        MediaCandidate candidate,
        string requestedQuality,
        int attempt)
    {
        if (candidate.HlsManifest is not { IsMaster: true } info)
            return requestedQuality;

        if (!string.Equals(requestedQuality, "best", StringComparison.Ordinal))
        {
            // Course quality is a ceiling, not an exact-height requirement.
            // Example: "до 480p" accepts 360p when 480p is absent.
            var selected = HlsTrackSelector.Select(info, requestedQuality);
            return selected?.Video.Height is > 0
                ? selected.Video.Height.Value + "p"
                : requestedQuality;
        }

        if (attempt < 3)
            return requestedQuality;

        var heights = info.Variants
            .Where(variant => variant.Height is > 0)
            .Select(variant => variant.Height!.Value)
            .Distinct()
            .OrderByDescending(height => height)
            .ToArray();

        if (heights.Length <= 1)
            return requestedQuality;

        var index = Math.Min(
            attempt - 2,
            heights.Length - 1);
        return heights[index] + "p";
    }

    private async Task<bool> NavigateCoursePageAsync(
        Uri uri,
        CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2
            ?? throw new InvalidOperationException(
                "Встроенный браузер не открыт.");

        if (_browserPageUri is { } current
            && string.Equals(
                GetCourseCourseStructure.CanonicalKey(current),
                GetCourseCourseStructure.CanonicalKey(uri),
                StringComparison.Ordinal))
        {
            var existingLease = _browserPages.Capture();
            await RefreshBrowserBindingsAsync(core, existingLease);
            return true;
        }

        var completion =
            new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        CoreWebView2WebErrorStatus? lastTransient = null;
        Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            if (args.IsSuccess)
            {
                completion.TrySetResult(args);
                return;
            }

            if (args.WebErrorStatus is CoreWebView2WebErrorStatus.ConnectionAborted
                or CoreWebView2WebErrorStatus.OperationCanceled)
            {
                lastTransient = args.WebErrorStatus;
                DiagnosticHub.Log.Write(
                    "course.navigation",
                    "observed",
                    "Transient redirect navigation: " + args.WebErrorStatus);
                return;
            }

            completion.TrySetResult(args);
        };
        core.NavigationCompleted += handler;

        try
        {
            core.Navigate(uri.AbsoluteUri);
            CoreWebView2NavigationCompletedEventArgs result;
            try
            {
                result = await completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(45),
                    token);
            }
            catch (TimeoutException)
            {
                DiagnosticHub.Log.Write(
                    "course.navigation",
                    "failed",
                    lastTransient is null
                        ? "Navigation timeout"
                        : "Navigation timeout after transient " + lastTransient);
                return false;
            }
            if (!result.IsSuccess)
            {
                DiagnosticHub.Log.Write(
                    "course.navigation",
                    "failed",
                    result.WebErrorStatus.ToString());
                return false;
            }

            await Task.Delay(500, token);
            var lease = _browserPages.Capture();
            await RefreshBrowserBindingsAsync(core, lease);
            return IsCurrentBrowserPage(lease, core);
        }
        finally
        {
            core.NavigationCompleted -= handler;
        }
    }

    private async Task<GetCourseCoursePage?> WaitForCoursePageAsync(
        CancellationToken token)
    {
        GetCourseCoursePage? best = null;
        var stableCount = -1;
        var stablePasses = 0;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var page = await ReadCoursePageAsync(token);
            if (page is not null)
            {
                best = page;
                if (page.Links.Count > 0)
                {
                    if (stableCount == page.Links.Count) stablePasses++;
                    else { stableCount = page.Links.Count; stablePasses = 0; }
                    if (stablePasses >= 2) return page;
                }
            }
            await Task.Delay(250, token);
        }
        return best;
    }
    private async Task<GetCourseCoursePage?> ReadCoursePageAsync(
        CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2;
        var current = _browserPageUri;
        if (core is null || current is null) return null;
        var lease = _browserPages.Capture();
        if (!IsCurrentBrowserPage(lease, core)) return null;

        const string script = """
            (() => {
              const clean = value =>
                (value || '').replace(/\\s+/g, ' ').trim();

              const nodes = [...document.querySelectorAll(
                'a[href],[data-href],[data-url],[data-link],[onclick]')];
              const urlPattern = new RegExp(
                "(?:https?:\\/\\/[^'\\\"\\s)]+|\\/(?:pl\\/)?teach\\/control\\/(?:lesson|stream)\\/view[^'\\\"\\s)]*)",
                'i');
              const urlOf = node => {
                const direct =
                  node.href ||
                  node.getAttribute?.('href') ||
                  node.getAttribute?.('data-href') ||
                  node.getAttribute?.('data-url') ||
                  node.getAttribute?.('data-link');
                if (direct && !String(direct).toLowerCase().startsWith('javascript:')) {
                  try { return new URL(direct, document.baseURI); }
                  catch {}
                }
                const onclick = node.getAttribute?.('onclick') || '';
                const match = onclick.match(urlPattern);
                if (!match) return null;
                try { return new URL(match[0], document.baseURI); }
                catch { return null; }
              };

              const titleOf = node => {
                const own = clean(
                  node.innerText ||
                  node.textContent ||
                  node.getAttribute?.('title') ||
                  node.getAttribute?.('aria-label'));
                if (own) return own.slice(0, 180);
                const parent = node.closest?.('li,tr,.lesson-row,.training-row');
                return clean(parent?.innerText || parent?.textContent)
                  .slice(0, 180);
              };

              const links = [];
              nodes.forEach((node, index) => {
                const url = urlOf(node);
                if (!url || url.origin !== location.origin) return;
                const path = url.pathname.toLowerCase();
                const kind = path.includes('/teach/control/lesson/view')
                  ? 'lesson'
                  : path.includes('/teach/control/stream/view')
                    ? 'training'
                    : null;
                if (!kind) return;
                links.push({
                  kind,
                  title: titleOf(node),
                  url: url.href,
                  order: index
                });
              });

              const heading = document.querySelector(
                'h1,.training-title,.stream-title,' +
                '.page-header h1,[class*="training-title"]');
              return {
                pageTitle: clean(
                  heading?.innerText ||
                  heading?.textContent ||
                  document.title),
                links
              };
            })()
            """;

        try
        {
            var json = await core.ExecuteScriptAsync(script)
                .AsTask()
                .WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrentBrowserPage(lease, core)) return null;
            if (!GetCourseCourseStructure.TryParsePage(
                    json,
                    current,
                    out var page)
                || page is null)
            {
                var resultKind = string.Equals(json.Trim(), "null", StringComparison.Ordinal)
                    ? "null"
                    : json.Length == 0 ? "empty" : "unparsed";
                DiagnosticHub.Log.Write(
                    "course.structure",
                    "failed",
                    $"host={current.IdnHost} result={resultKind} length={json.Length}");
                return null;
            }
            DiagnosticHub.Log.Write(
                "course.structure",
                "succeeded",
                $"host={current.IdnHost} links={page.Links.Count} lessons={page.Lessons.Count} trainings={page.Trainings.Count}");
            return page;
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException)
        {
            DiagnosticHub.Log.Write(
                "course.structure",
                "failed",
                ex.GetType().Name);
            return null;
        }
    }

    private async Task<IReadOnlyList<MediaCandidate>>
        WaitForCourseMediaAsync(CancellationToken token)
    {
        var stableCount = -1;
        var stablePasses = 0;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var core = _mediaBrowser?.CoreWebView2;
            if (core is null) return [];

            var lease = _browserPages.Capture();
            if (IsCurrentBrowserPage(lease, core)
                && (attempt == 0 || attempt % 4 == 0))
                await RefreshBrowserBindingsAsync(core, lease);

            var candidates = _mediaCandidatesBox.Items
                .OfType<ComboBoxItem>()
                .Select(item => item.Tag as MediaCandidate)
                .Where(item => item is not null)
                .Cast<MediaCandidate>()
                .ToArray();

            if (candidates.Length > 0)
            {
                if (stableCount == candidates.Length)
                    stablePasses++;
                else
                {
                    stableCount = candidates.Length;
                    stablePasses = 0;
                }

                if (stablePasses >= 2)
                    return candidates;
            }

            var playerSeen = Volatile.Read(
                ref _lastGetCoursePlayerGeneration) == lease.Generation;
            if (attempt >= 12 && !playerSeen)
            {
                DiagnosticHub.Log.Write(
                    "course.media",
                    "observed",
                    "No GetCourse player detected on lesson page");
                return [];
            }

            await Task.Delay(250, token);
        }

        return _mediaCandidatesBox.Items
            .OfType<ComboBoxItem>()
            .Select(item => item.Tag as MediaCandidate)
            .Where(item => item is not null)
            .Cast<MediaCandidate>()
            .ToArray();
    }
    private void StartCourseElapsedTimer()
    {
        _courseElapsedTimer ??= DispatcherQueue.CreateTimer();
        _courseElapsedTimer.Interval = TimeSpan.FromSeconds(1);
        _courseElapsedTimer.IsRepeating = true;
        _courseElapsedTimer.Tick -= CourseElapsedTimer_Tick;
        _courseElapsedTimer.Tick += CourseElapsedTimer_Tick;
        _courseElapsedTimer.Start();
    }

    private void StopCourseElapsedTimer()
    {
        if (_courseElapsedTimer is null) return;
        _courseElapsedTimer.Stop();
    }

    private void CourseElapsedTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
        => UpdateCourseElapsed();

    private void UpdateCourseElapsed()
    {
        if (_courseElapsedText is null
            || _courseProcessStartedUtc == default)
            return;

        var elapsed = DateTimeOffset.UtcNow - _courseProcessStartedUtc;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var hours = (int)elapsed.TotalHours;
        _courseElapsedText.Text =
            $"Прошло с начала: {hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void SetCourseOverallProgress(double? percent, string? label = null)
    {
        if (_courseProgressTrack is null || _courseProgressFill is null
            || _courseProgressPercent is null) return;

        if (percent is null)
        {
            _courseOverallPercent = 0;
            _courseProgressPercent.Text = label ?? "Анализирую структуру…";
        }
        else
        {
            _courseOverallPercent = Math.Clamp(percent.Value, 0, 100);
            _courseProgressPercent.Text = label ?? $"{_courseOverallPercent:0.0}%";
        }
        UpdateCourseProgressWidth();
    }

    private void UpdateCourseProgressWidth()
    {
        if (_courseProgressTrack is null || _courseProgressFill is null) return;
        _courseProgressFill.Width =
            _courseProgressTrack.ActualWidth * _courseOverallPercent / 100d;
    }

    private void BeginCourseStructureProgress()
    {
        SetCourseOverallProgress(null, "Анализирую структуру…");
        _courseStageText.Text =
            "Этап 1 из 2 — анализирую структуру курса";
        _courseCurrentText.Text =
            "Обхожу основные разделы и вложенные модули, чтобы составить полный список уроков.";
        _courseEtaText.Text =
            "Количество уроков и общий процент появятся после завершения анализа структуры.";
    }

    private void UpdateCourseStructureProgress(
        string moduleTitle,
        int scannedModules)
    {
        _courseStageText.Text =
            $"Этап 1 из 2 — анализ структуры · просмотрено модулей: {scannedModules}";
        _courseCurrentText.Text =
            $"Сейчас считываю: {moduleTitle}";
        _courseEtaText.Text =
            "После построения плана начнётся сохранение страниц, вложений и видео.";
    }

    private void BeginCourseDownloadProgress(
        GetCourseCoursePlan plan,
        bool resume)
    {
        _courseCompletedAtRunStart = _courseCompletedLessons.Count;
        var percent = plan.Lessons.Length == 0
            ? 0
            : 100d * _courseCompletedLessons.Count / plan.Lessons.Length;
        SetCourseOverallProgress(percent);
        _courseStageText.Text =
            resume
                ? $"Этап 2 из 2 — продолжаю курс · {_courseCompletedLessons.Count}/{plan.Lessons.Length}"
                : $"Этап 2 из 2 — сохраняю курс · 0/{plan.Lessons.Length}";
        _courseCurrentText.Text =
            resume
                ? "Использую уже построенный план; полностью готовые уроки будут пропущены."
                : $"Структура готова: найдено {plan.Lessons.Length} уроков.";
        _courseEtaText.Text =
            "Оценка оставшегося времени появится после первого завершённого урока.";
    }

    private void UpdateCourseLessonProgress(
        int lessonIndex,
        int total,
        GetCourseLessonPlan lesson,
        string action)
    {
        if (total <= 0) return;
        var overallPercent =
            Math.Clamp(100d * _courseCompletedLessons.Count / total, 0, 100);
        SetCourseOverallProgress(overallPercent);

        var module = lesson.ModuleFolders.Length == 0
            ? "Курс"
            : string.Join(" → ", lesson.ModuleFolders);
        _courseStageText.Text =
            $"Этап 2 из 2 — урок {lessonIndex + 1} из {total} · " +
            $"готово {_courseCompletedLessons.Count}/{total} · {overallPercent:0.0}%";
        _courseCurrentText.Text =
            $"{module} → {lesson.Title}" + Environment.NewLine + action;
        _courseEtaText.Text = EstimateCourseRemaining(total);
        _browserHint.Text =
            $"Урок {lessonIndex + 1}/{total}: {module} → {lesson.Title}. {action}.";
    }

    private async Task MarkCourseLessonCompletedAsync(
        string lessonKey,
        int lessonIndex,
        int total,
        string rootFolder,
        GetCourseLessonPlan lesson)
    {
        if (!CourseLessonLooksCompleteOnDisk(
                rootFolder,
                lesson,
                out var incompleteReason))
        {
            _courseCompletedLessons.Remove(lessonKey);
            _courseResumeLessonIndex = lessonIndex;
            DiagnosticHub.Log.Write(
                "course.lesson-verify",
                "observed",
                $"lesson={lessonIndex + 1}/{total} incomplete={incompleteReason}");
            await PersistCourseStateSafeAsync();
            return;
        }

        if (!_courseCompletedLessons.Add(lessonKey)) return;
        _courseResumeLessonIndex = lessonIndex + 1;
        if (total > 0)
            SetCourseOverallProgress(
                Math.Clamp(100d * _courseCompletedLessons.Count / total, 0, 100));
        if (_courseStageText is not null)
            _courseStageText.Text =
                $"Этап 2 из 2 — готово {_courseCompletedLessons.Count} из {total} уроков";
        if (_courseEtaText is not null)
            _courseEtaText.Text = EstimateCourseRemaining(total);

        await PersistCourseStateSafeAsync();
    }

    private string EstimateCourseRemaining(int total)
    {
        var completedThisRun =
            _courseCompletedLessons.Count - _courseCompletedAtRunStart;
        var remaining = Math.Max(0, total - _courseCompletedLessons.Count);
        if (remaining == 0) return "Осталось: 0 уроков.";
        if (completedThisRun <= 0)
            return $"Осталось: {remaining} уроков · расчёт времени появится после первого готового урока.";

        var elapsed = DateTimeOffset.UtcNow - _courseWorkStartedUtc;
        var secondsPerLesson =
            elapsed.TotalSeconds / Math.Max(1, completedThisRun);
        var estimate = TimeSpan.FromSeconds(
            Math.Max(0, secondsPerLesson * remaining));
        return $"Осталось: {remaining} уроков · ориентировочно {FormatCourseDuration(estimate)}.";
    }

    private static string FormatCourseDuration(TimeSpan value)
    {
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours} ч {value.Minutes:00} мин";
        if (value.TotalMinutes >= 1)
            return $"{Math.Max(1, (int)Math.Ceiling(value.TotalMinutes))} мин";
        return $"{Math.Max(1, (int)Math.Ceiling(value.TotalSeconds))} сек";
    }

    private void SetCourseProgressPaused()
    {
        _courseStageText.Text =
            $"Пауза — готово {_courseCompletedLessons.Count}/{_courseTotalLessons}.";
        _courseCurrentText.Text =
            "Процесс остановлен. Нажмите «Продолжить», чтобы продолжить по сохранённому плану курса.";
        _courseEtaText.Text =
            "Готовые уроки повторно скачиваться не будут.";
    }

    private void SetCourseProgressFinished(string message)
    {
        var percent = _courseTotalLessons > 0
            ? Math.Clamp(
                100d * _courseCompletedLessons.Count / _courseTotalLessons,
                0,
                100)
            : 100;
        SetCourseOverallProgress(percent);
        _courseStageText.Text = message;
        _courseCurrentText.Text = " ";
        _courseEtaText.Text = " ";
    }

    private void SetCourseProgressError(string message)
    {
        _courseStageText.Text = "Курс остановлен.";
        _courseCurrentText.Text = message;
        _courseEtaText.Text =
            _courseResumeAvailable
                ? "Можно нажать «Продолжить»."
                : "Нужно запустить курс заново.";
    }

    private void UpdateCourseFileProgress(double? filePercent)
    {
        if (!_courseDownloadActive
            || _courseTotalLessons <= 0
            || filePercent is null)
            return;

        var currentFraction =
            Math.Clamp(filePercent.Value, 0, 100) / 100d;
        var overall =
            (_courseCompletedLessons.Count + 0.8d * currentFraction)
            / _courseTotalLessons * 100d;
        SetCourseOverallProgress(
            Math.Clamp(overall, 0, 99.9),
            $"{Math.Clamp(overall, 0, 99.9):0.0}%");
        if (_courseStageText is not null)
            _courseStageText.Text =
                $"Этап 2 из 2 — урок {_courseCurrentLessonIndex + 1} из {_courseTotalLessons} · " +
                $"общий прогресс {Math.Clamp(overall, 0, 99.9):0.0}%";
    }
    private async Task<int> RecoverCompletedCourseJobsAsync(
        string root,
        CancellationToken token)
    {
        if (!Directory.Exists(root) || !File.Exists(_tools.Ffprobe))
            return 0;

        var recovered = 0;
        var probe = new FfprobeMediaProbe(_componentRunner, _tools);
        var jobs = Directory.EnumerateDirectories(
                root,
                ".vg-job-*",
                SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .ToArray();

        foreach (var job in jobs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if ((File.GetAttributes(job) & FileAttributes.ReparsePoint) != 0)
                    continue;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var parent = Path.GetDirectoryName(job);
            if (string.IsNullOrWhiteSpace(parent))
                continue;

            var mediaFiles = Directory.EnumerateFiles(
                    job,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                        return false;
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    return extension is ".mp4" or ".mkv" or ".webm"
                        or ".mov" or ".m4a" or ".mp3" or ".aac"
                        or ".opus" or ".ts";
                })
                .Where(path =>
                {
                    try { return new FileInfo(path).Length > 0; }
                    catch (IOException) { return false; }
                })
                .ToArray();

            foreach (var source in mediaFiles)
            {
                token.ThrowIfCancellationRequested();
                var media = await probe.ProbeAsync(source, token);
                if (!media.IsValid || (!media.HasVideo && !media.HasAudio))
                    continue;

                var extension = Path.GetExtension(source);
                var stem = Path.GetFileNameWithoutExtension(source);
                const string downloading = " - downloading";
                if (stem.EndsWith(
                        downloading,
                        StringComparison.OrdinalIgnoreCase))
                    stem = stem[..^downloading.Length];

                var target = Path.Combine(parent, stem + extension);
                if (File.Exists(target))
                {
                    try
                    {
                        if (new FileInfo(target).Length
                            == new FileInfo(source).Length)
                        {
                            File.Delete(source);
                            recovered++;
                            continue;
                        }
                    }
                    catch (IOException) { }

                    for (var index = 2; index < 1000; index++)
                    {
                        var alternative = Path.Combine(
                            parent,
                            $"{stem} - recovered {index}{extension}");
                        if (File.Exists(alternative)) continue;
                        target = alternative;
                        break;
                    }
                }

                try
                {
                    File.Move(source, target, overwrite: false);
                    recovered++;
                    DiagnosticHub.Log.Write(
                        "course.recovery",
                        "succeeded",
                        "Recovered verified media from an owned job directory");
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                    DiagnosticHub.Log.Write(
                        "course.recovery",
                        "failed",
                        ex.GetType().Name);
                }
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(job).Any())
                    Directory.Delete(job, recursive: false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return recovered;
    }

    private static bool IsCourseTemporaryFile(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        return name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
            || name.Contains(".partial-", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".temp", StringComparison.OrdinalIgnoreCase);
    }

    private void ClearCourseTemporaryFiles()
    {
        if (_courseDownloadActive || _operations.IsBusy)
        {
            _browserHint.Text =
                "Сначала остановите текущую загрузку кнопкой «Отменить всё», затем очищайте временные файлы.";
            return;
        }

        var root = _cachedCourseRootFolder;
        if (string.IsNullOrWhiteSpace(root))
            root = _outputFolderBox.Text;
        if (string.IsNullOrWhiteSpace(root)
            || !Directory.Exists(root))
        {
            _browserHint.Text =
                "Папка курса ещё не создана — очищать нечего.";
            return;
        }

        var deletedFiles = 0;
        var deletedDirectories = 0;
        var preservedReadyMedia = 0;

        try
        {
            var jobs = Directory.EnumerateDirectories(
                    root,
                    ".vg-job-*",
                    SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToArray();

            foreach (var job in jobs)
            {
                foreach (var file in Directory.EnumerateFiles(
                             job,
                             "*",
                             SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);
                    var temporary =
                        name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
                        || name.Contains(".partial-", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".temp", StringComparison.OrdinalIgnoreCase);

                    if (temporary)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedFiles++;
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                        continue;
                    }

                    if (extension is ".mp4" or ".mkv" or ".webm" or ".mov"
                        or ".m4a" or ".mp3" or ".aac" or ".opus" or ".ts")
                    {
                        try
                        {
                            if (new FileInfo(file).Length > 0)
                                preservedReadyMedia++;
                        }
                        catch (IOException) { }
                    }
                }

                try
                {
                    foreach (var directory in Directory.EnumerateDirectories(
                                 job,
                                 "*",
                                 SearchOption.AllDirectories)
                             .OrderByDescending(path => path.Length))
                    {
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                            Directory.Delete(directory, recursive: false);
                    }

                    if (!Directory.EnumerateFileSystemEntries(job).Any())
                    {
                        Directory.Delete(job, recursive: false);
                        deletedDirectories++;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            _courseStageText.Text =
                "Временные файлы очищены.";
            _courseCurrentText.Text =
                $"Удалено временных файлов: {deletedFiles}; пустых рабочих папок: {deletedDirectories}.";
            _courseEtaText.Text =
                preservedReadyMedia > 0
                    ? $"Сохранено {preservedReadyMedia} готовых медиафайлов из старых рабочих папок — они не удалены."
                    : "Готовые видео и документы не удалялись.";
            _browserHint.Text =
                "Очистка завершена. Готовые файлы сохранены.";
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            _browserHint.Text =
                "Не удалось полностью очистить временные файлы: "
                + VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                    ex.Message);
        }
    }

    private string SelectedCourseQuality()
        => (_courseQualityBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";

    private string CourseQuality() => _courseActiveQuality;

    private void SetCourseQualitySelection(string quality)
    {
        if (_courseQualityBox is null) return;
        for (var index = 0; index < _courseQualityBox.Items.Count; index++)
        {
            if (_courseQualityBox.Items[index] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), quality, StringComparison.OrdinalIgnoreCase))
            {
                _courseQualityBox.SelectedIndex = index;
                return;
            }
        }
        _courseQualityBox.SelectedIndex = Math.Max(0, _courseQualityBox.Items.Count - 1);
    }

    private void SelectEmbeddedBrowserSession()
    {
        for (var index = 0;
             index < _cookiesBox.Items.Count;
             index++)
        {
            if (_cookiesBox.Items[index] is ComboBoxItem item
                && string.Equals(
                    item.Tag?.ToString(),
                    "embedded",
                    StringComparison.Ordinal))
            {
                var target = index;
                _browserSession.RunProgrammaticSelectionCleanup(
                    () => _cookiesBox.SelectedIndex = target);
                return;
            }
        }
    }

    private static void MigrateLegacyCourseJob(
        string lessonFolder,
        string baseName,
        string resumeKey)
    {
        if (!Directory.Exists(lessonFolder))
            return;

        var target = Path.Combine(
            lessonFolder,
            ".vg-job-" + resumeKey);
        if (Directory.Exists(target))
        {
            PruneZeroLengthCoursePartials(target);
            return;
        }
        if (File.Exists(target))
            return;

        DirectoryInfo? best = null;
        foreach (var path in Directory.EnumerateDirectories(
                     lessonFolder,
                     ".vg-job-*",
                     SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(
                    path,
                    target,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            DirectoryInfo info;
            try
            {
                info = new DirectoryInfo(path);
                if ((info.Attributes
                        & FileAttributes.ReparsePoint) != 0)
                    continue;
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException)
            {
                continue;
            }

            var match = false;
            try
            {
                foreach (var file in info.EnumerateFiles(
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    if (!file.Name.StartsWith(
                            baseName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var extension =
                        file.Extension.ToLowerInvariant();
                    if (IsCourseTemporaryFile(file.FullName)
                        || extension is ".mp4" or ".mkv"
                            or ".webm" or ".mov"
                            or ".m4a" or ".mp3"
                            or ".aac" or ".opus" or ".ts")
                    {
                        match = true;
                        break;
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException)
            {
                continue;
            }

            if (!match) continue;
            if (best is null
                || info.LastWriteTimeUtc
                    > best.LastWriteTimeUtc)
                best = info;
        }

        if (best is null)
            return;

        try
        {
            Directory.Move(
                best.FullName,
                target);
            PruneZeroLengthCoursePartials(target);
            DiagnosticHub.Log.Write(
                "course.resume",
                "succeeded",
                "Legacy partial workspace migrated to stable resume key");
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException)
        {
            DiagnosticHub.Log.Write(
                "course.resume",
                "failed",
                ex.GetType().Name);
        }
    }

    private static void PruneZeroLengthCoursePartials(
        string jobDirectory)
    {
        if (!Directory.Exists(jobDirectory))
            return;

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(
                    jobDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .ToArray();
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException)
        {
            return;
        }

        var partFiles = files
            .Where(path => Path.GetFileName(path)
                .EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var hasNonEmptyPart = partFiles.Any(path =>
        {
            try { return new FileInfo(path).Length > 0; }
            catch (IOException) { return false; }
        });

        foreach (var file in files)
        {
            if (!IsCourseTemporaryFile(file))
                continue;

            try
            {
                var name = Path.GetFileName(file);
                var length = new FileInfo(file).Length;

                if (length == 0
                    || (!hasNonEmptyPart
                        && name.EndsWith(
                            ".ytdl",
                            StringComparison.OrdinalIgnoreCase)))
                    File.Delete(file);
            }
            catch (Exception ex) when (
                ex is IOException
                    or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool CourseOutputExists(
        string directory,
        string baseName)
    {
        if (!Directory.Exists(directory)) return false;
        var extensions = new HashSet<string>(
            new[] { ".mp4", ".mkv", ".webm", ".mov", ".mp3" },
            StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(directory, baseName + "*")
            .Any(path => extensions.Contains(Path.GetExtension(path))
                && new FileInfo(path).Length > 0);
    }
    private void UpdateCourseControls()
    {
        var busy = _courseDownloadActive || _operations.IsBusy;

        if (_mp3Button is not null)
            _mp3Button.IsEnabled = !busy;
        if (_textButton is not null)
            _textButton.IsEnabled = !busy;
        UpdateLocalMediaAvailabilityHint();

        if (_courseDownloadButton is not null)
            _courseDownloadButton.IsEnabled = !busy;

        if (_courseQualityBox is not null)
            _courseQualityBox.IsEnabled = !busy;

        if (_courseResumeButton is not null)
            _courseResumeButton.IsEnabled = !busy;

        if (_courseClearCacheButton is not null)
            _courseClearCacheButton.IsEnabled = !busy;

        if (_transcribeDownloadedButton is not null)
            _transcribeDownloadedButton.IsEnabled = !busy;

        if (_cancelButton is not null)
            _cancelButton.IsEnabled = true;

        UpdatePauseButtonsAvailability(busy || _isInstallingComponents);
    }
}
