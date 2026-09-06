# VideoGrabber Downloader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Создать проверенный portable VideoGrabber.exe для анализа и загрузки разрешённых незашифрованных видео с YouTube, Rutube, 1tv.ru, прямых MP4, HLS и DASH.

**Architecture:** WinUI 3 отвечает только за представление и вызывает application-сервисы. Core не зависит от UI и описывает задачи, форматы, стратегии и ошибки; Infrastructure изолирует yt-dlp, N_m3u8DL-RE, FFmpeg/FFprobe, WebView2 и SQLite. Любой внешний процесс запускается через единый безопасный ProcessRunner, а готовый файл публикуется только после валидации.

**Tech Stack:** C# 14, .NET 10, WinUI 3, Microsoft.WindowsAppSDK 2.4.0, Microsoft.Web.WebView2 1.0.4191.47, Microsoft.Data.Sqlite, xUnit, PowerShell 7, yt-dlp nightly, N_m3u8DL-RE, FFmpeg и FFprobe.

**Spec:** C:\Users\Oleg\Documents\Codex\2026-09-06\new-chat\outputs\VideoGrabber-design.md

## Global Constraints

- Project root: D:\CODEX\Projects\2026-09-06\video-grabber.
- Target: Windows 10 Enterprise LTSC 2021 21H2 build 19044 x64 and Windows 11 x64.
- Deliverable: unpackaged self-contained win-x64 portable folder launched only through VideoGrabber.exe; no console window.
- No DRM, CAPTCHA, paywall, geo-control, or account-control bypass.
- No telemetry or cloud processing.
- Never print cookie values, Authorization headers, full signed query strings, or browser profile contents.
- Source, completed, and recovery files are never removed or overwritten without an explicit user action.
- External process arguments use ProcessStartInfo.ArgumentList; UseShellExecute is false and CreateNoWindow is true.
- A job reaches Completed only after FFprobe and FFmpeg decode validation succeeds.
- NuGet restore, SDK installation, binary downloads, and system changes require an explicit approval at execution time.
- Every task ends with focused tests and a local commit; push, merge, release, and publication are excluded.

---

## File Map

### Repository and build

- global.json — pins the installed .NET 10 SDK.
- Directory.Build.props — nullable, analyzers, warnings, deterministic build, Windows target.
- Directory.Packages.props — central stable NuGet versions.
- VideoGrabber.slnx — solution membership.
- scripts/Verify-Environment.ps1 — read-only prerequisite report.
- scripts/Build-Portable.ps1 — deterministic win-x64 publish and runtime layout.
- runtime/components.lock.json — official component sources, versions, filenames, hashes, and licenses.

### Core

- src/VideoGrabber.Core/Downloads/DownloadModels.cs — URLs, formats, jobs, progress, results, and error codes.
- src/VideoGrabber.Core/Downloads/IDownloadAnalyzer.cs — analyzer contract.
- src/VideoGrabber.Core/Downloads/IDownloadExecutor.cs — executor contract.
- src/VideoGrabber.Core/Downloads/DownloadOrchestrator.cs — ordered strategy selection.
- src/VideoGrabber.Core/Jobs/DownloadQueue.cs — queue transitions and cancellation.
- src/VideoGrabber.Core/Jobs/IJobStore.cs — durable job contract.
- src/VideoGrabber.Core/Security/UrlPolicy.cs — allowed URL schemes and local-network blocking for detector input.
- src/VideoGrabber.Core/Security/LogRedactor.cs — secret removal.
- src/VideoGrabber.Core/Security/FileNamePolicy.cs — safe Windows output names and containment.
- src/VideoGrabber.Core/Storage/DiskSpacePolicy.cs — conservative preflight free-space decision.
- src/VideoGrabber.Core/Media/MediaProbe.cs — UI-independent probed media metadata shared with the editor.
- src/VideoGrabber.Core/Settings/AppSettings.cs — typed folders, theme, history, and update preferences.

### Infrastructure

- src/VideoGrabber.Infrastructure/Processes/ProcessRunner.cs — safe process execution and structured lines.
- src/VideoGrabber.Infrastructure/Components/ComponentCatalog.cs — paths and health.
- src/VideoGrabber.Infrastructure/YtDlp/YtDlpClient.cs — JSON analysis and download progress.
- src/VideoGrabber.Infrastructure/YtDlp/BrowserCookieSource.cs — explicit one-run browser-cookie selection without value exposure.
- src/VideoGrabber.Infrastructure/Direct/HttpFileClient.cs — resumable direct HTTP media download.
- src/VideoGrabber.Infrastructure/Media/FfmpegClient.cs — mux/remux and decode checks.
- src/VideoGrabber.Infrastructure/Media/FfprobeClient.cs — media metadata.
- src/VideoGrabber.Infrastructure/Media/MediaValidator.cs — final validation policy.
- src/VideoGrabber.Infrastructure/Manifests/Nm3u8DlClient.cs — HLS/DASH/MSS execution.
- src/VideoGrabber.Infrastructure/Sites/OneTvAdapter.cs — 1tv playlist extraction.
- src/VideoGrabber.Infrastructure/Web/StreamDetector.cs — WebView2 media request capture.
- src/VideoGrabber.Infrastructure/Jobs/SqliteJobStore.cs — queue persistence.
- src/VideoGrabber.Infrastructure/Settings/JsonSettingsStore.cs — atomic local settings persistence.
- src/VideoGrabber.Infrastructure/Updates/ComponentUpdater.cs — verified download and rollback.

### App

