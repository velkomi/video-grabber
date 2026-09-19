using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private sealed record LessonArchiveResult(
        bool PageSaved,
        int AssetsSaved,
        int AssetErrors);

    private sealed record CoursePlayerDomItem(
        string? url,
        string? masterUrl,
        string? title,
        int order);

    private sealed record CourseTextFetch(
        Uri FinalUri,
        string Body,
        string? ContentType);
    private async Task<LessonArchiveResult> SaveCourseLessonArchiveAsync(
        GetCourseLessonPlan lesson,
        string lessonFolder,
        CancellationToken token)
    {
        var snapshot = await CaptureCourseLessonSnapshotAsync(token);
        if (snapshot is null)
        {
            DiagnosticHub.Log.Write(
                "course.archive",
                "failed",
                "Lesson snapshot unavailable");
            return new(false, 0, 0);
        }

        Directory.CreateDirectory(lessonFolder);

        var imageCount = snapshot.Assets.Count(asset =>
            string.Equals(asset.Kind, "image", StringComparison.OrdinalIgnoreCase));
        var fileCount = snapshot.Assets.Count - imageCount;
        DiagnosticHub.Log.Write(
            "course.archive.snapshot",
            "observed",
            $"images={imageCount} files={fileCount}");

        var docx = Path.Combine(
            lessonFolder,
            "Урок.docx");
        var html = Path.Combine(
            lessonFolder,
            "Страница.html");

        CourseLessonArchive.WriteDocx(
            docx,
            snapshot.Title,
            lesson.Uri,
            snapshot.Text);
        await File.WriteAllTextAsync(
            html,
            snapshot.Html,
            new UTF8Encoding(false),
            token);

        var saved = 0;
        var errors = 0;
        foreach (var asset in snapshot.Assets)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (await DownloadCourseAssetAsync(
                        asset,
                        lesson.Uri,
                        lessonFolder,
                        token))
                    saved++;
                else
                    errors++;
            }

            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                DiagnosticHub.Log.Write(
                    "course.asset",
                    "failed",
                    "host=" + asset.Source.IdnHost
                    + " error=" + ex.GetType().Name
                    + " message="
                    + VideoGrabber.Core.Security.SensitiveDataRedactor.Redact(
                        ex.Message));
            }
        }

        DiagnosticHub.Log.Write(
            "course.archive",
            "succeeded",
            "page saved; assets saved "
            + saved
            + "; asset errors "
            + errors);
        return new(true, saved, errors);
    }

    private async Task<CourseLessonSnapshot?>
        CaptureCourseLessonSnapshotAsync(
            CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2;
        var page = _browserPageUri;
        if (core is null || page is null)
            return null;

        var lease = _browserPages.Capture();
        if (!IsCurrentBrowserPage(lease, core))
            return null;

        const string script = """
            (() => {
              const clean = value =>
                (value || '').replace(/\s+/g, ' ').trim();

              const root =
                document.querySelector('.lite-page.block-set') ||
                document.querySelector('.standard-page-content .center-block') ||
                document.querySelector('.standard-page-content') ||
                document.body;

              const excludedSelector = [
                '.o-lt-lesson-comment-block',
                '.lt-lesson-comment-block',
                '.lesson-comment-block-1',
                '.lesson-answers-title',
                '.answers-list',
                '.self-answers',
                '.user-answer',
                '.answer_wrapper',
                '.comments',
                '.comments-tree-wrapper',
                '.gc-comment-form',
                '.new-comment',
                '.comment-user-settings-json',
                '.add-redesign-subblock',
                '.common-setting-link',
                '.lt-page-edit-link',
                '.lesson-navigation',
                '.gc-account-leftbar',
                '.gc-leftbar-bottom-badges',
                '.gc-mobile-bottom-icons'
              ].join(',');
              const isExcluded = node =>
                !!node.closest?.(excludedSelector);

              const titleNode = document.querySelector(
                '.lesson-title-value,.lesson-title,' +
                '.lesson-header h1,.page-header h2,' +
                '[class*="lesson-title"]');
              const title = clean(
                titleNode?.innerText ||
                titleNode?.textContent ||
                document.title);

              const assets = [];
              const seen = new Set();
              const filePattern =
                /\.(?:pdf|docx?|xlsx?|pptx?|zip|rar|7z|txt|csv|rtf|odt|ods|epub|jpg|jpeg|png|webp|gif|svg)(?:$|[?#])/i;
              const fileService = url => {
                const host = url.hostname.toLowerCase();
                const path = url.pathname.toLowerCase();
                return path.includes('/fileservice/file/')
                  || path.includes('/file/download')
                  || host === 'fs.getcourse.ru'
                  || host.startsWith('fs-')
                  || host.startsWith('fs.');
              };
              const add = (raw, kind, name) => {
                if (!raw || assets.length >= 500) return;
                let url;
                try { url = new URL(raw, document.baseURI); }
                catch { return; }
                if (!['http:', 'https:'].includes(url.protocol)) return;
                if (url.pathname.toLowerCase().includes('/sign-player/')) return;
                if (seen.has(url.href)) return;
                seen.add(url.href);
                assets.push({
                  url: url.href,
                  kind,
                  name: clean(name).slice(0, 160)
                });
              };

              root.querySelectorAll('a[href]').forEach(link => {
                if (isExcluded(link)) return;
                let url;
                try { url = new URL(link.href, document.baseURI); }
                catch { return; }
                if (link.hasAttribute('download')
                    || filePattern.test(url.href)
                    || fileService(url))
                  add(
                    url.href,
                    filePattern.test(url.href)
                      && /\.(?:jpg|jpeg|png|webp|gif|svg)(?:$|[?#])/i.test(url.href)
                      ? 'image' : 'file',
                    link.getAttribute('download')
                      || link.innerText
                      || link.getAttribute('title'));
              });

              root.querySelectorAll('img').forEach(img => {
                if (isExcluded(img)) return;
                const raw = img.currentSrc
                  || img.src
                  || img.getAttribute('data-src')
                  || img.getAttribute('data-original');
                const width = img.naturalWidth
                  || Number(img.getAttribute('width'))
                  || 0;
                const height = img.naturalHeight
                  || Number(img.getAttribute('height'))
                  || 0;
                if (width >= 80 || height >= 80 || (!width && !height))
                  add(raw, 'image', img.alt || img.title);
              });

              root.querySelectorAll(
                'object[data],embed[src],iframe[src]')
                .forEach(node => {
                  if (isExcluded(node)) return;
                  const raw = node.getAttribute('data')
                    || node.getAttribute('src');
                  if (!raw) return;
                  let url;
                  try { url = new URL(raw, document.baseURI); }
                  catch { return; }
                  if (filePattern.test(url.href)
                      || fileService(url))
                    add(
                      url.href,
                      'file',
                      node.getAttribute('title')
                        || node.getAttribute('name'));
                });

              const clone = root.cloneNode(true);
              clone.querySelectorAll(
                'style,link,script,noscript,template,iframe,object,embed,form,' +
                'input,button,textarea,select,svg,canvas,' +
                excludedSelector)
                .forEach(node => node.remove());
              clone.querySelectorAll('*').forEach(node => {
                [...node.attributes].forEach(attr => {
                  const name = attr.name.toLowerCase();
                  if (name.startsWith('on')
                      || name === 'srcdoc')
                    node.removeAttribute(attr.name);
                });
              });

              clone.querySelectorAll('[href]').forEach(node => {
                try {
                  node.setAttribute(
                    'href',
                    new URL(
                      node.getAttribute('href'),
                      document.baseURI).href);
                } catch {}
              });
              clone.querySelectorAll('[src]').forEach(node => {
                try {
                  node.setAttribute(
                    'src',
                    new URL(
                      node.getAttribute('src'),
                      document.baseURI).href);
                } catch {}
              });

              const textHost = document.createElement('div');
              textHost.style.cssText =
                'position:fixed;left:-100000px;top:0;width:900px;' +
                'visibility:hidden;pointer-events:none;';
              textHost.appendChild(clone.cloneNode(true));
              document.body.appendChild(textHost);
              const text = (textHost.innerText || '')
                .replace(/\u0000/g, '');
              textHost.remove();
              const safeTitle = title || 'Урок';
              const html =
                '<!doctype html><html><head><meta charset="utf-8">' +
                '<meta name="viewport" content="width=device-width,initial-scale=1">' +
                '<title>' +
                safeTitle.replace(/[&<>"']/g, c => ({
                  '&':'&amp;','<':'&lt;','>':'&gt;',
                  '"':'&quot;',"'":'&#39;'
                }[c])) +
                '</title><style>body{font-family:Segoe UI,Arial,sans-serif;' +
                'max-width:1000px;margin:32px auto;padding:0 20px;line-height:1.5}' +
                'img{max-width:100%;height:auto}</style></head><body>' +
                clone.outerHTML +
                '</body></html>';

              return { title: safeTitle, text, html, assets };
            })()
            """;

        try
        {
            var json = await core.ExecuteScriptAsync(script)
                .AsTask()
                .WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrentBrowserPage(lease, core))
                return null;

            return CourseLessonArchive.TryParseWebViewResult(
                json,
                page,
                out var snapshot)
                ? snapshot
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticHub.Log.Write(
                "course.archive.capture",
                "failed",
                ex.GetType().Name);
            return null;
        }
    }

    private async Task<bool> DownloadCourseAssetAsync(
        CourseLessonAsset asset,
        Uri referer,
        string lessonFolder,
        CancellationToken token)
    {
        var core = _mediaBrowser?.CoreWebView2
            ?? throw new InvalidOperationException(
                "Встроенный браузер закрыт.");
        var current = asset.Source;
        for (var redirect = 0; redirect <= 6; redirect++)
        {
            token.ThrowIfCancellationRequested();
            if (!UrlPolicy.TryValidate(
                    current.AbsoluteUri,
                    out var safe,
                    out _)
                || safe is null)
                return false;
            current = safe;

            _ = DownloadRouteResolver.ResolveAdapterId(
                _routePolicy,
                _routes,
                current,
                referer);
            var connector = new RouteConnector(_routePolicy);

            using var handler = new SocketsHttpHandler
            {
                UseCookies = false,
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(25),
                ConnectCallback = (context, cancellationToken) =>
                    new ValueTask<Stream>(connector.OpenAsync(
                        context.DnsEndPoint.Host,
                        context.DnsEndPoint.Port,
                        cancellationToken))
            };

            using var http = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                current)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            request.Headers.Referrer = referer;
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("*/*"));

            var userAgent = core.Settings.UserAgent;
            if (!string.IsNullOrWhiteSpace(userAgent))
                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    userAgent);

            var sendCookies =
                string.Equals(
                    current.IdnHost,
                    referer.IdnHost,
                    StringComparison.OrdinalIgnoreCase)
                || current.AbsolutePath.Contains(
                    "/pl/fileservice/",
                    StringComparison.OrdinalIgnoreCase);

            if (sendCookies)
            {
                var cookies = await core.CookieManager
                    .GetCookiesAsync(current.AbsoluteUri)
                    .AsTask()
                    .WaitAsync(token);
                var cookieHeader = string.Join(
                    "; ",
                    cookies
                        .Where(cookie =>
                            !string.IsNullOrWhiteSpace(cookie.Name)
                            && cookie.Name.IndexOfAny(['\r', '\n', ';']) < 0
                            && cookie.Value.IndexOfAny(['\r', '\n']) < 0)
                        .Select(cookie =>
                            cookie.Name + "=" + cookie.Value));
                if (!string.IsNullOrWhiteSpace(cookieHeader))
                    request.Headers.TryAddWithoutValidation(
                        "Cookie",
                        cookieHeader);
            }

            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token);
            if (IsCourseRedirect(response.StatusCode))
            {
                var location = response.Headers.Location;
                if (location is null) return false;
                current = location.IsAbsoluteUri
                    ? location
                    : new Uri(current, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                DiagnosticHub.Log.Write(
                    "course.asset",
                    "failed",
                    "host=" + current.IdnHost
                    + " HTTP=" + (int)response.StatusCode);
                return false;
            }

            var declaredLength =
                response.Content.Headers.ContentLength;
            const long maxAssetBytes = 1_000_000_000;
            if (declaredLength is > maxAssetBytes)
            {
                DiagnosticHub.Log.Write(
                    "course.asset",
                    "blocked",
                    "host=" + current.IdnHost
                    + " asset exceeds size limit");
                return false;
            }

            var disposition =
                response.Content.Headers.ContentDisposition;
            var serverName =
                disposition?.FileNameStar
                ?? disposition?.FileName;
            serverName = serverName?.Trim().Trim('"');

            var fallback = asset.Kind == "image"
                ? "Изображение"
                : "Вложение";
            var fileName =
                CourseLessonArchive.AssetFileName(
                    current,
                    asset.SuggestedName,
                    fallback,
                    serverName);

            if (string.IsNullOrWhiteSpace(
                    Path.GetExtension(fileName)))
            {
                var inferred = ExtensionForContentType(
                    response.Content.Headers.ContentType?.MediaType);
                if (inferred is not null)
                    fileName += inferred;
            }

            var targetDirectory = Path.Combine(
                lessonFolder,
                asset.Kind == "image"
                    ? "Изображения"
                    : "Вложения");
            Directory.CreateDirectory(targetDirectory);
            foreach (var stale in Directory.EnumerateFiles(
                         targetDirectory,
                         "*.partial",
                         SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(stale); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            var target = Path.Combine(
                targetDirectory,
                fileName);
            if (File.Exists(target))
            {
                if (new FileInfo(target).Length > 0)
                    return true;
                File.Delete(target);
            }

            var temp = target + ".partial";
            try
            {
                long total = 0;
                await using (var input =
                    await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(
                    temp,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    131072,
                    useAsync: true))
                {
                    var buffer = new byte[131072];
                    while (true)
                    {
                        var read = await input.ReadAsync(
                            buffer,
                            token);
                        if (read == 0) break;
                        total += read;
                        if (total > maxAssetBytes)
                            throw new InvalidDataException(
                                "Course asset exceeded size limit.");
                        await output.WriteAsync(
                            buffer.AsMemory(0, read),
                            token);
                    }
                    await output.FlushAsync(token);
                }

                if (total == 0)
                    throw new InvalidDataException(
                        "Course asset is empty.");

                if (File.Exists(target))
                    File.Delete(target);
                File.Move(temp, target);
                DiagnosticHub.Log.Write(
                    "course.asset",
                    "succeeded",
                    "host=" + current.IdnHost
                    + " kind=" + asset.Kind
                    + " bytes=" + total);
                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch { }
                throw;
            }
        }

        return false;
    }

    private static bool IsCourseRedirect(
        HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static string AvailableCourseAssetPath(
        string directory,
        string fileName)
    {
        var safe = string.IsNullOrWhiteSpace(fileName)
            ? "Вложение"
            : fileName;
        var candidate = Path.Combine(directory, safe);
        if (!File.Exists(candidate)
            || new FileInfo(candidate).Length == 0)
            return candidate;

        var stem = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);
        for (var index = 2; index < 10_000; index++)
        {
            candidate = Path.Combine(
                directory,
                $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
        throw new IOException(
            "Слишком много файлов с одинаковым именем.");
    }

    private static string? ExtensionForContentType(
        string? mediaType)
        => mediaType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "application/rtf" => ".rtf",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            _ => null
        };
}
