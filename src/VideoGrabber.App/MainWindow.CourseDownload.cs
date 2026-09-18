using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private Button _courseDownloadButton = null!;
    private Button _transcribeDownloadedButton = null!;
    private CancellationTokenSource? _courseCancellation;
    private bool _courseDownloadActive;
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
        var path = _lastDownloadedMediaPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _browserHint.Text =
                "Сначала успешно скачайте видео.";
            return;
        }

        _localMediaBox.Text = path;
        _localOutputBaseBox.Text = Path.Combine(
            Path.GetDirectoryName(path)!,
            Path.GetFileNameWithoutExtension(path) + "-text");
        await RunLocalMediaAsync(text: true);
    }

    private async Task DownloadWholeGetCourseAsync()
    {
        if (_courseDownloadActive) return;
        if (_mediaBrowser?.CoreWebView2 is null)
        {
            _browserHint.Text =
                "Сначала откройте курс во встроенном браузере и войдите на GetCourse.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputFolderBox.Text))
        {
            _browserHint.Text =
                "Сначала выберите папку сохранения.";
            return;
        }

        var root = _browserPageUri;
        if (root is null)
        {
            _browserHint.Text = "Страница курса ещё не открыта.";
            return;
        }

        var originalCookieIndex = _cookiesBox.SelectedIndex;
        SelectEmbeddedBrowserSession();
        _courseDownloadActive = true;
        _courseCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _windowLifetime.Token);
        UpdateCourseControls();

        try
        {
            var token = _courseCancellation.Token;
            _browserHint.Text =
                "Считываю структуру курса: модули и уроки…";
            var plan = await BuildCoursePlanAsync(root, token);
            if (plan.Lessons.Length == 0)
            {
                _browserHint.Text =
                    "В структуре курса не найдено доступных уроков.";
                return;
            }

            var rootFolder = Path.Combine(
                Path.GetFullPath(_outputFolderBox.Text),
                GetCourseCourseStructure.CourseFolder(plan.CourseTitle));
            Directory.CreateDirectory(rootFolder);
            await DownloadCoursePlanAsync(plan, rootFolder, token);
        }
        catch (OperationCanceledException)
        {
            _browserHint.Text =
                "Скачивание курса остановлено. Уже готовые файлы сохранены.";
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write(
                "course.download",
                "failed",
                ex.GetType().Name);
            _browserHint.Text =
                "Не удалось обработать курс: " +
                VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                    ex.Message);
        }
        finally
        {
            _courseCancellation?.Dispose();
            _courseCancellation = null;
            _courseDownloadActive = false;
            _browserSession.RunProgrammaticSelectionCleanup(
                () => _cookiesBox.SelectedIndex = originalCookieIndex);
            UpdateCourseControls();
        }
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

        return await GetCourseCoursePlanner.BuildAsync(
            root,
            rootPage,
            async (uri, cancellationToken) =>
            {
                if (!await NavigateCoursePageAsync(uri, cancellationToken))
                    return null;
                return await WaitForCoursePageAsync(cancellationToken);
            },
            title => _browserHint.Text = $"Считываю модуль: {title}",
            token);
    }

    private async Task DownloadCoursePlanAsync(
        GetCourseCoursePlan plan,
        string rootFolder,
        CancellationToken token)
    {
        var completedLessons = 0;
        var completedVideos = 0;
        var failures = 0;

        for (var lessonIndex = 0;
             lessonIndex < plan.Lessons.Length;
             lessonIndex++)
        {
            token.ThrowIfCancellationRequested();
            var lesson = plan.Lessons[lessonIndex];
            _browserHint.Text =
                $"Курс: урок {lessonIndex + 1} из {plan.Lessons.Length} — {lesson.Title}";

            if (!await NavigateCoursePageAsync(lesson.Uri, token))
            {
                failures++;
                continue;
            }

            var candidates =
                await WaitForCourseMediaAsync(token);
            if (candidates.Count == 0)
            {
                failures++;
                DiagnosticHub.Log.Write(
                    "course.lesson",
                    "failed",
                    "No downloadable media on accessible lesson");
                continue;
            }

            var lessonFolder = lesson.ModuleFolders.Aggregate(
                rootFolder,
                Path.Combine);
            Directory.CreateDirectory(lessonFolder);
            var quality = CourseQuality();

            var lessonSucceeded = true;
            for (var videoIndex = 0;
                 videoIndex < candidates.Count;
                 videoIndex++)
            {
                token.ThrowIfCancellationRequested();
                var candidate = candidates[videoIndex];
                var baseName =
                    GetCourseCourseStructure.VideoBaseName(
                        lesson.LessonOrdinal,
                        lesson.Title,
                        videoIndex + 1,
                        candidates.Count,
                        quality);

                if (CourseOutputExists(lessonFolder, baseName))
                {
                    completedVideos++;
                    continue;
                }
                var intent = CaptureDownloadIntent(
                    candidate.Source,
                    quality) with
                {
                    OutputDirectory = lessonFolder,
                    AudioOnly = false
                };

                var outcome = await RunDownloadOperationAsync(
                    intent,
                    new BrowserDownloadPreparation(
                        this,
                        candidate,
                        candidate.PageOrdinal ?? videoIndex + 1,
                        queueContext: null,
                        suggestedBaseNameOverride: baseName),
                    resetCookieSelectionAfterUse: false,
                    managedKind: "course_download");

                if (outcome == OperationOutcome.Cancelled)
                    throw new OperationCanceledException(token);
                if (outcome != OperationOutcome.Succeeded)
                {
                    failures++;
                    lessonSucceeded = false;
                    continue;
                }
                completedVideos++;
            }

            if (lessonSucceeded) completedLessons++;
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
            // The downloaded files are more important than restoring the UI page.
        }

        _browserHint.Text =
            $"Курс обработан. Уроков успешно: {completedLessons}/{plan.Lessons.Length}. " +
            $"Видео скачано: {completedVideos}. Ошибок: {failures}.";
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
        Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, args) => completion.TrySetResult(args);
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
                    "Navigation timeout");
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

            await Task.Delay(350, token);
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
                (value || '').replace(/s+/g, ' ').trim();

              const selectors = [
                'a[href*="/teach/control/lesson/view"]',
                'a[href*="/teach/control/stream/view"]',
                '[onclick*="/teach/control/lesson/view"]',
                '[onclick*="/teach/control/stream/view"]'
              ].join(',');

              const all = [...document.querySelectorAll(selectors)];
              const preferred = all.filter(node =>
                node.closest(
                  '.lesson-list,.lesson-row,.training-row,' +
                  '.training-list,.stream-table,.stream-list,' +
                  '[class*="lesson-list"],[class*="training-list"]'));

              const nodes = preferred.length > 0 ? preferred : all;
              const urlOf = node => {
                const href = node.href || node.getAttribute?.('href');
                if (href) {
                  try { return new URL(href, document.baseURI); }
                  catch {}
                }
                const onclick = node.getAttribute?.('onclick') || '';
                const match = onclick.match(
                  /(?:https?://[^'"\s)]+|/(?:pl/)?teach/control/(?:lesson|stream)/view[^'"\s)]*)/i);
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
            return GetCourseCourseStructure.TryParsePage(
                json,
                current,
                out var page)
                ? page
                : null;
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

        for (var attempt = 0; attempt < 60; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var core = _mediaBrowser?.CoreWebView2;
            if (core is null) return [];

            var lease = _browserPages.Capture();
            if (IsCurrentBrowserPage(lease, core))
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

                if (stablePasses >= 3)
                    return candidates;
            }

            await Task.Delay(300, token);
        }

        return _mediaCandidatesBox.Items
            .OfType<ComboBoxItem>()
            .Select(item => item.Tag as MediaCandidate)
            .Where(item => item is not null)
            .Cast<MediaCandidate>()
            .ToArray();
    }

    private string CourseQuality()
        => (_qualityBox.SelectedItem as ComboBoxItem)?
               .Tag?.ToString()
           ?? "best";

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
        if (_courseDownloadButton is not null)
            _courseDownloadButton.IsEnabled =
                !_courseDownloadActive
                && !_operations.IsBusy;
        if (_transcribeDownloadedButton is not null)
            _transcribeDownloadedButton.IsEnabled =
                !_courseDownloadActive
                && !_operations.IsBusy
                && !string.IsNullOrWhiteSpace(
                    _lastDownloadedMediaPath)
                && File.Exists(_lastDownloadedMediaPath);
        if (_cancelButton is not null
            && _courseDownloadActive)
            _cancelButton.IsEnabled = true;
    }
}