- src/VideoGrabber.App/App.xaml and App.xaml.cs — application startup.
- src/VideoGrabber.App/MainWindow.xaml and MainWindow.xaml.cs — shell and navigation.
- src/VideoGrabber.App/Downloads/DownloaderPage.xaml and DownloaderPage.xaml.cs — downloader screen.
- src/VideoGrabber.App/Downloads/DownloaderViewModel.cs — UI state and commands.
- src/VideoGrabber.App/Downloads/DownloadJobViewModel.cs — one queue card.
- src/VideoGrabber.App/Settings/SettingsPage.xaml and SettingsPage.xaml.cs — folders, theme, history, and components.
- src/VideoGrabber.App/Settings/SettingsViewModel.cs — settings state.
- src/VideoGrabber.App/Styles/ThemeResources.xaml — Fluent colors, spacing, typography, and states.
- src/VideoGrabber.App/Styles/CardStyles.xaml — reusable media and queue card styles.
- src/VideoGrabber.App/Services/AppPaths.cs — portable-safe data paths.
- src/VideoGrabber.App/Services/CompositionRoot.cs — dependency construction.

### Tests

- tests/VideoGrabber.Core.Tests — pure domain and security tests.
- tests/VideoGrabber.Infrastructure.Tests — process, adapter, persistence, and media tests.
- tests/VideoGrabber.App.Tests — view-model tests.
- tests/fixtures — tiny owned MP4/HLS/DASH and sanitized JSON fixtures.
- tests/smoke/Invoke-DownloaderSmoke.ps1 — approved network smoke runner.

---

### Task 1: Bootstrap a runnable portable WinUI shell

**Files:**
- Create: global.json, Directory.Build.props, Directory.Packages.props, VideoGrabber.slnx
- Create: src/VideoGrabber.App/VideoGrabber.App.csproj
- Create: src/VideoGrabber.Core/VideoGrabber.Core.csproj
- Create: src/VideoGrabber.Infrastructure/VideoGrabber.Infrastructure.csproj
- Create: tests/VideoGrabber.Core.Tests/VideoGrabber.Core.Tests.csproj
- Create: tests/VideoGrabber.Infrastructure.Tests/VideoGrabber.Infrastructure.Tests.csproj
- Create: tests/VideoGrabber.App.Tests/VideoGrabber.App.Tests.csproj
- Create: src/VideoGrabber.App/App.xaml, src/VideoGrabber.App/App.xaml.cs
- Create: src/VideoGrabber.App/MainWindow.xaml, src/VideoGrabber.App/MainWindow.xaml.cs
- Create: scripts/Verify-Environment.ps1

**Interfaces:**
- Consumes: approved design only.
- Produces: a buildable solution and VideoGrabber.App executable shell.

- [ ] **Step 1: Verify prerequisites without changing the machine**

Run:

~~~powershell
pwsh -NoProfile -File .\scripts\Verify-Environment.ps1
~~~

The script must report OS build, architecture, dotnet SDKs, WebView2 Runtime registry presence, Git, FFmpeg, FFprobe, yt-dlp, N_m3u8DL-RE, and free disk space without printing environment-variable values.

- [ ] **Step 2: Obtain approval and install the missing official .NET 10 SDK**

After approval, use the official Microsoft distribution. Run dotnet --version and write the exact returned 10.0.x SDK version into global.json with rollForward set to latestPatch.

~~~json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
~~~

If the installed feature band differs from 10.0.400, replace only that value with the exact installed stable SDK version before the first restore.

- [ ] **Step 3: Create the solution and projects**

Run dotnet new commands for class libraries and xUnit projects, then create the WinUI project with these fixed properties:

~~~xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
  <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
  <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  <WindowsPackageType>None</WindowsPackageType>
  <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <SelfContained>true</SelfContained>
</PropertyGroup>
~~~

- [ ] **Step 4: Pin stable packages centrally**

Directory.Packages.props must pin Microsoft.WindowsAppSDK 2.4.0, Microsoft.Web.WebView2 1.0.4191.47, Microsoft.Data.Sqlite 10.0.11, Microsoft.NET.Test.Sdk 18.9.0, xunit 2.9.3, and xunit.runner.visualstudio 3.1.5. Enable RestorePackagesWithLockFile and commit packages.lock.json files.

- [ ] **Step 5: Add the minimal Fluent shell**

MainWindow uses NavigationView with three items named Загрузчик, Редактор, Настройки. Редактор is visible but disabled with tooltip Будет добавлен вторым этапом. Add a title bar, system theme behavior, 900×650 default size, and a minimum 720×520 size.

- [ ] **Step 6: Build and run tests**

Run:

~~~powershell
dotnet restore --locked-mode
dotnet build .\VideoGrabber.slnx -c Debug --no-restore
dotnet test .\VideoGrabber.slnx -c Debug --no-build
~~~

Expected: restore, build, and all template tests succeed; VideoGrabber.App launches without a console window on build 19044.

- [ ] **Step 7: Commit**

~~~powershell
git add global.json Directory.Build.props Directory.Packages.props VideoGrabber.slnx src tests scripts
git commit -m "build: bootstrap VideoGrabber WinUI solution"
~~~

---

### Task 2: Define download models, URL policy, and sanitized diagnostics

**Files:**
- Create: src/VideoGrabber.Core/Downloads/DownloadModels.cs
- Create: src/VideoGrabber.Core/Downloads/IDownloadAnalyzer.cs
- Create: src/VideoGrabber.Core/Downloads/IDownloadExecutor.cs
- Create: src/VideoGrabber.Core/Security/UrlPolicy.cs
- Create: src/VideoGrabber.Core/Security/LogRedactor.cs
- Create: src/VideoGrabber.Core/Security/FileNamePolicy.cs
- Create: src/VideoGrabber.Core/Storage/DiskSpacePolicy.cs
- Test: tests/VideoGrabber.Core.Tests/Downloads/DownloadModelsTests.cs
- Test: tests/VideoGrabber.Core.Tests/Security/UrlPolicyTests.cs
- Test: tests/VideoGrabber.Core.Tests/Security/LogRedactorTests.cs
- Test: tests/VideoGrabber.Core.Tests/Security/FileNamePolicyTests.cs
- Test: tests/VideoGrabber.Core.Tests/Storage/DiskSpacePolicyTests.cs

**Interfaces:**
- Consumes: none beyond BCL.
- Produces: AnalyzeRequest, MediaDescriptor, MediaFormat, DownloadOptions, DownloadRequest, DownloadProgress, DownloadResult, DownloadErrorCode, IDownloadAnalyzer.AnalyzeAsync, IDownloadExecutor.DownloadAsync.

- [ ] **Step 1: Write failing contract and security tests**

Cover https acceptance, http acceptance, ftp/file rejection, userinfo rejection, loopback/private host rejection for WebView2 detector mode, Windows filename sanitization, output-path containment, known/estimated/unknown download sizes, safety reserve, insufficient space, and redaction of Cookie, Authorization, token, sig, signature, policy, key and expires query values.

~~~csharp
[Theory]
[InlineData("file:///C:/secret.txt")]
[InlineData("https://user:pass@example.com/video")]
public void Validate_rejects_unsafe_urls(string value)
{
    var result = UrlPolicy.ValidateUserUrl(value);
    Assert.False(result.IsAllowed);
}

[Fact]
public void Redact_removes_authorization_and_signed_query()
{
    var input = "Authorization: Bearer abc https://cdn/x.m3u8?token=secret&id=42";
    var text = LogRedactor.Redact(input);
    Assert.DoesNotContain("abc", text);
    Assert.DoesNotContain("secret", text);
    Assert.Contains("id=42", text);
}
~~~

- [ ] **Step 2: Run tests and confirm failure**

~~~powershell
dotnet test .\tests\VideoGrabber.Core.Tests -c Debug --filter "UrlPolicyTests|LogRedactorTests|DownloadModelsTests"
~~~

Expected: compile failure because the contracts do not exist.

- [ ] **Step 3: Implement immutable contracts**

Use records and enums. The public signatures are:

~~~csharp
public sealed record AnalyzeRequest(Uri Source, bool AllowAuthenticatedSession);
public sealed record MediaFormat(string Id, string Label, int? Width, int? Height,
    double? Fps, string? VideoCodec, string? AudioCodec, string Container,
    long? EstimatedBytes, Uri? DirectUri);
public sealed record MediaDescriptor(string Id, Uri Source, string Title,
    TimeSpan? Duration, Uri? Thumbnail, IReadOnlyList<MediaFormat> Formats,
    string Analyzer);
public enum BrowserCookieSourceKind { None, Edge, Chrome, Firefox }
public enum DownloadErrorCode
{
    None, InvalidUrl, Unsupported, RequiresLogin, GeoRestricted,
    DrmProtected, CaptchaRequired, NotFound, ExpiredStream, Network,
    InsufficientSpace, ProcessingFailed, ValidationFailed, Cancelled
}
public sealed record DownloadOptions(bool EmbedSubtitles, bool EmbedThumbnail,
    bool EmbedMetadata, bool AudioOnly, BrowserCookieSourceKind CookieSource);
public sealed record DownloadRequest(Guid JobId, MediaDescriptor Media,
    string FormatId, string OutputDirectory, string Container,
    DownloadOptions Options);
public sealed record DownloadProgress(Guid JobId, double? Percent,
    long DownloadedBytes, long? TotalBytes, double? BytesPerSecond, TimeSpan? Eta);
public sealed record DownloadResult(Guid JobId, bool IsSuccess,
    string? OutputPath, DownloadErrorCode ErrorCode, string? SafeMessage);

public interface IDownloadAnalyzer
{
    int Priority { get; }
    bool CanAnalyze(Uri source);
    Task<MediaDescriptor?> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken);
}

public interface IDownloadExecutor
{
    string Name { get; }
    bool CanExecute(MediaDescriptor media, MediaFormat format);
    Task<DownloadResult> DownloadAsync(DownloadRequest request,
        IProgress<DownloadProgress> progress, CancellationToken cancellationToken);
}
~~~

- [ ] **Step 4: Implement URL and log policies**

UrlPolicy performs syntax/scheme/userinfo checks synchronously. Detector-mode network safety resolves host addresses immediately before navigation and rejects loopback, link-local, multicast, RFC1918, and IPv6 unique-local addresses. LogRedactor parses header-shaped lines and URLs instead of relying on one broad regex. FileNamePolicy replaces invalid characters, rejects reserved device names, caps the stem at 180 characters and verifies final containment under the chosen output directory. DiskSpacePolicy requires estimated output bytes plus temporary streams plus max(1 GiB, 10 percent) reserve; an unknown size requires at least 5 GiB free and a visible warning.

- [ ] **Step 5: Run focused and full tests**

~~~powershell
dotnet test .\tests\VideoGrabber.Core.Tests -c Debug
dotnet test .\VideoGrabber.slnx -c Debug
~~~

Expected: all tests pass.

- [ ] **Step 6: Commit**

~~~powershell
git add src/VideoGrabber.Core tests/VideoGrabber.Core.Tests
git commit -m "feat: define safe download contracts"
~~~

---

### Task 3: Add safe external-process and component boundaries

**Files:**
- Create: src/VideoGrabber.Infrastructure/Processes/ProcessCommand.cs
- Create: src/VideoGrabber.Infrastructure/Processes/ProcessRunner.cs
- Create: src/VideoGrabber.Infrastructure/Components/ComponentCatalog.cs
- Create: runtime/components.lock.json
- Test: tests/VideoGrabber.Infrastructure.Tests/Processes/ProcessRunnerTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Components/ComponentCatalogTests.cs
- Create: tests/fixtures/process/echo-args.ps1

**Interfaces:**
- Consumes: LogRedactor.Redact(string).
- Produces: IProcessRunner.RunAsync(ProcessCommand, IProgress<ProcessLine>?, CancellationToken), IComponentCatalog.Get(ComponentId).

- [ ] **Step 1: Write failing tests**

Assert that arguments containing spaces and shell metacharacters arrive unchanged, CreateNoWindow is true, cancellation kills the process tree, stdout/stderr lines are delivered separately, and reported lines are redacted.

~~~csharp
var command = new ProcessCommand(
    "pwsh",
    ["-NoProfile", "-File", fixture, "a b", "& whoami", "token=secret"],
    workingDirectory);
var result = await runner.RunAsync(command, progress, CancellationToken.None);
Assert.Equal(0, result.ExitCode);
Assert.Contains("a b", result.StandardOutput);
Assert.DoesNotContain("secret", progressLines);
~~~

- [ ] **Step 2: Run tests and confirm failure**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "ProcessRunnerTests|ComponentCatalogTests"
~~~

Expected: compile failure for missing types.

- [ ] **Step 3: Implement ProcessRunner**

ProcessCommand contains FileName, IReadOnlyList<string> Arguments, WorkingDirectory, and optional safe environment allowlist. ProcessRunner creates ProcessStartInfo with UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true, and adds each argument through ArgumentList.Add. On cancellation call Kill(entireProcessTree: true), await exit, and return a Cancelled result.

- [ ] **Step 4: Implement the component catalog**

components.lock.json entries contain id, version, relativePath, sourceUrl, sha256, license and enabled. The initial required ids are yt-dlp, yt-dlp-ejs, deno, ffmpeg, ffprobe and N_m3u8DL-RE. ComponentCatalog resolves each path under the application runtime directory, rejects traversal, verifies SHA-256, and exposes Healthy, Missing or HashMismatch.

- [ ] **Step 5: Run all tests**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug
~~~

Expected: all tests pass, and no process test opens a visible console.

- [ ] **Step 6: Commit**

~~~powershell
git add src/VideoGrabber.Infrastructure runtime tests/VideoGrabber.Infrastructure.Tests
git commit -m "feat: add verified process component boundary"
~~~

---

### Task 4: Integrate yt-dlp analysis and downloading

**Files:**
- Create: src/VideoGrabber.Infrastructure/YtDlp/YtDlpJsonModels.cs
- Create: src/VideoGrabber.Infrastructure/YtDlp/YtDlpClient.cs
- Create: src/VideoGrabber.Infrastructure/YtDlp/BrowserCookieSource.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/YtDlp/YtDlpClientTests.cs
- Create: tests/fixtures/ytdlp/youtube-info.json
- Create: tests/fixtures/ytdlp/rutube-info.json
- Create: tests/fixtures/ytdlp/progress-lines.txt

**Interfaces:**
- Consumes: IDownloadAnalyzer, IDownloadExecutor, IProcessRunner, IComponentCatalog.
- Produces: YtDlpClient implementing IDownloadAnalyzer and IDownloadExecutor.

- [ ] **Step 1: Add sanitized fixtures and failing parser tests**

Test title/duration/thumbnail mapping, video-only and audio-only format mapping, combined formats, absent filesize, playlist rejection in version one, and progress/after-move parsing.

~~~csharp
var media = await client.ParseAnalysisFixtureAsync("youtube-info.json");
Assert.Equal("fixture-video", media.Id);
Assert.Contains(media.Formats, f => f.Height == 1080 && f.VideoCodec != null);
Assert.DoesNotContain(media.Formats, f => f.Id == "playlist");
~~~

- [ ] **Step 2: Verify failure**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter YtDlpClientTests
~~~

Expected: compile failure because YtDlpClient is absent.

- [ ] **Step 3: Implement analysis**

Run yt-dlp with --ignore-config, --no-playlist, --dump-single-json, --no-warnings, --windows-filenames, --ffmpeg-location, --plugin-dirs pointing only to the verified bundled yt-dlp-ejs directory, and --js-runtimes deno:<verified path>. Missing or unhealthy Deno/yt-dlp-ejs produces a component-health error before YouTube analysis. Parse JSON through System.Text.Json source generation. Never place a URL in a log before LogRedactor.

- [ ] **Step 4: Implement download and progress**

Use an exact selected format id. Map DownloadOptions to fixed switches: --embed-subs, --embed-thumbnail, --embed-metadata and audio-only presets. BrowserCookieSourceKind.None adds no browser arguments; an explicitly selected Edge, Chrome or Firefox adds --cookies-from-browser with only the enum-mapped browser name. Never accept a browser/profile argument from free text and never log the expanded cookie command. Use:

~~~text
--newline
--progress-template
download:%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s
--print
after_move:filepath
--continue
--part
--no-overwrites
~~~

Map expected extractor errors to RequiresLogin, GeoRestricted, DrmProtected, CaptchaRequired, NotFound, ExpiredStream or Network. Return the produced path only from the after_move line and verify it remains inside the selected output directory.

- [ ] **Step 5: Run focused tests**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter YtDlpClientTests
~~~

Expected: all fixture tests pass without network access.

- [ ] **Step 6: Run an approved harmless live analysis**

After explicit approval for network access and official binary download, verify yt-dlp --version and analyze one public test URL with download disabled. Do not use browser cookies.

- [ ] **Step 7: Test the explicit cookie boundary**

With a fake ProcessRunner, assert that the default command contains no cookie switch, each approved browser enum maps to exactly one fixed --cookies-from-browser value, unsupported values are rejected before process start, and sanitized diagnostics contain neither cookie database paths nor expanded values.

- [ ] **Step 8: Commit**

~~~powershell
git add src/VideoGrabber.Infrastructure/YtDlp tests/VideoGrabber.Infrastructure.Tests/YtDlp tests/fixtures/ytdlp
git commit -m "feat: integrate yt-dlp analysis and download"
~~~

---

### Task 5: Persist and execute the recoverable download queue

**Files:**
- Create: src/VideoGrabber.Core/Jobs/DownloadJob.cs
- Create: src/VideoGrabber.Core/Jobs/DownloadQueue.cs
- Create: src/VideoGrabber.Core/Jobs/IJobStore.cs
- Create: src/VideoGrabber.Infrastructure/Jobs/SqliteJobStore.cs
- Test: tests/VideoGrabber.Core.Tests/Jobs/DownloadQueueTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Jobs/SqliteJobStoreTests.cs

**Interfaces:**
- Consumes: DownloadRequest, DownloadProgress, DownloadResult, IDownloadExecutor.
- Produces: DownloadJobState and DownloadQueue.EnqueueAsync, PauseAsync, ResumeAsync, CancelAsync, RestoreAsync.

- [ ] **Step 1: Write failing state-machine tests**

Allowed transitions are Queued→Analyzing→Downloading→Processing→Validating→Completed and any active state→Paused/Failed/Cancelled. Completed cannot transition. Restored active jobs become Paused with safe message Приложение было закрыто; продолжите задачу.

~~~csharp
await queue.EnqueueAsync(request, CancellationToken.None);
await queue.StartNextAsync(CancellationToken.None);
Assert.Equal(DownloadJobState.Downloading, queue.Jobs.Single().State);
await queue.PauseAsync(request.JobId);
Assert.Equal(DownloadJobState.Paused, queue.Jobs.Single().State);
~~~

- [ ] **Step 2: Run tests and confirm failure**

~~~powershell
dotnet test .\tests\VideoGrabber.Core.Tests -c Debug --filter DownloadQueueTests
~~~

- [ ] **Step 3: Implement the single-worker queue**

Version one runs one download at a time to reduce site throttling and simplify recovery. Pause is implemented as process cancellation while preserving .part and segment files; resume starts the same request with --continue. Cancellation retains recovery data until the user separately requests cleanup.

- [ ] **Step 4: Add SQLite persistence**

Create schema_version and jobs tables in data/jobs.db. Use parameterized SQL only. Persist normalized source URL with sensitive query values redacted, selected format metadata, safe output path, state, byte counts, and timestamps; do not store cookie or Authorization data.

- [ ] **Step 5: Test restart recovery and concurrency**

Use a temporary database. Verify two concurrent callers cannot start the same job, persisted job ordering is stable, and a corrupt database is moved to jobs.corrupt-<timestamp>.db without deletion and replaced only after showing a recovery warning.

- [ ] **Step 6: Run all tests and commit**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug
git add src/VideoGrabber.Core/Jobs src/VideoGrabber.Infrastructure/Jobs tests
git commit -m "feat: add recoverable download queue"
~~~

---

### Task 6: Add FFmpeg processing and mandatory media validation

**Files:**
- Create: src/VideoGrabber.Core/Media/MediaProbe.cs
- Create: src/VideoGrabber.Infrastructure/Media/FfprobeClient.cs
- Create: src/VideoGrabber.Infrastructure/Media/FfmpegClient.cs
- Create: src/VideoGrabber.Infrastructure/Media/MediaValidator.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Media/FfprobeClientTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Media/MediaValidatorTests.cs
- Create: tests/fixtures/media/av-3s.mp4
- Create: tests/fixtures/media/truncated.mp4

**Interfaces:**
- Consumes: IProcessRunner and component catalog.
- Produces: FfprobeClient.ProbeAsync, FfmpegClient.RemuxAsync, MediaValidator.ValidateAsync.

- [ ] **Step 1: Create tiny owned fixtures and failing tests**

Generate a 3-second color/test-tone MP4 using the already installed FFmpeg; document the exact generation command in tests/fixtures/media/README.md. Produce truncated.mp4 by copying a fixture and truncating the copy through a deterministic test helper.

~~~csharp
var result = await validator.ValidateAsync(validPath, CancellationToken.None);
Assert.True(result.IsValid);
Assert.Equal(320, result.Probe.Width);
Assert.True((result.Probe.Duration - TimeSpan.FromSeconds(3)).Duration() < TimeSpan.FromMilliseconds(150));

var broken = await validator.ValidateAsync(truncatedPath, CancellationToken.None);
Assert.False(broken.IsValid);
~~~

- [ ] **Step 2: Implement FFprobe JSON parsing**

Run ffprobe with -v error -show_streams -show_format -of json. Require one decodable video stream for video mode, positive duration for VOD, and a path inside the requested output directory.

The shared Core type is:

~~~csharp
public sealed record MediaProbe(
    TimeSpan? Duration,
    string? FormatName,
    string? VideoCodec,
    string? PixelFormat,
    int? Width,
    int? Height,
    string? FrameRate,
    string? TimeBase,
    string? AudioCodec,
    int? AudioSampleRate,
    string? AudioChannelLayout,
    long? FileSize);
~~~

- [ ] **Step 3: Implement mux/remux without overwrites**

Write to <final>.videograbber.tmp.<extension> using -map 0:v:0 -map 1:a:0 -c copy and -movflags +faststart for MP4. Pass -n. Atomically move the temporary file only after validation.

- [ ] **Step 4: Implement decode sampling**

For files under 30 seconds decode the entire file to null. Otherwise decode 5 seconds at start, midpoint, and max(duration−10 seconds, 0). Every command must return exit code 0.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "FfprobeClientTests|MediaValidatorTests"
git add src/VideoGrabber.Infrastructure/Media tests/fixtures/media tests/VideoGrabber.Infrastructure.Tests/Media
git commit -m "feat: validate downloaded media"
~~~

---

### Task 7: Add 1tv.ru and manifest fallback

**Files:**
- Create: src/VideoGrabber.Infrastructure/Sites/ISiteAdapter.cs
- Create: src/VideoGrabber.Infrastructure/Sites/OneTvAdapter.cs
- Create: src/VideoGrabber.Infrastructure/Manifests/Nm3u8DlClient.cs
- Create: src/VideoGrabber.Infrastructure/Direct/HttpFileClient.cs
- Create: src/VideoGrabber.Core/Downloads/DownloadOrchestrator.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Sites/OneTvAdapterTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Manifests/Nm3u8DlClientTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Direct/HttpFileClientTests.cs
- Test: tests/VideoGrabber.Core.Tests/Downloads/DownloadOrchestratorTests.cs
- Create: tests/fixtures/1tv/page.html
- Create: tests/fixtures/1tv/playlist.json
- Create: tests/fixtures/manifests/master.m3u8

**Interfaces:**
- Consumes: IDownloadAnalyzer, IDownloadExecutor, IProcessRunner, UrlPolicy.
- Produces: ISiteAdapter, OneTvAdapter, Nm3u8DlClient, HttpFileClient, DownloadOrchestrator.AnalyzeAsync and DownloadAsync.

- [ ] **Step 1: Write orchestration and adapter tests**

Assert order: matching site adapter, yt-dlp, then WebView2 detector. A null/unsupported result advances to the next analyzer; RequiresLogin, DRM and CAPTCHA stop and surface the classified error. Verify 1tv video id extraction and playlist format mapping from sanitized fixtures.

- [ ] **Step 2: Implement OneTvAdapter**

Accept only 1tv.ru hosts, follow at most five HTTPS redirects within the URL policy, parse the page video id, request the endpoint used by the page, and map HLS/MP4 entries. Probe a small part of each candidate before labeling quality; do not infer resolution from a filename.

- [ ] **Step 3: Implement N_m3u8DL-RE execution**

Provide manifest URL, save directory, safe save name, temp directory, retry count 5, timeout 30 seconds, binary merge with FFmpeg, and no deletion of temp segments before MediaValidator succeeds. Parse progress into DownloadProgress; classify expired 403/410 manifests as ExpiredStream.

- [ ] **Step 4: Implement DownloadOrchestrator**

Sort analyzers by Priority. Store the analyzer/executor name inside MediaDescriptor so download uses the same strategy. Re-analysis creates a new descriptor but keeps the job id and user options.

- [ ] **Step 5: Implement resumable direct HTTP download**

HttpFileClient accepts only a MediaFormat.DirectUri already approved by UrlPolicy. It performs HEAD when available, follows at most five allowed HTTPS redirects, checks Content-Type and size, writes to a .part file with Range and If-Range/ETag resume, restarts safely when the server ignores Range, and atomically promotes only after MediaValidator succeeds. Tests use an in-process HTTP handler for full, ranged, ignored-range, changed-ETag, 403-expired and interrupted responses.

- [ ] **Step 6: Run offline tests**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug --filter "OneTvAdapterTests|Nm3u8DlClientTests|DownloadOrchestratorTests"
~~~

- [ ] **Step 7: Run approved live smoke tests**

After explicit approval, analyze one allowed current 1tv.ru URL and official HLS/DASH test manifests. Confirm selected resolution using FFprobe and decode the finished file. Preserve recovery segments until the user approves cleanup.

- [ ] **Step 8: Commit**

~~~powershell
git add src/VideoGrabber.Core/Downloads src/VideoGrabber.Infrastructure/Sites src/VideoGrabber.Infrastructure/Manifests tests
git commit -m "feat: add 1tv and manifest fallback"
~~~

---

### Task 8: Add the WebView2 stream detector and optional local login

**Files:**
- Create: src/VideoGrabber.Infrastructure/Web/DetectedStream.cs
- Create: src/VideoGrabber.Infrastructure/Web/StreamDetector.cs
- Create: src/VideoGrabber.Infrastructure/Web/WebViewProfileService.cs
- Create: src/VideoGrabber.App/Downloads/StreamDetectorDialog.xaml
- Create: src/VideoGrabber.App/Downloads/StreamDetectorDialog.xaml.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Web/StreamDetectorPolicyTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Web/WebViewProfileServiceTests.cs

**Interfaces:**
- Consumes: UrlPolicy and LogRedactor.
- Produces: IStreamDetector.DetectAsync(Uri, string profilePath, CancellationToken) and WebViewProfileService.ClearAsync.

- [ ] **Step 1: Write failing detector-policy tests**

Accept response types video/*, application/vnd.apple.mpegurl, application/x-mpegURL and application/dash+xml plus .mp4/.m3u8/.mpd paths. Reject data/blob/file schemes, localhost/private destinations, tiny tracking responses, duplicate normalized URLs and URLs whose DNS answer changes to a private address.

- [ ] **Step 2: Implement detector policy separately from WebView2**

The pure policy receives request URI, response content type and optional content length and returns Accept, Ignore or RejectNavigation. Strip fragments and redact signed query values for display, while the live in-memory request object retains the original URL only until the job ends.

- [ ] **Step 3: Implement WebView2 capture**

Create CoreWebView2Environment with userDataFolder=data\webview. Enable DevToolsProtocol Network events only for the active dialog; detach handlers on close. Disable downloads, external protocols, permission prompts except media playback, new-window escapes, host object injection and local file access.

- [ ] **Step 4: Implement explicit login boundary**

The dialog opens with status Встроенный браузер используется только для поиска медиапотока. A separate Войти на сайте action enables the persistent profile for that domain. Clearing data lists the exact profile directory and requires confirmation; it never removes output or job data.

- [ ] **Step 5: Run tests and a local fixture smoke**

Serve tests/fixtures/web/index.html on 127.0.0.1 only inside the test harness and allow loopback solely through an internal test flag inaccessible to production. Verify one HLS URL is detected and all event handlers are released.

- [ ] **Step 6: Commit**

~~~powershell
git add src/VideoGrabber.Infrastructure/Web src/VideoGrabber.App/Downloads tests/VideoGrabber.Infrastructure.Tests/Web tests/fixtures/web
git commit -m "feat: detect media streams with WebView2"
~~~

---

### Task 9: Build the intuitive downloader UI

**Files:**
- Create: src/VideoGrabber.App/Downloads/DownloaderPage.xaml
- Create: src/VideoGrabber.App/Downloads/DownloaderPage.xaml.cs
- Create: src/VideoGrabber.App/Downloads/DownloaderViewModel.cs
- Create: src/VideoGrabber.App/Downloads/DownloadJobViewModel.cs
- Create: src/VideoGrabber.App/Services/AppPaths.cs
- Create: src/VideoGrabber.App/Services/CompositionRoot.cs
- Create: src/VideoGrabber.Infrastructure/Settings/JsonSettingsStore.cs
- Create: src/VideoGrabber.Core/Settings/AppSettings.cs
- Create: src/VideoGrabber.App/Styles/ThemeResources.xaml
- Create: src/VideoGrabber.App/Styles/CardStyles.xaml
- Modify: src/VideoGrabber.App/MainWindow.xaml
- Test: tests/VideoGrabber.App.Tests/Downloads/DownloaderViewModelTests.cs

**Interfaces:**
- Consumes: DownloadOrchestrator, DownloadQueue, MediaValidator, IJobStore.
- Produces: AnalyzeCommand, DownloadCommand, PauseCommand, ResumeCommand, CancelCommand, OpenFileCommand, OpenFolderCommand and atomic typed settings persistence.

- [ ] **Step 1: Write view-model tests**

Test empty URL, busy analysis, successful analysis, no formats, best-format default, download enqueue, duplicate click suppression, cancellation, classified errors, and no shell launch for paths outside the output folder.

~~~csharp
vm.SourceText = "https://example.test/video";
await vm.AnalyzeCommand.ExecuteAsync(null);
Assert.Equal("Fixture title", vm.Title);
Assert.Equal(1080, vm.SelectedFormat!.Height);
Assert.True(vm.DownloadCommand.CanExecute(null));
~~~

- [ ] **Step 2: Implement view models without UI dependencies**

Expose observable state and async commands. Disable Analyze while analyzing and Download until a format/output folder is valid. Translate DownloadErrorCode to fixed Russian messages; technical details remain behind Подробности.

- [ ] **Step 3: Implement DownloaderPage**

Use a responsive Grid with URL field and Analyze button, media card, quality/container controls, options, output folder picker, primary Download button, and ItemsRepeater/ListView queue cards. At widths below 760 px use one column. Provide empty, loading, success, paused, failed and completed visual states.

ThemeResources.xaml defines an eight-pixel spacing scale, 12-pixel card radius, subtle neutral surfaces, one system-accent primary action, 14/20/28-pixel type ramp and state colors that pass high-contrast fallback. CardStyles.xaml applies consistent padding, thumbnail ratio and progress/status placement. Use Fluent SymbolIcon glyphs instead of unlicensed image assets.

- [ ] **Step 4: Add accessibility and theme verification**

Assign AutomationProperties.Name, keyboard accelerators, visible focus, high-contrast resources and minimum 44×44 effective target size. Verify at 100%, 125%, 150% and 200% scaling, light/dark/high-contrast themes, and minimum window size.

- [ ] **Step 5: Wire CompositionRoot and safe shell actions**

Construct dependencies once. Open file/folder only after Path.GetFullPath containment checks and with ProcessStartInfo.UseShellExecute=true solely for Explorer/open actions; never pass web content as an executable or argument.

- [ ] **Step 6: Implement first-run and settings persistence**

AppSettings contains OutputDirectory, TempDirectory, Theme, HistoryEnabled, CheckUpdatesDaily, DefaultContainer and default metadata options. JsonSettingsStore writes data\settings.json atomically through settings.json.tmp, retains settings.json.previous, and falls back to defaults with a visible warning if both are invalid. First launch requires output/temp folder selection, validates write access and free space, and stores no secret. When HistoryEnabled is false, completed jobs are removed from jobs.db after their current UI session while failed/recovery jobs remain until resolved.

- [ ] **Step 7: Run tests and manual UI smoke**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug
dotnet run --project .\src\VideoGrabber.App\VideoGrabber.App.csproj
~~~

Expected: app opens without console, Russian downloader flow is usable by keyboard, and fixture-backed fake services exercise every state.

- [ ] **Step 8: Commit**

~~~powershell
git add src/VideoGrabber.App tests/VideoGrabber.App.Tests
git commit -m "feat: add intuitive downloader interface"
~~~

---

### Task 10: Add verified component updates with rollback

**Files:**
- Create: src/VideoGrabber.Infrastructure/Updates/ComponentRelease.cs
- Create: src/VideoGrabber.Infrastructure/Updates/ComponentUpdater.cs
- Create: src/VideoGrabber.App/Settings/SettingsPage.xaml
- Create: src/VideoGrabber.App/Settings/SettingsPage.xaml.cs
- Create: src/VideoGrabber.App/Settings/SettingsViewModel.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Updates/ComponentUpdaterTests.cs
- Test: tests/VideoGrabber.App.Tests/Settings/SettingsViewModelTests.cs

**Interfaces:**
- Consumes: IProcessRunner, IComponentCatalog, DownloadQueue active-state query.
- Produces: CheckAsync, StageAsync, ApplyAsync and RollbackAsync.

- [ ] **Step 1: Write updater tests**

Test HTTPS/host allowlist, expected asset name, checksum mismatch, staging outside live runtime, active-job rejection, successful --version self-test, failed self-test rollback and retained backup.

- [ ] **Step 2: Implement allowlisted release sources**

Only official repositories listed in components.lock.json are accepted. Follow redirects only to github.com, api.github.com, objects.githubusercontent.com and github-releases.githubusercontent.com. Stream to runtime\staging, enforce a size limit per component, compute SHA-256 during download, and never execute before verification.

- [ ] **Step 3: Implement atomic apply and rollback**

Reject updates while jobs are active. Rename current binary to .previous, move staged binary into place, run --version, update lock metadata, and retain one backup. On any failure restore .previous and surface the exact safe stage that failed.

- [ ] **Step 4: Implement Settings UI**

Show application and component versions, health, update availability, source repository, and buttons Проверить, Обновить, Откатить. Automatic checks run at most once per 24 hours and never auto-apply.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug --filter "ComponentUpdaterTests|SettingsViewModelTests"
git add src/VideoGrabber.Infrastructure/Updates src/VideoGrabber.App/Settings tests
git commit -m "feat: add verified component updates"
~~~

---

### Task 11: Produce and verify the downloader portable release

**Files:**
- Create: scripts/Build-Portable.ps1
- Create: tests/smoke/Invoke-DownloaderSmoke.ps1
- Create: docs/USER-GUIDE.md
- Create: docs/THIRD-PARTY-NOTICES.md
- Modify: runtime/components.lock.json

**Interfaces:**
- Consumes: complete downloader application.
- Produces: artifacts/VideoGrabber-win-x64/VideoGrabber.exe and a verification report.

- [ ] **Step 1: Write the publish script and Pester-style assertions**

Build-Portable.ps1 restores locked packages, runs tests, publishes self-contained win-x64, copies verified runtime components and notices, then emits SHA256SUMS.txt. It fails if VideoGrabber.exe or any required component is absent, if a hash differs, or if a debug symbol is included unintentionally.

- [ ] **Step 2: Add the smoke script**

Invoke-DownloaderSmoke.ps1 starts VideoGrabber.exe, verifies the process has no console window, tests local direct MP4/HLS/DASH fixtures, closes gracefully, and runs FFprobe plus decode checks on outputs. Network cases require -IncludeNetwork and print the exact domains before asking for approval.

- [ ] **Step 3: Run complete offline verification**

~~~powershell
dotnet restore --locked-mode
dotnet build .\VideoGrabber.slnx -c Release --no-restore
dotnet test .\VideoGrabber.slnx -c Release --no-build
pwsh -NoProfile -File .\scripts\Build-Portable.ps1
pwsh -NoProfile -File .\tests\smoke\Invoke-DownloaderSmoke.ps1 -ArtifactPath .\artifacts\VideoGrabber-win-x64
~~~

Expected: all tests pass; VideoGrabber.exe launches on build 19044; local MP4/HLS/DASH downloads validate; SHA256SUMS.txt matches.

- [ ] **Step 4: Run approved network acceptance**

With explicit approval, test one public/authorized URL each for YouTube, Rutube and 1tv.ru. Record source domain, selected resolution, duration, output size, analyzer/executor used, FFprobe result, decode-check exits and any limitation. Do not retain browser cookies unless the user explicitly used login mode.

- [ ] **Step 5: Review the artifact**

Confirm no .env, browser profile, cookie database, personal path, source fixture with rights ambiguity, logs, job history or unfinished media is inside artifacts. Verify third-party license notices and component hashes.

- [ ] **Step 6: Commit**

~~~powershell
git add scripts tests/smoke docs runtime
git commit -m "build: verify portable downloader release"
~~~

Downloader milestone is complete only after all offline checks and the explicitly approved network acceptance pass.
