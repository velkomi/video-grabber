# VideoGrabber Desktop Stabilization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Исправить 25 подтверждённых desktop-дефектов, исследовать VG-AUD-024 и получить проверяемые результаты четырёх desktop release gates.

**Architecture:** Сохранить существующие Core → Infrastructure → WinUI App зависимости и partial MainWindow. Вынести только небольшие проверяемые политики владения, качества и жизненного цикла; подключить их к реальным обработчикам интерфейса. Каждый task — самостоятельный review unit с RED, минимальным исправлением, GREEN, связанной интеграцией и отдельным локальным коммитом.

**Tech Stack:** C# 14, .NET SDK 10.0.400 (latestPatch), net10.0, WinUI 3 / Windows App SDK 2.4.0, WebView2 1.0.4191.47, xUnit 2.9.3, PowerShell, yt-dlp, FFmpeg/ffprobe, Deno, whisper.cpp.

**Spec:** Authoritative defect specification — [03-findings.md](C:/Users/Oleg/Documents/Codex/2026-09-15/videograbber-codex-astra-gpt6-full-audit/outputs/artifacts/full-audit/20260915-01a0a373/03-findings.md), execution order and acceptance — [09-fix-plan.md](C:/Users/Oleg/Documents/Codex/2026-09-15/videograbber-codex-astra-gpt6-full-audit/outputs/artifacts/full-audit/20260915-01a0a373/09-fix-plan.md). Read both completely before execution, together with [05-test-results.md](C:/Users/Oleg/Documents/Codex/2026-09-15/videograbber-codex-astra-gpt6-full-audit/outputs/artifacts/full-audit/20260915-01a0a373/05-test-results.md) and [10-final-report-ru.md](C:/Users/Oleg/Documents/Codex/2026-09-15/videograbber-codex-astra-gpt6-full-audit/outputs/artifacts/full-audit/20260915-01a0a373/10-final-report-ru.md). Audit snapshot: `36b2831aa3615accb50af081f31257a964902c58`. The audit remains immutable evidence; later results go into `fix-execution`.

## Global Constraints

- Scope: desktop AUDIT_FIX only; accounts, social auth, Telegram bot/Mini App, payments, licensing, gifts, PostgreSQL, server workers and deployment are outside this plan.
- Spec: «Каждый fix — исходный RED → минимальный patch → targetedGREEN → связанная media integration.»
- Spec: «Не удалять/перемещать pre-existing/чужие файлы».
- Spec: «Не подменять несуществующий quality глобальным master.»
- Spec: «Loopback разрешать только явно в isolated test setup.»
- Spec: «до доказательства не выпускать speculative fix.» This applies to VG-AUD-024; VG-AUD-021 also requires a working fixture before production edits.
- Spec: «Закрыть GUI/privateGetCourse/cleanWindows/tool provenance gates и один полный final regression без скрытых skips.»
- Spec: «push/merge/release/production требуют отдельного разрешения.» Local publish for inspection is allowed; no release publication or installed-tool replacement.
- Work only in `D:\CODEX\Worktrees\videograbber-full-audit-20260915-01a0a373\repo`, branch `fix/full-audit-20260915`. Preserve other workers' files and staged changes; never use `git add .`.
- No deletion of user files, media, old releases or settings. Test cleanup is restricted to verified, unique test-owned roots; retain evidence and recoverable partial media.
- No real secrets in code, logs, reports, Git, screenshots or command arguments. Real GetCourse login and secret use require the user's existing explicit authorization for that fixture; otherwise record BLOCKED.
- Keep `TreatWarningsAsErrors=true`, central versions and lock files; restore with `--locked-mode`. Do not force solution-wide `-r win-x64` (audit NU1004); App already owns its RID.
- No new package is needed for this plan. Native Windows job APIs use explicit P/Invoke; do not install tools or change global environment settings during tests.
- All numerical limits introduced below are implementation decisions of this plan, not claims about pre-existing product policy. Record any necessary deviation before implementation and review it against the authoritative spec.
- A code-only/model test cannot close a WinUI gate. A synthetic six-part fixture cannot close the private GetCourse gate. A package launching on this SDK machine cannot close clean-Windows verification.

---

## Repository map and execution contract

Existing boundaries: `src/VideoGrabber.Core/Downloads/DownloadRequest.cs` carries the download request; `Infrastructure/Downloads/YtDlpDownloader.cs` constructs tools, verifies and promotes output; `Infrastructure/Browser` contains binding, manifests, queues and preflight; `App/MainWindow.*.cs` owns WinUI state; `Infrastructure/Networking` owns optional routing; `Infrastructure/Processes/ProcessRunner.cs` owns subprocesses; `scripts` contains diagnostics and component installation.

Use the five existing audit files in `tests/VideoGrabber.Infrastructure.Tests`: `AuditBrowserRegressionTests.cs`, `AuditMediaRegressionTests.cs`, `AuditNetworkRegressionTests.cs`, `AuditSixPartsIntegrationTests.cs`, `AuditLogIsolation.cs`. They were copied from audit evidence and may initially be untracked. The coordinator owns their initial import; do not silently stage the import with a production task. `AuditLogIsolation` requires `VIDEOGRABBER_EVIDENCE` before test assembly initialization. Preserve its isolation until a tested public diagnostics seam replaces it.

New focused types/files are defined in their owning task; all tests remain in the existing Core or Infrastructure test projects. Test code snippets below are exact focused assertions to add to the named class; reuse that class's existing fixtures when named. New policy methods are specified with signatures so an implementer does not invent a neighboring task's API.

For a new API, first add only its declared types/signatures and a compiling body that throws `NotSupportedException("Behavior not implemented")`; this is test scaffolding in that same task, not its GREEN implementation. Compile the fixture, then record failing desired-behavior assertions. Missing types, missing tools and setup timeout never count as behavioral RED. Existing reproduced audit tests run against unchanged production first. The per-task snippets show core assertions; implement the explicitly enumerated scenario matrix in the named test file before closing the task.

### Commands used in every task

Run this once in the executing PowerShell process; paths are from the audit harness and must pass `Test-Path` before use. They are read-only inputs. No global environment mutation:

```powershell
Set-Location -LiteralPath 'D:\CODEX\Worktrees\videograbber-full-audit-20260915-01a0a373\repo'
$vgDotnet = 'D:\CODEX\Portable\dotnet-sdk-10\dotnet.exe'
$vgAudit = 'C:\Users\Oleg\Documents\Codex\2026-09-15\videograbber-codex-astra-gpt6-full-audit\outputs\artifacts\full-audit\20260915-01a0a373'
$vgRun = Join-Path $vgAudit ('fix-execution\runs\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $vgRun | Out-Null
$env:VIDEOGRABBER_EVIDENCE = Join-Path $vgRun 'media-fixtures'
$env:VIDEOGRABBER_INTEGRATION_TOOLS = 'C:\Users\Oleg\AppData\Local\VideoGrabber\tools'
$env:VIDEOGRABBER_WHISPER_EXE = 'D:\CODEX\GPT\Tools\whisper-videograbber-b5130\Release\whisper-cli.exe'
$env:VIDEOGRABBER_WHISPER_MODEL = 'D:\CODEX\GPT\Tools\whisper-videograbber-b5130\ggml-base.bin'
$env:VIDEOGRABBER_WHISPER_SAMPLE = 'D:\CODEX\Worktrees\video-grabber-downloader\artifacts\upgrade-20260912\jfk.wav'
$env:TEMP = Join-Path $vgRun 'temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:VIDEOGRABBER_EVIDENCE,$env:TEMP | Out-Null
foreach ($vgPath in @($vgDotnet)) {
    if (-not (Test-Path -LiteralPath $vgPath -PathType Leaf)) { throw 'Required local fixture/tool unavailable; record BLOCKED.' }
}
git status --short
git branch --show-current
& $vgDotnet restore VideoGrabber.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
```

For each task's explicit filter below, run both phases into separate files using this helper. `Run-VgTests` returns the native code; RED requires a behavioral failure, not compile/setup error. GREEN requires exit 0. Do not rerun the audit's fixed-path `commands/run-gate.ps1`, which would overwrite audit evidence.

Before a media/Whisper task, validate its exact executable/model/sample paths and record versions/hashes. Missing media dependencies block that integration gate, while independent unit/plan work continues. No test with an unavailable dependency may be reported as passing; final regression requires every listed media dependency and zero skips.

```powershell
function Run-VgTests([string]$Label,[string]$Filter) {
    $vgArgs = @('test','tests/VideoGrabber.Infrastructure.Tests/VideoGrabber.Infrastructure.Tests.csproj','-c','Release','--no-restore','--filter',$Filter,'--logger',"trx;LogFileName=$Label.trx",'--results-directory',$vgRun)
    $vgStarted = [DateTime]::UtcNow
    & $vgDotnet @vgArgs *> (Join-Path $vgRun "$Label.log")
    $vgExit = $LASTEXITCODE
    [ordered]@{ label=$Label; arguments=$vgArgs; startedUtc=$vgStarted; finishedUtc=[DateTime]::UtcNow; exitCode=$vgExit } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $vgRun "$Label.json") -Encoding utf8
    return $vgExit
}
```

After every GREEN: `& $vgDotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release --no-restore`, `git diff --check`, review exact staged diff and commit only that task's file list. Build must have 0 errors/warnings. Never change an assertion merely to make the current defect pass. Existing tests that enforce wrong behavior may be corrected with an explicit old/new explanation in the task evidence.

## Coverage and ordering

|Task|Defects / gate|Depends on|
|---|---|---|
|1|VG-AUD-017, VG-AUD-018 ownership|baseline/import|
|2|VG-AUD-019, VG-AUD-023 output contract|1|
|3|VG-AUD-016 cookie redirects|baseline|
|4|VG-AUD-001 binding|baseline|
|5|VG-AUD-002, VG-AUD-010 selection and merge|4|
|6|VG-AUD-014 fail-closed selected leaves|baseline|
|7|VG-AUD-003, VG-AUD-015 quality data flow|2,6|
|8|VG-AUD-008, VG-AUD-009 duration semantics|baseline|
|9|VG-AUD-007, VG-AUD-012 page lifetime|3,5,8|
|10|VG-AUD-004, VG-AUD-005, VG-AUD-006 UI operation lifetime|7,9|
|11|VG-AUD-011 queue navigation/session|9,10|
|12|VG-AUD-021 process ownership|baseline; valid fixture first|
|13|VG-AUD-013 installer lifetime|10,12|
|14|VG-AUD-020 mandatory egress|3,7,12|
|15|VG-AUD-022 SRT validity|baseline|
|16|VG-AUD-025 diagnostics coverage|baseline|
|17|VG-AUD-026 component transaction|13|
|18|VG-AUD-024 investigation and conditional fix|2,12|
|19|GUI, private GetCourse, clean Windows, provenance, final regression|all completed fixes|

Complete all P1 tasks 1–6 before P2-only work, except a strictly required dependency. Shared-file tasks execute serially. Each task has a fresh review gate; grouping is limited to one shared state/contract.

### Task 1: Job-owned output and cancellation preservation

**Files:** Modify `src/VideoGrabber.Infrastructure/Downloads/YtDlpDownloader.cs` (normal/split download, recovery, cleanup, promotion); create `src/VideoGrabber.Infrastructure/Downloads/DownloadWorkspace.cs`; extend `tests/VideoGrabber.Infrastructure.Tests/AuditMediaRegressionTests.cs`, `DownloadJobIsolationTests.cs`, `SplitHlsCleanupTests.cs`.

**Interfaces:** New `DownloadWorkspace.Create(string outputDirectory, string? requestedParent)`, properties `Root: string`, `OwnedFiles: IReadOnlySet<string>`, `RegisterCreatedFile(string path): void`, `Owns(string path): bool`; no automatic destructive Dispose. Always create a unique child `.vg-job-{Guid:N}` inside the canonical output directory; `JobDirectory` is accepted only as a validated parent inside that output directory, never as ownership of an existing directory. Reject reparse components and path escape before writes, promotion and cleanup. A returned stdout pathname does not itself confer ownership. Discover tool-created files only under the initially empty owned root, register them after process exit, and reject reparse entries; restrict the tool template to that root.

- [ ] **RED:** Run `Run-VgTests 't01-red' 'FullyQualifiedName~Failed_download_must_preserve_concurrent_unrelated_media|FullyQualifiedName~Failed_download_must_not_promote_preexisting_exact_basename|FullyQualifiedName~Cancel_must_preserve_preexisting_similarly_named_temp|FullyQualifiedName~Cancel_after_track_100_must_not_promote_old_prefix_video'`. All four existing assertions must fail for the documented ownership reasons on the audit snapshot; save their outcomes.
- [ ] Add these assertions to `DownloadJobIsolationTests` using the new workspace type, then run its class as RED:

```csharp
[Fact]
public void Workspace_rejects_sibling_prefix_and_preexisting_files()
{
    var root = Path.Combine(Path.GetTempPath(), "vg-owned-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var sentinel = Path.Combine(root, "Lesson.mp4");
    File.WriteAllBytes(sentinel, [1, 2, 3]);
    var job = DownloadWorkspace.Create(root, null);
    Assert.False(job.Owns(sentinel));
    Assert.False(job.Owns(job.Root + "-sibling\\file.mp4"));
    Assert.Throws<InvalidOperationException>(() => job.RegisterCreatedFile(sentinel));
    Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(sentinel));
}
```

- [ ] **GREEN implementation:** Replace broad `SnapshotOutputFiles`/`RecoverOutputPath` and prefix cleanup with the workspace invariant. Use the same owner in split mode. Do not promote after cancel based on track percentage; retain the only usable `.temp` and report its job directory. Cleanup only registered intermediates after final output verification; failure/cancel retains recoverable material. Safe containment uses `Path.GetRelativePath`, separator boundaries and filesystem reparse checks, not string prefix alone:

```csharp
var relative = Path.GetRelativePath(Root, Path.GetFullPath(path));
if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new InvalidOperationException("Output is outside the owned job directory.");
```

- [ ] **GREEN verification:** Rerun the four original regressions and `DownloadJobIsolationTests|SplitHlsCleanupTests|DownloadOutputPathTests`. Add and run `BogusFilepathOutsideJobMustFail`, `CancelWithOnlyRecoverableTempMustPreserveIt`, `JunctionMustNotEscapeJob`, `ReadOnlyForeignFileMustBeUnchanged`, `HeldHandleMustRetainOwnedPartial`, `SimulatedDiskFullMustNotRemoveOriginal`. Fixture file operations throw deterministic `IOException` for disk-full; an actual full-volume exercise is separate disposable-volume acceptance, never fill the user's disk. Verify SHA-256 and original paths for every sentinel, normal and split promotion collision, and explicit job parent. Capture reparse rejection; if junction creation is unavailable, that matrix row stays BLOCKED.
- [ ] **Review/commit:** `git add` only the four named existing files plus `DownloadWorkspace.cs`; `git diff --cached --check`; commit `fix: isolate download file ownership`. Do not claim completeness recovery solved here; Task 2 owns it.

### Task 2: Completion and requested-stream contracts

**Files:** Modify `src/VideoGrabber.Core/Downloads/DownloadRequest.cs`, `src/VideoGrabber.Infrastructure/Downloads/YtDlpDownloader.cs`, `src/VideoGrabber.App/MainWindow.Download.cs`, `src/VideoGrabber.App/MainWindow.BatchDownload.cs`; create `src/VideoGrabber.Infrastructure/Downloads/DownloadOutputContract.cs`; extend `AuditMediaRegressionTests.cs`, `Preview12FinalizationTests.cs`, `AuthenticatedHlsDownloadIntegrationTests.cs` in the Infrastructure test directory.

**Interfaces:** Append optional `double? ExpectedDurationSeconds = null`, `bool? ExpectedAudio = null`, `bool ResolvedHlsLeaf = false` to `DownloadRequest` (last flag used by Task 7). New `DownloadOutputContract.IsSatisfied(DownloadRequest request, MediaProbeResult media): bool`. Duration tolerance for known VOD is `max(1 second, expected * 0.02)`; unknown duration remains unknown. `ExpectedAudio=null` means unknown, not absent. Ordinary video always requires `HasVideo`; audio-only MP3 requires audio, no video and mp3 codec. No failed or cancelled tool invocation returns Success in this stabilization pass; preserve partial outputs for explicit later retry. This deliberately removes unsafe automatic recovery rather than inventing unreliable error-string allowlists.

- [ ] **RED:** Run existing `Failed_fragment_download_must_not_succeed_with_short_playable_partial` and `Video_request_must_reject_audio_only_media`. Add the focused policy test below and `CancelAfterFirstTrack100MustNotSucceed` using `AuditMediaRegressionTests.StubRunner`, with its 100% line followed by cancellation and a real owned output. The test must assert cancellation, never success:

```csharp
[Fact]
public void Short_readable_video_does_not_satisfy_expected_duration()
{
    var request = new DownloadRequest(new Uri("https://cdn.example/video"), "output", "best", ExpectedDurationSeconds: 60, ExpectedAudio: true);
    Assert.False(DownloadOutputContract.IsSatisfied(request, new(true, true, true, "aac", DurationSeconds: 2)));
    Assert.True(DownloadOutputContract.IsSatisfied(request, new(true, true, true, "aac", DurationSeconds: 59.5)));
    Assert.False(DownloadOutputContract.IsSatisfied(request, new(true, true, false, "aac", DurationSeconds: 60)));
}
```

- [ ] **GREEN implementation:** Check native result before promotion; use output contract in normal and split paths. Carry known finite VOD duration and track expectation from selected candidate into the request; for unknown metadata require exit=0 plus requested streams, never infer completeness from a 100% track message. Use yt-dlp abort-on-unavailable-fragment policy for HLS so skipped segments cannot yield a normal success. Record one sanitized failed/cancelled terminal event, no `download.recovered succeeded` after failed fragment execution.

```csharp
if (!result.IsSuccess) return DownloadFailureFormatter.Create(request.Source, result.StandardError);
if (!DownloadOutputContract.IsSatisfied(request, media))
    return new(false, "Файл не соответствует ожидаемой длительности или дорожкам.");
```

- [ ] **GREEN/integration (R1):** `Run-VgTests 't02-green' 'FullyQualifiedName~Failed_download_must_preserve_concurrent_unrelated_media|FullyQualifiedName~Failed_download_must_not_promote_preexisting_exact_basename|FullyQualifiedName~Cancel_must_preserve_preexisting_similarly_named_temp|FullyQualifiedName~Cancel_after_track_100_must_not_promote_old_prefix_video|FullyQualifiedName~Failed_fragment_download_must_not_succeed_with_short_playable_partial|FullyQualifiedName~Video_request_must_reject_audio_only_media|FullyQualifiedName~Short_readable_video_does_not_satisfy_expected_duration|FullyQualifiedName~CancelAfterFirstTrack100MustNotSucceed'`. Run related completed-output integration separately: `Run-VgTests 't02-media' 'FullyQualifiedName~Preview12FinalizationTests|FullyQualifiedName~DownloadFailureRegressionTests|FullyQualifiedName~AuthenticatedHlsDownloadIntegrationTests'`. SRT cases remain RED until Task 15; the whole AuditMediaRegressionTests class runs only in final regression. Adjust old recovery expectations explicitly to failure plus retained partial. Extend the authenticated HLS fixture with unavailable final segment; real yt-dlp must fail and preserve the owned incomplete file. Complete control must have requested streams, expected duration, ffprobe height and `ffmpeg -v error -nostdin -i output -f null -` exit 0, including tail decode. Cost-if-wrong: a cross-area regression can remain undetected until Task 15/final; the complete final suite remains mandatory.
- [ ] **Review/commit:** Stage only the eight listed files; commit `fix: require complete requested media output`.

### Task 3: Cookie scope across redirects

**Files:** Modify `src/VideoGrabber.Infrastructure/Browser/HlsPreflightClient.cs`, `src/VideoGrabber.App/MainWindow.HlsPreflight.cs`; extend `tests/VideoGrabber.Infrastructure.Tests/AuditNetworkRegressionTests.cs`, `DirectManifestHardeningTests.cs`.

**Interfaces:** Append `Func<Uri,CancellationToken,Task<string?>>? CookieProvider = null` to `HlsPreflightFetchOptions`. Existing raw `CookieHeader` is valid only for the initial scheme/host/port and exact initial path; the UI switches to a provider invoking WebView2's cookie manager for each validated URI. The provider executes through the UI dispatcher and checks Task 9's page lease when available. No browser cookie jar is copied into a global HttpClient.

- [ ] **RED:** `Run-VgTests 't03-red' 'FullyQualifiedName~Cross_host_hls_redirect_must_not_forward_source_cookie'`; require observed synthetic source cookie and missing target isolation as original failure.
- [ ] Extend that real HTTP fixture with same-origin/same-path control, changed path, parent/subdomain, Secure cookie over HTTP, 301/302/303/307/308, relative Location, loop and hop limit. Retain exact test assertions:

```csharp
Assert.Contains("Cookie: audit_cookie=synthetic_only", observed.Source, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("audit_cookie=synthetic_only", observed.Target, StringComparison.Ordinal);
```

- [ ] **GREEN implementation:** Set `AllowAutoRedirect=false`; at most 5 redirects, validate each absolute resolved HTTP(S) URI and reject userinfo/HTTPS downgrade. Build a fresh request each hop. Recompute scoped cookie using `CookieProvider`; without it never forward raw cookie to changed origin/path. Parse relative manifests against the final response URI, not original source. Keep the existing 4,000,000 body limit and a single 25-second total deadline.

```csharp
var cookie = options.CookieProvider is not null
    ? await options.CookieProvider(current, cancellationToken).ConfigureAwait(false)
    : current == source ? options.CookieHeader : null;
// Build a new HttpRequestMessage for current; never reuse headers from the previous hop.
```

- [ ] **GREEN:** Run `AuditNetworkRegressionTests` filtered to cookie tests plus `DirectManifestHardeningTests|AuthenticatedHlsDownloadIntegrationTests`; process fixture is handled in Task 12. Assert no real cookie values in reports; redirect loop is bounded; both audio/video leaf preflight uses the new provider.
- [ ] **Review/commit:** Stage four listed files; commit `fix: scope hls cookies per redirect target`.

### Task 4: Evidence-based part binding

**Files:** Modify `src/VideoGrabber.Infrastructure/Browser/DevToolsFrameTreeParser.cs`, `MediaCandidatePresentation.cs` in that directory; extend Infrastructure tests `AuditBrowserRegressionTests.cs`, `BrowserUniqueBindingTests.cs`, `BrowserPlayerBindingTests.cs`, `AuditSixPartsIntegrationTests.cs`.

**Interfaces:** Keep `BrowserFrameBindingResolver.Bind/BindAll` signatures. `PageOrdinal=null` and `PageSectionTitle=null` mean unknown. Only unique full DOM URL/referer matches, or FrameId → frame URL → unique full DOM URL, confer a part. Frame tree position, arrival order and duration do not confer a part. Conflicting duplicate evidence produces unknown, not the first available slot. `ordinal` display parameter remains an item counter; it must not appear as a proven PART in a suggested filename.

- [ ] **RED:** `Run-VgTests 't04-red' 'FullyQualifiedName~Audit_A001|FullyQualifiedName~BrowserUniqueBindingTests'`; add absent/conflicting FrameId and permuted frame tree tests based on `SixSlots()`.

```csharp
var unknown = BrowserFrameBindingResolver.BindAll(
    Enumerable.Range(1, 6).Reverse().Select(i => Part(i, false)).ToArray(), [], SixSlots());
Assert.All(unknown, c => { Assert.Null(c.PageOrdinal); Assert.Null(c.PageSectionTitle); });
```

- [ ] **GREEN implementation:** Remove free-slot assignment and positional FrameOrdinal inference from both Bind APIs. Clear obsolete ordinals before rebinding. Resolve the matched frame to a DOM slot by complete URL; only assign when both source and target mapping are unambiguous. Preserve technical-child suppression. Unknown UI label explicitly says `Привязка к части не подтверждена`; a neutral filename uses source identity, not a fabricated part number.
- [ ] **GREEN/integration:** Run `Audit_A001|Audit_Control_Exact|BrowserUniqueBindingTests|BrowserPlayerBindingTests|MediaCandidatePresentationTests`. Change only documented wrong fallback assertions. In `AuditSixPartsIntegrationTests`, replace the last `Assert.Contains` expecting wrong ordinal with `Assert.All(ambiguous, c => Assert.Null(c.PageOrdinal))`. Known binding still downloads each real PART 1–6, source hash must match requested part, two equal-duration sources remain distinguishable, full decode and audio/height match. Use the audit's 12 synthetic source fixtures copied to the new evidence run, never private media.
- [ ] **Review/commit:** Stage six listed files; commit `fix: require evidence for browser part bindings`.

### Task 5: Stable selection and metadata merge

**Files:** Modify `src/VideoGrabber.App/MainWindow.DevTools.cs`, `MainWindow.Browser.cs`, `MainWindow.BatchDownload.cs`; create `src/VideoGrabber.Infrastructure/Browser/MediaCandidateMerge.cs`, `MediaCandidateSelectionReducer.cs`; extend Infrastructure tests `MediaCandidatePresentationTests.cs`, `BrowserWiringRegressionTests.cs`; create `BrowserSelectionRegressionTests.cs` there.

**Interfaces:** `MediaCandidateMerge.Merge(MediaCandidate previous, MediaCandidate incoming): MediaCandidate` requires identical Source within a page generation. Preserve non-null FrameId and known duration from previous when incoming lacks them. Conflicting non-null frame evidence clears PageOrdinal/title and FrameId; conflicting finite durations become unknown and trigger revalidation. A manifest refresh replaces track lists only when present; it cannot silently erase confirmed duration with null. UI selection identity is `Source.AbsoluteUri`, quality remains `_mediaQualitySelections[Source]`.

**R7 production seam:** `MediaCandidateSelectionReducer.Reduce(IReadOnlyList<MediaCandidate> orderedCandidates, Uri? previousSelectedSource, IReadOnlyDictionary<string,string> qualityBySource): MediaCandidateSelection`; result is `MediaCandidateSelection(IReadOnlyList<MediaCandidate> Candidates, Uri? SelectedSource, string SelectedQuality)`. It copies the ordered candidates, retains exact Source if present, otherwise selects first or null; quality is looked up by resulting Source, default best only when absent. App calls this reducer for every rebind, then applies returned selection once under suppression. Infrastructure tests invoke this production reducer, never a duplicated test-only reducer.

- [ ] **RED:** First extract current selection behavior into the named production reducer with App delegation (preserve current first-item selection), then add a behavioral RED to `BrowserSelectionRegressionTests`: choose Source part4+720p, reorder candidates and call Reduce; assert SelectedSource remains part4 and SelectedQuality remains720p. Extraction must pass pre-existing controls before the defect assertion; only then fix the reducer. A separate GUI reproduction record still checks PART4/reversed arrivals/late metadata. For merge use existing `MediaCandidate`/`HlsManifestInfo` constructors:

```csharp
[Fact]
public void Sparse_discovery_preserves_confirmed_frame()
{
    var source = new Uri("https://cdn.example/p4/master.m3u8");
    var referer = new Uri("https://school.example/lesson");
    var previous = new MediaCandidate(source, referer, "HLS", FrameId: "frame4", PageOrdinal: 4, PageSectionTitle: "PART 4");
    var merged = MediaCandidateMerge.Merge(previous, new(source, referer, "HLS"));
    Assert.Equal("frame4", merged.FrameId);
    Assert.Equal(4, merged.PageOrdinal);
}
```

- [ ] **GREEN implementation:** Capture selected Source before Items.Clear; set `_updatingMediaQuality=true` across the entire rebuild and restore flag in finally; the SelectionChanged handler must return while this flag is set. Restore selected ComboBoxItem by Source, only choose first if old source was genuinely removed. Then sync quality once. Apply `MediaCandidateMerge` before rendering/updating queue, with a caller generation guard (Task 9 strengthens all awaits).

```csharp
var selectedSource = (_mediaCandidatesBox.SelectedItem as ComboBoxItem)?.Tag is MediaCandidate selected ? selected.Source : null;
var selection = MediaCandidateSelectionReducer.Reduce(orderedCandidates, selectedSource, _mediaQualitySelections);
// Rebuild returned Candidates under the selection/quality suppression flag.
_mediaCandidatesBox.SelectedItem = _mediaCandidatesBox.Items.OfType<ComboBoxItem>()
    .FirstOrDefault(item => item.Tag is MediaCandidate value && value.Source == selection.SelectedSource);
```

- [ ] **GREEN:** `Run-VgTests 't05-green' 'FullyQualifiedName~BrowserSelectionRegressionTests|FullyQualifiedName~MediaCandidatePresentationTests|FullyQualifiedName~BrowserWiringRegressionTests'`. Model tests cover sparse and conflicting FrameId/duration; GUI gate repeats part4+720p, reordered list and duplicate WebResource delivery, then inspects the actual source passed to downloader. Source-text wiring checks alone are insufficient.
- [ ] **Review/commit:** Stage eight listed files; commit `fix: retain browser selection and confirmed metadata`.

### Task 6: Fail-closed HLS plan resolution

**Files:** Modify `src/VideoGrabber.Infrastructure/Browser/HlsDownloadPolicy.cs`, `MediaDownloadPlanResolver.cs`, `src/VideoGrabber.App/MainWindow.HlsPreflight.cs`, `MainWindow.BatchDownload.cs`; extend Infrastructure tests `AuditBrowserRegressionTests.cs`, `BrowserDownloadPolicyTests.cs`, `DirectManifestHardeningTests.cs`.

**Interfaces:** Append `bool IsResolved = true`, `string? Error = null`, `bool ResolvedHlsLeaf = false` to `MediaDownloadPlan`. Unavailable selection returns `IsResolved=false` with Russian explanation; Source may remain for identity but is never downloadable. A resolved split plan contains both selected leaves; a resolved single leaf has `ResolvedHlsLeaf=true`. Existing Resolve signature remains unchanged.

- [ ] **RED:** Run `Run-VgTests 't06-red' 'FullyQualifiedName~Audit_A014'`. Retain core safety assertion; update the old source/null descriptive assertions only to add the new explicit failure contract.

```csharp
var plan = MediaDownloadPlanResolver.Resolve(candidate, "360p", false);
Assert.False(plan.IsResolved);
Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>()));
```

- [ ] **GREEN implementation:** Reject !IsResolved before any cookie export or downloader invocation. `AreSelectedTracksVerified` requires `selected.Count > 0 && selected.All(...)` for masters. A fetched leaf must be parsed, non-master and clear before insertion into verified cache; an unexpected second master is unresolved, not proof of clear media. Check both chosen split leaves, malformed/unknown-key manifests fail closed.

```csharp
if (!plan.IsResolved) return false;
return selected.Count > 0 && selected.All(uri => verifiedClear.Contains(uri.AbsoluteUri));
```

- [ ] **GREEN:** Run `Audit_A014|BrowserDownloadPolicyTests|DirectManifestHardeningTests|MasterHlsDownloadTests`. Controls: clear video+audio accepted only after both leaves; one encrypted/unknown leaf refuses; empty refreshed quality has zero downloader calls; ordinary direct clear media still works.
- [ ] **Review/commit:** Stage seven files; commit `fix: reject unresolved hls selections`.

### Task 7: One quality choice from UI through downloader

**Files:** Create `src/VideoGrabber.Core/Downloads/DownloadQuality.cs`, `UserDownloadIntent.cs`; modify `src/VideoGrabber.Infrastructure/Browser/HlsTrackSelector.cs`, `BrowserDownloadQueue.cs`, `MediaDownloadPlanResolver.cs`, `src/VideoGrabber.Infrastructure/Downloads/YtDlpDownloader.cs`, `src/VideoGrabber.App/MainWindow.BatchDownload.cs`, `MainWindow.Download.cs`; extend Infrastructure tests `AuditBrowserRegressionTests.cs`, `HlsTrackSelectorTests.cs`, `YtDlpDownloaderTests.cs`, `AuditSixPartsIntegrationTests.cs`.

**Interfaces:** `public readonly record struct DownloadQuality(int? MaximumHeight)` with `TryParse(string? value, out DownloadQuality quality): bool`, `ToTag(): string`; best=null, `4K`→2160, positive ASCII integer + `p` accepted through Int32; invalid tags reject instead of silently selecting best. Keep `DownloadRequest.Quality` string for compatibility and normalize once. Task 2's `ResolvedHlsLeaf` means the verified leaf was already selected; `SelectFormat` uses `best` (or `bestaudio/best` for requested audio) without a height expression.

**R2 immutable intent/prepared request:** Define in `UserDownloadIntent.cs` the records `UserDownloadIntent(Uri SelectedSource, string Quality, bool AudioOnly, string OutputDirectory, string? CookieSelection, long SessionEpoch)` and `PreparedDownload(Uri Source, Uri? Referer, string? CookiesFile, string? UserAgent, string? LocalProxy, Uri? HlsVideoSource, Uri? HlsAudioSource, bool DirectManifest, bool ResolvedHlsLeaf, string? SuggestedBaseName, double? ExpectedDurationSeconds, bool? ExpectedAudio)`. The same file defines `DownloadRequestFactory.Create(UserDownloadIntent intent, PreparedDownload prepared): DownloadRequest`. Capture intent synchronously before the first await; all selection/quality/audio/output/cookie-selector reads occur only there. Prepare scoped auth, selected-leaf preflight and egress inside the same operation/page/session lease, then call Create exactly once from intent plus verified prepared values. Final request is never reconstructed or enriched from fresh UI state. Private App boundary becomes `DownloadSourceAsync(UserDownloadIntent intent, CancellationToken operationToken)` until Task 10 delegates to the production orchestration service. Task 14 replaces prepared raw proxy evidence with verified capability fields. Cost-if-wrong: transport preparation must remain inside the same operation lease.

- [ ] **RED:** `Run-VgTests 't07-red' 'FullyQualifiedName~Audit_A003'`; extend selector test with 2160/4320 and runner argument assertions for global360/perVideo720 and unknown-height360/global720.

```csharp
Assert.True(DownloadQuality.TryParse("1440p", out var quality));
Assert.Equal(1440, quality.MaximumHeight);
Assert.Equal("1440p", quality.ToTag());
Assert.False(DownloadQuality.TryParse("broken", out _));
// In YtDlpDownloaderTests' recording runner:
Assert.DoesNotContain(spec.Arguments, argument => argument.Contains("height", StringComparison.Ordinal));
```

- [ ] **GREEN implementation:** Replace both hardcoded allowlists with the shared parser; set IsResolved/ResolvedHlsLeaf from actual plan. Capture UserDownloadIntent in `DownloadCandidateAsync` before first await, then prepare auth/preflight/egress and create final DownloadRequest exactly once using DownloadRequestFactory. Task 10 extracts the entire owned flow into a production service. Numeric non-leaf selectors interpolate only parsed integer, never raw text. Persist queue tag unchanged for valid dynamic quality. A TCS preparation test changes every global control while awaiting: recorded final request retains original intent fields, receives the verified late CookiesFile/leaf/egress and no later UI values.
- [ ] **GREEN/integration:** Run `Audit_A003|HlsTrackSelectorTests|BrowserDownloadQueueTests|YtDlpDownloaderTests|Audit_Six_real_parts`. Six-part test uses captured per-video quality with ResolvedHlsLeaf, not a hardcoded global best workaround. Verify selected height 360/720, a separate 1440/2160 selector fixture, audio, expected duration, frame identity and full decode; check globalbest positive control and both conflicting global-quality cases.
- [ ] **Review/commit:** Stage twelve named files; commit `fix: preserve one resolved media quality choice`.

### Task 8: Safe VOD and live duration semantics

**Files:** Modify `src/VideoGrabber.Infrastructure/Browser/HlsManifestParser.cs`, `MediaCandidatePresentation.cs`, `src/VideoGrabber.App/MainWindow.HlsPreflight.cs`; extend Infrastructure tests `AuditBrowserRegressionTests.cs`, `HlsDurationTests.cs`, `MediaCandidatePresentationTests.cs`.

**Interfaces:** Append to `HlsManifestInfo`: `bool HasEndList = false`, `double? WindowDurationSeconds = null`, `bool DurationInvalid = false`; computed `IsLive => !IsMaster && !HasEndList`. Sum is valid only if each EXTINF is finite, positive, and total <= 31,536,000 seconds (365 days). `DurationSeconds` is non-null only for valid ENDLIST media or a master enriched from such media. This cap safely fits formatting and is a documented parser limit, not inferred content duration.

- [ ] **RED:** `Run-VgTests 't08-red' 'FullyQualifiedName~Audit_A008|FullyQualifiedName~Audit_A009'`. Add Infinity,1e309,NaN,-1,zero, two finite values exceeding cap and a direct presentation call with an unsafe externally constructed model.

```csharp
Assert.True(HlsManifestParser.TryParse("#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXTINF:10,\na.ts\n", new Uri("https://cdn.example/live.m3u8"), out var live));
Assert.Null(live!.DurationSeconds);
Assert.True(live.IsLive);
Assert.Equal(10, live.WindowDurationSeconds);
```

- [ ] **GREEN implementation:** Validate during accumulation (`sum > cap - segment` prevents overflow); mark invalid and leave duration null while still scanning encryption tags. ENDLIST gating is independent of encryption policy. Presentation checks finite/range before TimeSpan conversion; show unknown or `Прямой эфир`, never window length as full video. Enrichment copies only complete VOD duration; no duration poisoning in suggested filenames.
- [ ] **GREEN:** Run `Audit_A008|Audit_A009|Audit_Control_Vod|HlsDurationTests|MediaCandidatePresentationTests|HlsManifestParserTests`. Check fractional ENDLIST sum 20.75, live transitions to ENDLIST and all malformed values without an exception.
- [ ] **Review/commit:** Stage six files; commit `fix: distinguish bounded vod duration from live window`.

### Task 9: Page generations and bounded probes

**Files:** Create `src/VideoGrabber.Infrastructure/Browser/BrowserPageLifetime.cs`; modify `src/VideoGrabber.App/MainWindow.Metadata.cs`, `MainWindow.DevTools.cs`, `MainWindow.HlsPreflight.cs`, `MainWindow.Browser.cs`; create Infrastructure test `BrowserPageLifetimeTests.cs`; extend `BrowserWiringRegressionTests.cs`.

**Interfaces:** `BrowserPageLifetime(int maxConcurrentProbes = 3)` implements IDisposable; `CurrentGeneration: long`; `Reset(): void` cancels old CTS and increments generation; `Capture(): BrowserPageLease`; `IsCurrent(BrowserPageLease lease): bool`; `RunProbeAsync(BrowserPageLease lease, Func<CancellationToken,Task> work): Task`. `BrowserPageLease` is an immutable record `(long Generation, CancellationToken Token)`. UI additionally captures CoreWebView2 reference and requires identical core before each write; the infrastructure type never references WebView2. Semaphore=3; page token linked with 25-second per-probe timeout; queued work cancels without starting.

- [ ] **RED:** Write `BrowserPageLifetimeTests` that delays response A via TCS, resets to B, then completes A. Also enqueue 200 delayed probes and track peak active count with Interlocked. No timing-only test is acceptable; TCS gates prove transitions.

```csharp
[Fact]
public void Reset_invalidates_previous_lease()
{
    using var pages = new BrowserPageLifetime();
    var previous = pages.Capture();
    pages.Reset();
    Assert.True(previous.Token.IsCancellationRequested);
    Assert.False(pages.IsCurrent(previous));
    Assert.True(pages.IsCurrent(pages.Capture()));
}
```

- [ ] **GREEN implementation:** Thread lease and core through `CaptureBrowserMetadataAsync`, `RefreshBrowserFrameTreeAsync`, delayed binding refresh and `RefreshCandidateBindingAsync`; check after each await and inside dispatched mutation. Guard verified-cache writes, queue merge and dictionary removal as well as labels. Key in-flight probes by `(Generation, Source)` so old finally cannot remove new work. Reset/DestroyBrowser/window close cancels probes and stale callback effects; new generation has a fresh verified cache.

```csharp
if (!pages.IsCurrent(lease) || !ReferenceEquals(_mediaBrowser?.CoreWebView2, core)) return;
// Repeat inside DispatcherQueue.TryEnqueue immediately before writing shared fields.
```

- [ ] **GREEN:** `Run-VgTests 't09-green' 'FullyQualifiedName~BrowserPageLifetimeTests|FullyQualifiedName~BrowserWiringRegressionTests'`. Assert peak<=3 for 200 masters; navigate/close cancels running+queued work; delayed metadata/frame/dispatch from A leaves B source, selection, queue metadata and verified set unchanged. GUI gate must separately exercise real late iframe/navigation.
- [ ] **Review/commit:** Stage seven files; commit `fix: bind browser async work to page lifetime`.

### Task 10: One cancellable operation from preflight to completion

**Files:** Create `src/VideoGrabber.Core/Processes/OperationCoordinator.cs`, `src/VideoGrabber.Infrastructure/Browser/BrowserDownloadOperation.cs`, `IBrowserDownloadPreparation.cs`; modify `src/VideoGrabber.App/MainWindow.Download.cs`, `MainWindow.BatchDownload.cs`, `MainWindow.MediaActions.cs`, `MainWindow.xaml.cs`, `MainWindow.Browser.cs`; create App `BrowserDownloadPreparation.cs` (WebView2 adapter only); create Infrastructure tests `OperationCoordinatorTests.cs`, `BrowserDownloadOperationTests.cs`; extend `BatchDownloadWiringTests.cs`.

**Interfaces:** New `OperationOutcome { Succeeded, Failed, Cancelled }`; `OperationCompletion { None, StartQueue, CloseWindow }`; `OperationCoordinator.TryBegin(): bool`, `RequestQueue(): void`, `RequestClose(): void`, `Cancel(): void`, `Complete(OperationOutcome outcome): OperationCompletion`, read-only `IsBusy: bool`. Coordinator owns state only; production BrowserDownloadOperation owns the linked CTS across preparation and download. Cancel clears queue intent; close has priority; successful completion may start an explicitly requested pending queue, failed/cancelled completion never does. Replace the private DownloadAttemptOutcome with Core OperationOutcome consistently.

**R7 production orchestration seam:** `IBrowserDownloadPreparation.PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token): Task<PreparedBrowserDownload>`; `PreparedBrowserDownload : IAsyncDisposable` exposes `Values: PreparedDownload` and owns its scoped cookie/transport disposal callbacks. `BrowserDownloadOperation(IBrowserDownloadPreparation preparation, IVideoDownloader downloader, OperationCoordinator coordinator)` exposes `RunAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken windowToken): Task<BrowserOperationResult>`, `RequestQueue(): void`, `Cancel(): void`, `RequestClose(): void`. Result is `BrowserOperationResult(OperationOutcome Outcome, OperationCompletion Completion, DownloadResult? Download)`. RunAsync atomically rejects a second operation before preparation, links lease/window cancellation, awaits preparation, checks current session/lease, creates the final DownloadRequest exactly once via Task 7 factory, invokes downloader once, classifies failure/cancel, disposes prepared resources, and calls coordinator.Complete exactly once in finally. The service returns the completion action; WinUI only renders/dispatches it. No state machine is reimplemented in a test-only fake host. App BrowserDownloadPreparation adapts cookie manager/dispatcher and existing HLS APIs; it does not decide queue/close/outcome policy. Local editor/ASR keep shared coordinator completion and can never overlap service ownership.

- [ ] **RED:** First extract existing preflight→download behavior into the named production service with App delegation without correcting the cancellation/order defect; run existing controls. Then test that service with a TCS-blocked IBrowserDownloadPreparation and recording IVideoDownloader so repeated click has exactly one in-flight preparation and cancel/close produces zero downloader calls. These fakes supply external effects only; all orchestration is production BrowserDownloadOperation. Retain state tests below as focused supplementary checks.

```csharp
[Theory]
[InlineData(OperationOutcome.Failed)]
[InlineData(OperationOutcome.Cancelled)]
public void Unsuccessful_completion_cannot_start_pending_queue(OperationOutcome outcome)
{
    var operations = new OperationCoordinator();
    Assert.True(operations.TryBegin());
    operations.RequestQueue();
    Assert.Equal(OperationCompletion.None, operations.Complete(outcome));
    Assert.False(operations.IsBusy);
}
```

- [ ] **GREEN implementation:** BrowserDownloadOperation begins before `RefreshCandidateBindingAsync`/preflight's first await; capture Task 7 UserDownloadIntent before RunAsync and create final request after verified preparation inside its lease. Pass its operation token throughout so download does not reject its own preflight owner. Convert preflight timeout/network failures to one visible Failed result. Common App `CompleteOperation` restores controls and dispatches only the already-computed service completion; it must not call coordinator.Complete twice. Local trim/join/MP3/Whisper paths call coordinator.Complete once and use the same renderer. Queue continuation after successful media operation is allowed only if explicitly requested; close wins.

```csharp
switch (result.Completion)
{
    case OperationCompletion.CloseWindow: DispatcherQueue.TryEnqueue(Close); break;
    case OperationCompletion.StartQueue: DispatcherQueue.TryEnqueue(async () => await DownloadQueuedCandidatesAsync()); break;
}
```

- [ ] **GREEN:** `Run-VgTests 't10-green' 'FullyQualifiedName~BrowserDownloadOperationTests|FullyQualifiedName~OperationCoordinatorTests|FullyQualifiedName~BatchDownloadWiringTests|FullyQualifiedName~BrowserWiringRegressionTests|FullyQualifiedName~CallbackFailureRegressionTests'`. Cover success, Cancel, 401,403, timeout, double click, close during each media action, close+pending queue and stale progress. Assert production service preparation/downloader call counts, resource disposal and exactly one completion. Pending entries stay present; no new call after Cancel/failure. App source-text wiring only confirms thin delegation; production service tests are primary behavioral evidence. GUI gate separately proves actual close/controls. R7 cost-if-wrong: modest extraction from WinUI partials.
- [ ] **Review/commit:** Stage twelve files; commit `fix: unify preflight and media operation lifetime`.

### Task 11: Preserve queued work across navigation without stale session reuse

**Files:** Modify `src/VideoGrabber.Infrastructure/Browser/BrowserDownloadQueue.cs`, `BrowserDownloadOperation.cs`, `src/VideoGrabber.App/MainWindow.BatchDownload.cs`, `MainWindow.DevTools.cs`, `MainWindow.Browser.cs`; extend Infrastructure tests `BrowserDownloadQueueTests.cs`, `BrowserSessionTests.cs`, `BrowserDownloadOperationTests.cs`.

**Interfaces:** Append immutable `BrowserQueueContext? Context = null` to `BrowserDownloadQueueItem`; record `BrowserQueueContext(Uri Page, long SessionEpoch, BrowserPageMetadata Metadata)`. Add optional context to `AddOrUpdate`. Epoch is local session selection/logout identity, not account auth. Queue copies candidates, per-video quality and page metadata; discovery reset never clears queue. Navigation pauses active/pending queue and requires explicit Continue; Continue is allowed only when the saved session epoch still matches. Logout/browser destruction/session-selector change increments epoch and leaves old items visibly `Требуется повторный выбор сессии`; no silent old-item cookie refresh under a different identity. Do not implement disk persistence (separate platform gap).

**R7 lifecycle seam:** Add to production BrowserDownloadOperation `OnNavigation(long currentSessionEpoch): void`, `OnSessionChanged(long currentSessionEpoch): void`, `CanContinue(BrowserQueueContext context, long currentSessionEpoch): bool`, `RunQueuedAsync(BrowserDownloadQueueItem entry, UserDownloadIntent intent, BrowserPageLease lease, long currentSessionEpoch, CancellationToken windowToken): Task<BrowserOperationResult>`. Navigation clears run intent/cancels current page operation, never mutates queue entries; session change invalidates epoch and also cancels. RunQueuedAsync verifies matching captured Source/quality/session before preparation, rejects mismatch with Failed and zero downloader calls, then delegates to RunAsync. Metadata and slot lists are copied into queue snapshots, not shared mutable lists. App navigation/logout handlers call these production methods.

- [ ] **RED:** Add a test invoking the production BrowserDownloadOperation.OnNavigation/OnSessionChanged/RunQueuedAsync methods using a real BrowserDownloadQueue and fake external preparation/downloader only. Capture A context, navigate to B, assert entries unchanged and pending intent cleared; then switch session and assert RunQueuedAsync makes zero preparation/downloader calls. Do not use a test-only UI host reproducing policy. Core queue snapshot assertions:

```csharp
var queue = new BrowserDownloadQueue();
var candidate = new MediaCandidate(new Uri("https://cdn.example/a.m3u8"), new Uri("https://school.example/a"), "HLS");
var context = new BrowserQueueContext(candidate.Referer, 1, BrowserPageMetadata.Empty);
queue.AddOrUpdate(candidate, 4, "720p", context);
Assert.Equal(context, Assert.Single(queue.Items).Context);
Assert.Equal("720p", Assert.Single(queue.Items).Quality);
```

- [ ] **GREEN implementation:** Remove queue Clear from discovery reset; do not call it indirectly on navigation. Render queue from stored Metadata, never current `_browserMetadata`; avoid refreshing queued candidate bindings against another page. Require explicit user requeue/revalidation after session epoch mismatch, retain original item and explain why. Visible remove/clear still requires the user's explicit action; no automatic destructive clear.
- [ ] **GREEN:** `Run-VgTests 't11-green' 'FullyQualifiedName~BrowserDownloadOperationTests|FullyQualifiedName~BrowserDownloadQueueTests|FullyQualifiedName~BrowserSessionTests|FullyQualifiedName~OperationCoordinatorTests'`. A→B preserves source/order/quality/title; same-session Continue uses A source snapshot; logout/session switch causes zero downloader calls for old entries; no persistence claim on restart.
- [ ] **Review/commit:** Stage eight files; commit `fix: separate browser queue and navigation lifetime`.

### Task 12: Reliable process-tree fixture, then owned-tree cancellation

**Files:** First modify only `tests/VideoGrabber.Infrastructure.Tests/AuditNetworkRegressionTests.cs`; create `tests/VideoGrabber.Infrastructure.Tests/Fixtures/ProcessTreeFixture.ps1`. After behavioral RED modify `src/VideoGrabber.Infrastructure/Processes/ProcessRunner.cs`; create `WindowsProcessJob.cs`, `INativeChildProcessHandle.cs`, `WindowsSuspendedProcessLauncher.cs` there; extend Infrastructure tests `ProcessLifecycleTests.cs`, `CallbackFailureRegressionTests.cs`; create `WindowsSuspendedProcessLauncherTests.cs` in that test directory.

**Interfaces (R8):** Keep public `IProcessRunner.RunAsync(ProcessSpec, Action<string>?, CancellationToken)` and ProcessResult unchanged. Internal `INativeChildProcessHandle : IDisposable` exposes `int Id`, `StreamReader StandardOutput`, `StreamReader StandardError`, `bool HasExited`, `int ExitCode`, `Task WaitForExitAsync(CancellationToken token)`, `void TerminateOwnedTree()`. Internal `WindowsSuspendedProcessLauncher.Start(ProcessSpec spec): INativeChildProcessHandle` calls CreateProcessW suspended with STARTUPINFOEX and an explicit inherited stdout/stderr write-handle list; the parent owns non-inherited read handles and UTF-8 StreamReaders. It assigns the suspended root to WindowsProcessJob with KILL_ON_JOB_CLOSE before ResumeThread. The returned native handle owns process/thread/job/pipe handles, supplies the streams and wait/exit status directly; Process.GetProcessById is not a substitute for redirected readers. ProcessRunner consumes this abstraction, never Process.Start followed by racy assignment. Quote arguments using Windows command-line escaping, preserve WorkingDirectory, avoid shell expansion, and close every handle on partial launch failure. Independent normal-exit drain deadline=2 seconds, cleanup deadline=5 seconds. Preserve user cancellation as OperationCanceledException, process timeout as TimeoutException, callback failure as original exception. Cost-if-wrong: P/Invoke complexity and Windows-only native tests.

- [ ] **Fixture first:** Replace dependence on grandchild stdout for setup with a test-owned PID/ready file plus parent's stdout PID marker. Synthetic child holds inherited pipe and waits up to 30 seconds; parent writes child PID and exits. Validate actual parent exit, child PID+start time and readiness before cancellation. Test finally only targets that owned PID/job; bounded independent child timeout protects failed setup. Fixture must independently prove inherited pipe remains open.
- [ ] **Behavioral RED required:** `Run-VgTests 't12-red' 'FullyQualifiedName~Cancellation_after_parent_exit_must_stop_pipe_holding_descendant_promptly'`. Assertions after proven setup:

```csharp
Assert.True(parentExited);
Assert.False(ownedChild.HasExited);
cancel.Cancel();
await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(TimeSpan.FromSeconds(6)));
Assert.True(ownedChild.HasExited);
```

If setup cannot reach these assertions, label VG-AUD-021 fixture BLOCKED and make no production change for this task. The audit's previous `startedChild.WaitAsync` timeout is not RED evidence of orphan behavior.
- [ ] **GREEN implementation after RED:** Create/own job handle before root execution. Always close/terminate owned job during cancellation/failure/finally even if root has exited; observe both pump tasks after closing readers on deadline. Cleanup errors are logged sanitized as secondary, never replace original cancellation/timeout/callback outcome. No process-name enumeration or killing foreign PIDs.
- [ ] **GREEN:** `Run-VgTests 't12-green' 'FullyQualifiedName~Cancellation_after_parent_exit_must_stop_pipe_holding_descendant_promptly|FullyQualifiedName~WindowsSuspendedProcessLauncherTests|FullyQualifiedName~ProcessLifecycleTests|FullyQualifiedName~ProcessRunnerSafetyTests|FullyQualifiedName~CallbackFailureRegressionTests'`. Require parent-exit/child-alive fixture, actual inherited stdout/stderr pipes, no surviving descendant after cancel/normal-drain-timeout/callback error, bounded completion and correct classification. Repeated 20 immediate-child-spawn iterations prove assignment precedes execution; test quoted paths/Unicode/embedded quotes, failed CreateProcess/assignment, closed read/write handle cleanup. Success output tail and UTF-8 remain intact. R4 exception: installer Commit is host-owned and shielded; this process-tree cancellation applies only to its Prepare/external child, never to pointer swap.
- [ ] **Review/commit:** Stage nine files; commit `fix: own subprocess trees through process completion`.

### Task 13: Component installation joins operation lifetime

**Files:** Modify `src/VideoGrabber.App/MainWindow.xaml.cs`, `MainWindow.Download.cs`; create `src/VideoGrabber.Infrastructure/Components/ComponentInstaller.cs`, `IComponentInstallTransaction.cs`; create `tests/VideoGrabber.Infrastructure.Tests/ComponentInstallerTests.cs`.

**Interfaces (R4):** `ComponentInstaller(IProcessRunner runner, IComponentInstallTransaction transaction).InstallAsync(string script, string destination, CancellationToken token): Task<ProcessResult>` implements `Prepare → Commit → Complete` or `Prepare/Commit → AbortRecovery → Failed/Cancelled`. `IComponentInstallTransaction` declares `CommitAsync(string destination, CancellationToken recoveryDeadline): Task`, `AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline): Task`. Prepare is noninteractive powershell under Task 12 runner with 10-minute timeout and user cancellation. It only stages/verifies, never swaps live pointer. Commit is bounded, shielded host-owned transaction, not the cancellable child process; Task 17 implements it. On cancellation/child crash/kill, host-owned AbortRecovery finishes journal rollback before InstallAsync returns or throws; user cancellation does not cancel recovery. Commit+recovery deadline=30 seconds total, with pointer operations using bounded retries; if recovery cannot complete, operation remains failed/unready with explicit journal recovery error, never successful/cancelled-ready. No code may interrupt the pointer swap. `MainWindow.InstallComponentsAsync(bool forceUpdate, CancellationToken cancellationToken)` receives existing download token or manually owned operation token; `_isInstallingComponents` remains status, not sole ownership. Cost-if-wrong: cancel may wait bounded recovery.

- [ ] **RED:** Stub IProcessRunner delays until cancellation and records ProcessSpec. Begin manual install, request close, cancel, then assert completion before a 6-second test timeout; download auto-install receives same token. Required assertions:

```csharp
Assert.Equal(TimeSpan.FromMinutes(10), spec.Timeout);
Assert.Contains("-NonInteractive", spec.Arguments);
Assert.Contains(destination, spec.Arguments);
await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installTask);
```

- [ ] **GREEN implementation:** Route install through ComponentInstaller and common completion. Closing cancels Prepare and awaits owned process-tree exit plus AbortRecovery; once Commit starts, defer cancel/close completion until shielded commit/recovery finishes. Test a recording IComponentInstallTransaction now; Task 17 supplies the actual journal transaction. Catch cancellation separately only after recovery. After verified success, request Task 17 component snapshot activation before readiness/status refresh. Until Task 17 lands do not run actual updates on installed tools.
- [ ] **GREEN:** `Run-VgTests 't13-green' 'FullyQualifiedName~ComponentInstallerTests|FullyQualifiedName~OperationCoordinatorTests|FullyQualifiedName~ProcessLifecycleTests'`. Synthetic long installer in a unique test destination leaves no orphan; success stub preserves status refresh; cancel during auto-install produces no downloader call.
- [ ] **Review/commit:** Stage five files; commit `fix: cancel and await component installation`.

### Task 14: Mandatory egress guard independently of routing

**Files:** Modify `src/VideoGrabber.Core/Security/UrlPolicy.cs`, `src/VideoGrabber.Core/Downloads/DownloadRequest.cs`, `UserDownloadIntent.cs`, `src/VideoGrabber.Infrastructure/Networking/RouteConnector.cs`, `SiteRouteProxy.cs`, `src/VideoGrabber.Infrastructure/Downloads/YtDlpDownloader.cs`, `src/VideoGrabber.Infrastructure/Browser/HlsPreflightClient.cs`, `BrowserDownloadOperation.cs`, `src/VideoGrabber.App/MainWindow.Network.cs`, `MainWindow.HlsPreflight.cs`, `BrowserDownloadPreparation.cs`; create `src/VideoGrabber.Core/Security/IManagedEgressSessionRegistry.cs`, `src/VideoGrabber.Infrastructure/Networking/DownloadEgressSession.cs`, `ManagedEgressSessionRegistry.cs`; create Infrastructure tests `DownloadEgressTests.cs`, `Fixtures/ManagedEgressFixture.cs`; extend Core `UrlPolicyTests.cs` and Infrastructure `RoutingDownloadTests.cs`, `AuthenticatedHlsDownloadIntegrationTests.cs`, `AuthenticatedDownloadIntegrationTests.cs`, `AuditSixPartsIntegrationTests.cs`, `AuditNetworkRegressionTests.cs`. These are the existing loopback downloader/preflight callers found by targeted scan; native SiteProxy tests that never call downloader/preflight retain their isolated socket tests.

**Interfaces (R5):** Replace trust in raw LocalProxy with opaque managed capability. Core defines `EgressSessionLease(Guid Id, Uri ProxyUri, EgressPolicy Policy)` and immutable `EgressPolicy(string Name, bool PublicOnly)`; The same Core file declares `IManagedEgressSessionResolver.Resolve(Guid id, Uri exactEndpoint): EgressSessionLease`; `IManagedEgressSessionRegistry : IManagedEgressSessionResolver` additionally declares `Issue(Uri proxyUri, EgressPolicy policy): EgressSessionLease`, `Revoke(Guid id): void`. Implementation registry is process-local, generates unpredictable IDs, allows issuance only through owned DownloadEgressSession construction, validates registered endpoint and lifetime, and revokes on Dispose; callers receive a restricted resolver interface outside composition/factory code. PublicOnly=false is never selectable from UI/config: isolated-test factory records named allowed listener endpoints in its privately owned connector, not in ambient policy. DownloadRequest appends `Guid? EgressCapabilityId=null`, `Uri? EgressEndpoint=null`; legacy LocalProxy is rejected unless matching that active capability endpoint, never trusted alone. PreparedDownload and HlsPreflightFetchOptions carry the same Id/endpoint. DownloadRequestFactory copies verified capability from prepared outputs; downloader and preflight resolve the active registry entry and exact endpoint immediately before network use and throughout lease lifetime. Unknown/disposed/mismatched capability fails before a process/request is started. YtDlpDownloader constructor appends `IManagedEgressSessionRegistry? egressRegistry=null`; its default private composition creates/registers mandatory public-only session only when no caller capability/proxy is supplied, and never adopts a raw endpoint. HlsPreflightClient takes the shared registry for supplied capabilities. DownloadEgressSession owns registry lease and proxy, uses RouteConnector DNS validation→pinned IP connect for every SOCKS CONNECT; adapter choice remains independent. Consolidate normalized public-IP policy in Core. Cost-if-wrong: interface/API change across Core/App/Infrastructure.

**R6 fixture migration:** `ManagedEgressFixture.RegisterListener(string name, Uri listener, IManagedEgressSessionRegistry registry)` returns an IDisposable fixture exposing a registered Lease and connector for that exact owned host/port. Migrate every downloader/preflight call in the Infrastructure integration/regression files listed above to this named-listener capability; forbidden loopback listener is never registered. Scan again before implementation with `rg -n 'new YtDlpDownloader|new HlsPreflightClient|127\\.0\\.0\\.1|localhost' tests/VideoGrabber.Infrastructure.Tests -g '*.cs'`; add any newly introduced affected caller to the task allowlist and record why before editing it. No global loopback bypass. Cost-if-wrong: more fixture migration.

- [ ] **RED/transport feasibility:** Add `DefaultRouteNestedManifestMustNotReachLoopback`, `RedirectToPrivateMustFail`, `DomainResolvingToPrivateMustFail`, `Ipv4MappedIpv6AndUlaMustBeRejected`. Public-looking fixture hostname resolves through the test connector to an owned public-response fixture; nested forbidden targets point only to an isolated loopback listener with hit counter. Never probe real LAN. The parser-only rejection is a control, not proof for yt-dlp/DASH redirects.

```csharp
[Theory]
[InlineData("http://[::ffff:127.0.0.1]/a")]
[InlineData("http://[fd00::1]/a")]
public void Private_address_forms_are_rejected(string input)
{
    Assert.False(UrlPolicy.TryValidate(input, out _, out _));
}
// In real downloader fixture after the failed request:
Assert.Equal(0, forbiddenListenerHits);
Assert.False(result.Success);
```

- [ ] **Mandatory feasibility checkpoint:** With installed yt-dlp/FFmpeg versions, capture exact actual commands and verify HLS/DASH segments, redirects, ffmpeg network fallback and remote JS components. SOCKS arguments alone do not prove FFmpeg honors them. Choose the bounded supported execution path: yt-dlp native downloader under guarded SOCKS; ffmpeg receives only local owned media files and an explicit protocol allowlist excluding network protocols. Disable remote `ejs:github` component fetching during execution; allow only locally verified components. If an extractor requires uncontrolled network FFmpeg/Deno or the tool ignores the proxy, fail closed with an unsupported-path message. If enforcing this supported path cannot be demonstrated, task stays BLOCKED; do not claim SSRF solved or change system firewall/VPN as a workaround.
- [ ] **GREEN implementation:** EnsureRoutingProxy returns an active EgressSessionLease from managed registry even with no adapter rule; App preparation owns its lifetime through download completion. Downloader/preflight resolve Id plus exact endpoint and reject forged, disposed and mismatched leases. Keep selected interface binding/session checks. All ordinary/direct/split paths use registry-validated guard; manual preflight uses pinned connector. Reject unsupported protocols/native fallback; FFmpeg only processes owned local paths with network protocols absent.
- [ ] **GREEN:** `Run-VgTests 't14-green' 'FullyQualifiedName~DownloadEgressTests|FullyQualifiedName~RoutingDownloadTests|FullyQualifiedName~SiteRouteTests|FullyQualifiedName~SiteProxyTests|FullyQualifiedName~AuthenticatedHlsDownloadIntegrationTests|FullyQualifiedName~AuthenticatedDownloadIntegrationTests|FullyQualifiedName~Audit_Six_real_parts|FullyQualifiedName~Cross_host_hls_redirect'`; separately `& $vgDotnet test tests/VideoGrabber.Core.Tests/VideoGrabber.Core.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UrlPolicyTests`. Arbitrary proxy/unknown Id/disposed Id/endpoint mismatch produce zero process/network calls; active registered own capability succeeds. Positive public target succeeds through pinned connector, DNS answer change cannot move connection, forbidden nested target remains zero hits; real named-loopback controls all execute without skip. Record extractor limitations.
- [ ] **Review/commit:** Stage only the explicit Task 14 file allowlist plus any newly scanned caller recorded before edits; commit `fix: enforce downloader egress on every supported path`.

### Task 15: Validate both transcription artifacts

**Files:** Create `src/VideoGrabber.Infrastructure/Transcription/SrtValidator.cs`; modify `WhisperTranscriber.cs` there; extend Infrastructure tests `AuditMediaRegressionTests.cs`, `TranscriptionTests.cs`, `WhisperIntegrationTests.cs`.

**Interfaces:** `SrtValidator.TryValidate(string text, double? mediaDurationSeconds, out string? error): bool`. Unknown duration is null. WhisperTranscriber maps MediaProbeResult.DurationSeconds to null unless finite and strictly positive; validator rejects non-null NaN/infinity/nonpositive values as invalid caller input. Limits: 16 MiB UTF-8 file before read, 100,000 cues, 65,536 characters per cue. Accept CRLF/LF, BOM, Unicode speech and multi-line cue text. Require >=1 nonempty cue, valid `HH:MM:SS,mmm` with minutes/seconds<60, strictly increasing cue starts, end>start, non-overlapping intervals, cue end <= media duration+1 second when duration known. Null disables only duration-bound check, never cue ordering/validity. Keep unvalidated transcript/SRT in owned ASR directory on failure, do not promote either file; diagnostics contain only error kind/count, never transcript text. Add null,0,NaN,infinity and finite-positive validator controls.

- [ ] **RED:** Run existing `Transcription_must_reject_empty_or_reversed_srt`; add standalone validator tests:

```csharp
[Fact]
public void Valid_unicode_cues_pass_and_reversed_cues_fail()
{
    Assert.True(SrtValidator.TryValidate("1\n00:00:00,100 --> 00:00:01,000\nПривет, мир\n", 10, out _));
    Assert.False(SrtValidator.TryValidate("1\n00:00:05,000 --> 00:00:01,000\nSpeech\n", 10, out _));
    Assert.False(SrtValidator.TryValidate("", 10, out _));
}
```

- [ ] **GREEN implementation:** Check file size before bounded reading; parse with invariant timestamp arithmetic. Validate TXT and SRT before either File.Move. On validation/cancellation error retain job artifacts and return a sanitized actionable error. Successful pair promotion remains no-overwrite; rollback removes only newly promoted owned TXT if SRT move failed.
- [ ] **GREEN:** `Run-VgTests 't15-green' 'FullyQualifiedName~Transcription_must_reject|FullyQualifiedName~TranscriptionTests|FullyQualifiedName~WhisperIntegrationTests'`. Real local Whisper control validates all cues, media timing and both nonempty outputs; malformed UTF/timestamps, duplicate/decreasing cue times, empty text, out-of-range end, held output handle and cancellation preserve source and foreign files.
- [ ] **Review/commit:** Stage five files; commit `fix: validate subtitle cues before transcription success`.

### Task 16: Report incomplete diagnostics coverage

**Files:** Modify `scripts/Analyze-Logs.ps1`, `scripts/Test-Diagnostics.ps1`.

**Interfaces:** Keep schemaVersion=1 and existing fields; `skippedFiles` includes eligible files excluded by count/byte caps. Status is `incomplete` whenever malformed/skipped>0, even when eventCount=0; no_data only when coverage is complete and no usable event exists. Preserve count512, single-file4MiB, total64MiB limits; no Telegram invocation in tests.

- [ ] **RED:** Add to `Test-Diagnostics.ps1` a unique root with 513 current `vg-*.jsonl`, oldest containing the sole failure; call Analyze-Logs with explicit LogDirectory/OutputDirectory. Assert JSON rather than script exit alone:

```powershell
if ($report.status -ne 'incomplete') { throw 'Count cap concealed incomplete coverage' }
if ($report.skippedFiles -ne 1) { throw 'Expected exactly one count-excluded file' }
if ($report.eventCount -ne 512) { throw 'Unexpected scanned event count' }
```

- [ ] **GREEN implementation:** Enumerate eligible files before selecting first512; count all excluded eligible files, avoid counting intentionally expired retention files as malformed coverage. Run resource limits unchanged. Reorder status computation so incomplete precedes no_data:

```powershell
$state = if($malformed -gt 0 -or $skipped -gt 0){'incomplete'}elseif($events -eq 0){'no_data'}elseif($failed -gt 0){'issues_found'}else{'no_errors_observed'}
```

- [ ] **GREEN:** Run `powershell.exe -NoProfile -NonInteractive -File scripts/Test-Diagnostics.ps1`; verify test script uses only its GUID root. Inspect AST with `[System.Management.Automation.Language.Parser]::ParseFile` and require zero errors. Verify daily/weekly/monthly, 512/513, bytes limit, all-skipped, malformed, privacy and retention tests. Save generated synthetic JSON and logs under new run.
- [ ] **Review/commit:** Stage two files; commit `fix: account for omitted diagnostic files`.

### Task 17: Transactional component set with rollback

**Files:** Modify `scripts/Install-Components.ps1`; create `scripts/Test-InstallComponents.ps1`; modify `src/VideoGrabber.Infrastructure/Components/ToolLocator.cs`, `ComponentInstaller.cs`, `IComponentInstallTransaction.cs`; create `ComponentSetSnapshot.cs`, `ComponentServiceFactory.cs`, `ComponentInstallTransaction.cs` in that directory; modify `src/VideoGrabber.Infrastructure/Browser/BrowserDownloadOperation.cs`, `src/VideoGrabber.App/MainWindow.xaml.cs`, `MainWindow.Download.cs`, `MainWindow.MediaActions.cs`, `BrowserDownloadPreparation.cs`; extend Infrastructure tests `ToolLocatorTests.cs`, `ComponentInstallerTests.cs`, `BrowserDownloadOperationTests.cs`; create `ComponentActivationTests.cs` there. App component fields/factory wiring, first-install flow and same-window upgrade are explicitly within this task's allowlist.

**Interfaces:** Installer supports existing `-Destination`; add `-AssetManifestPath` for an explicitly supplied local verified fixture manifest (JSON asset name/path/sha256), no implicit remote substitutions. Stage all four tools into a unique sibling generation, validate digests and run bounded `--version`/`-version` self-tests before selecting any new live set. Add a `components-current.json` pointer containing a relative generation directory and hashes; ToolLocator validates containment and resolves the set once per instance, falling back to legacy flat tools only when no pointer exists. Never mix pointer and legacy per-file fallback. Use atomic replace of the small pointer; retain previous generation and journal until successful reopen/self-test. No deletion of old generations in this task.

**R3 activation contract:** Immutable `ComponentSetSnapshot(string GenerationDirectory, string YtDlp, string Ffmpeg, string Ffprobe, string Deno, IReadOnlyDictionary<string,string> Sha256)` copies its hash map and verifies the entire set. `ComponentServiceFactory.Create(ComponentSetSnapshot snapshot, IProcessRunner runner): ComponentServices`; immutable `ComponentServices(ToolLocator Tools, YtDlpDownloader Downloader, FfmpegVideoEditor Editor, WhisperTranscriber Transcriber)` captures one set. App stores one replaceable `_componentServices` reference, not readonly stale `_tools/_downloader/_editor` fields; atomically swaps the bundle at an idle tool-operation boundary after verified installation, then refreshes readiness/status. Each running media operation captures a bundle and keeps it until done. First-install during a held preparation lease may activate only while no tool/media operation is active; keep the original user intent/lease, atomically activate, then start its download from the new bundle. BrowserDownloadOperation receives `Func<IVideoDownloader>` provider instead of a stale downloader singleton and resolves it once after successful preparation; tests pass a fixed-provider recording downloader. Manual upgrade during active work waits for idle; same-window subsequent download/edit/ASR use the new set without restart. Cost-if-wrong: slightly broader App refactor.

**R4 journal ownership:** Implement Task 13 IComponentInstallTransaction in host Infrastructure. Installer script gains `-Phase Prepare` and writes staged verified manifest/journal only. Host Commit holds destination transaction lock, writes old/new pointer intent durably, atomically swaps pointer, self-tests selected snapshot and marks commit complete. Commit and AbortRecovery share a shielded 30-second deadline; cancel during Commit requests AbortRecovery after the indivisible pointer swap. Process-tree kill only affects Prepare/external children. If Prepare child is killed/crashes, host AbortRecovery restores previous verified set before InstallAsync completes. If host crashes, startup must run AbortRecovery before any component readiness/next InstallAsync returns; the crashed call itself has no return. Crash tests explicitly restart host recovery. Never claim successful cancellation/readiness with unfinished rollback; no orphan and previous verified set remain mandatory. Cost-if-wrong: cancellation may wait bounded recovery.

- [ ] **RED:** New PowerShell script creates a test-owned legacy set and synthetic fixture executables from an already available tool fixture, captures SHA-256, injects second asset failure and ffprobe staging-copy failure through local manifest paths/read-only destination. Assert failures leave original files and pointer hashes unchanged. Test new set selection in ToolLocator:

```csharp
Assert.Equal(expectedGeneration, Path.GetDirectoryName(locator.YtDlp));
Assert.Equal(expectedGeneration, Path.GetDirectoryName(locator.Ffmpeg));
Assert.Equal(expectedGeneration, Path.GetDirectoryName(locator.Ffprobe));
Assert.Equal(expectedGeneration, Path.GetDirectoryName(locator.Deno));
```

- [ ] **GREEN implementation:** Prepare stages/verifies all before host Commit; no current-path Copy-Item -Force per tool. Follow R4 state transitions with durable old/new journal and host recovery; missing trusted digest blocks selection. After verified commit, build a complete R3 bundle and atomically activate it at idle before readiness refresh. No per-service staggered replacement, no live operation can mix generations. No automatic deletion of previous sets.
- [ ] **GREEN:** `powershell.exe -NoProfile -NonInteractive -File scripts/Test-InstallComponents.ps1`; `Run-VgTests 't17-green' 'FullyQualifiedName~ToolLocatorTests|FullyQualifiedName~ComponentInstallerTests|FullyQualifiedName~ComponentActivationTests|FullyQualifiedName~BrowserDownloadOperationTests'`. Include second-download failure, wrong digest, missing ffprobe, demonstrably denied copy (Windows directory ReadOnly alone is not an injection), pointer lock, self-test failure, cancel in Prepare/Commit/AbortRecovery, killed Prepare child, host crash before/after pointer replace and restarted recovery. Assert InstallAsync cannot return while AbortRecovery TCS is held; release it, then assert previous set and zero orphan. Same-window first install turns missing→ready and actual recorded download path uses new generation; upgrade during active operation leaves old captured bundle intact, then next download/editor/transcriber all use new generation. GUI gate repeats same-window first-install+upgrade on disposable tool sets. Original hashes unchanged for precommit failures; no installed-tool mutation on user's host.
- [ ] **Review/commit:** Stage seventeen listed files; commit `fix: activate component updates as verified sets`.

### Task 18: Evidence-first editing contract investigation

**Files:** Initially create only `tests/VideoGrabber.Infrastructure.Tests/AuditEditingRegressionTests.cs`; modify `FfmpegVideoEditorIntegrationTests.cs` there if its synthetic fixture builder is reused. Write findings in external `$vgRun/editing-024.md`. Only after confirmed behavioral RED may modify `src/VideoGrabber.Infrastructure/Editing/FfmpegVideoEditor.cs`, `src/VideoGrabber.Core/Media/IMediaProbe.cs`, `src/VideoGrabber.Infrastructure/Media/FfprobeMediaProbe.cs`, `docs/MEDIA_WORKFLOWS.md`.

**Interfaces:** Existing `EditAsync(VideoEditRequest, CancellationToken)` preserved. If confirmation requires richer probing, append `IReadOnlyList<MediaStreamInfo>? Streams = null` to MediaProbeResult; define `MediaStreamInfo(string Type,string Codec,int? Width,int? Height,string? PixelFormat,string? TimeBase,int? SampleRate,int? Channels)`. Probe reads these fields from ffprobe JSON, retaining existing constructor compatibility.

- [ ] **Characterize first:** Create local lavfi samples (testsrc2+sine, 4 seconds): H.264 320x180 and MPEG4 640x360, plus identical H.264 control. Record source streams/duration/hash. Exercise trim start=3s,duration=3s and join incompatible samples through production editor, then full-decode output and inspect every stream/time bound. Do not assert just output filesize.

```powershell
& (Join-Path $env:VIDEOGRABBER_INTEGRATION_TOOLS 'ffmpeg.exe') -v error -nostdin -f lavfi -i 'testsrc2=size=320x180:rate=25' -f lavfi -i 'sine=frequency=440:sample_rate=48000' -t 4 -c:v libx264 -g 25 -c:a aac (Join-Path $vgRun 'edit-h264.mp4')
& (Join-Path $env:VIDEOGRABBER_INTEGRATION_TOOLS 'ffmpeg.exe') -v error -nostdin -f lavfi -i 'testsrc2=size=640x360:rate=25' -f lavfi -i 'sine=frequency=880:sample_rate=48000' -t 4 -c:v mpeg4 -c:a aac (Join-Path $vgRun 'edit-mpeg4.mp4')
```

- [ ] **RED decision:** `Run-VgTests 't18-characterize' 'FullyQualifiedName~AuditEditingRegressionTests'`. `TrimBeyondEndMustReject` requires an explicit range error; `JoinDifferentCodecMustRejectOrProduceFullyDecodableExpectedDuration` accepts explicit incompatibility failure OR 8-second decoded output within one source GOP, with required streams and recognizable first/tail content. Capture `Record.ExceptionAsync` and both alternatives explicitly:

```csharp
var error = await Record.ExceptionAsync(() => editor.EditAsync(request, token));
if (error is null)
{
    Assert.True(fullDecode.IsSuccess, fullDecode.StandardError);
    Assert.InRange(outputProbe.DurationSeconds, expectedSeconds - 1, expectedSeconds + 1);
    Assert.True(outputProbe.HasVideo && outputProbe.HasAudio);
}
else Assert.IsAssignableFrom<ArgumentException>(error);
```

If no unsafe behavior reproduced, record `not-reproduced` with exact exercised variants; leave untested variants open and do not manufacture a production fix. If fixture/tool unavailable, record BLOCKED. Confirmed RED converts the hypothesis to confirmed evidence with author/date/reproducer in the execution report, never rewrites the audit history.
- [ ] **Conditional GREEN only after RED:** Probe inputs before invocation; require trim end<=source duration and known positive bounds, reject incompatible stream-copy inputs based on ordered stream codec/type/resolution/timebase/audio parameters. No automatic transcoding feature is added; tell user that incompatible streams require explicit re-encoding outside this operation. Verify output stream/duration against expected range before promotion; explain keyframe granularity for fast trim in MEDIA_WORKFLOWS. Unit tests assert preflight refusal makes zero FFmpeg edit calls.
- [ ] **Verification/review:** Run `AuditEditingRegressionTests|FfmpegVideoEditorIntegrationTests|EditorSafetyTests`; positive matching join/valid trim fully decode including tail and preserve original hashes. If fixed, commit only relevant listed files `fix: validate stream copy editing contracts`; if investigation only, commit test/document evidence `test: characterize editing output contracts`. Status is evidence-based, not automatically fixed.

### Task 19: Four desktop gates and final regression

**Files:** No speculative production edits. Create `docs/validation/desktop-stabilization-acceptance.md` containing sanitized outcomes and references to external run artifacts; update it only with observed results. If a gate reveals a defect, return to its owning task, capture RED and review its fix before final rerun.

**Interfaces:** Gate report rows contain `gate`, `status` (PASS/FAIL/BLOCKED), `commit`, `toolVersions`, `commands`, `evidence`, `limitation`, `nextAction`. All actual media and private data remain outside Git. Record expected test count from discovery and compare TRX outcomes; no hidden skip or dropped audit case.

- [ ] **Tool provenance gate:** Inventory actual yt-dlp, FFmpeg, ffprobe, Deno, Whisper executable/model and any local JS component: canonical path, version, SHA-256, source release/tag, upstream digest/signature verification, license/notice/source-offer requirements for the exact artifact. Read `THIRD_PARTY_NOTICES.md` and compare installed payloads. An unsigned binary with a locally computed hash is `provenance unverified`, not PASS. Use official release/license sources; do not install/replace anything to turn the gate green. Missing source attestation or GPL materials keeps gate BLOCKED and prevents release approval. No license/legal conclusion beyond documented materials.
- [ ] **Synthetic media setup:** Copy only the audit-owned `media-fixtures/six-parts` subtree into `$env:VIDEOGRABBER_EVIDENCE/six-parts` and hash-compare the 12 source MP4s; copy no cookies or user profile. Extend required variant fixtures from Tasks 2/7/18 under this run. Verify every source with ffprobe + full decode before interpreting test failures.
- [ ] **Final automated regression:** Run the complete solution once after the last production fix, with all real media tools configured; then inspect TRX for 0 Fail and 0 NotExecuted/Skipped. Compare discovered test names to audit baseline272 plus imported regressions and new tests, documenting renamed/deleted wrong-policy assertions. A fixed number272 is historical context, not the expected final count.

```powershell
& $vgDotnet test VideoGrabber.slnx -c Release --no-restore --logger 'trx;LogFileName=desktop-final.trx' --results-directory (Join-Path $vgRun 'final')
if ($LASTEXITCODE -ne 0) { throw 'Final regression failed' }
& $vgDotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
& $vgDotnet publish src/VideoGrabber.App/VideoGrabber.App.csproj -c Release --self-contained true --no-restore -o (Join-Path $vgRun 'publish')
if ($LASTEXITCODE -ne 0) { throw 'Local publish failed' }
powershell.exe -NoProfile -NonInteractive -File scripts/Test-Diagnostics.ps1
if ($LASTEXITCODE -ne 0) { throw 'Diagnostics regression failed' }
powershell.exe -NoProfile -NonInteractive -File scripts/Test-InstallComponents.ps1
if ($LASTEXITCODE -ne 0) { throw 'Installer transaction regression failed' }
git diff --check
```

No source change exists between final run and tested publish. Preserve standard per-project TRX separately if runner filenames collide. Parse all PowerShell ASTs; validate changed JSON/XML and embedded browser JS through the existing audit syntax/DOM harness without overwriting audit output. Release build remains 0 warnings/errors. Local publish is inspection output, not permission to publish a release.
- [ ] **Full GUI gate:** Launch that exact locally published EXE in a disposable Windows user/profile or proven diagnostics-isolated harness. Verify actual UI: six late/shuffled masters, unknown binding, PART4 retention, per-video quality, queue add/reorder/remove/Continue, navigation and session expiry, Cancel during binding/preflight/download/merge/Whisper/install, close during each action, repeated click, 401/403, empty/error/loading states. Check 100/125/150% scale, keyboard focus/tab/activation, light/dark readability and no clipped controls. Observe actual downloaded source/content and process exit. Use screenshots or human-observed evidence when accessibility permits; the previous `SetIsBorderRequired / 0x80004002` and empty accessibility tree are explicitly unresolved until real inspection succeeds. Report BLOCKED if no supported observation channel is available; model tests do not substitute.
- [ ] **Private GetCourse gate:** In an explicitly authorized real lesson, user signs in manually. Do not read/export session secrets to reports. Confirm six actual source-to-part bindings including two equal durations if that lesson provides them; otherwise list absent scenario and use synthetic coverage only for it. Run chosen per-video quality, audio verification, full duration/metadata and decode on authorized downloaded media, late frame/lesson navigation and queue stop after expired session. Record sanitized source fingerprints/part numbers/height, not signed URLs or cookies. Missing authorized lesson/access leaves this gate BLOCKED; no credential/session copying from another profile.
- [ ] **Clean Windows gate:** Use a clean supported Windows VM without SDK or development-installed runtimes, standard user, fresh user data. Record OS build and absence of SDK, unpack exact test ZIP, hash-check every packaged file. Start EXE, inspect Russian UI, use manually supplied verified portable components and one synthetic download/MP3/trim/join/ASR scenario; validate WebView2 prerequisite messaging both present and absent. Close during synthetic process and reopen; original profile/settings unchanged. No system installation/reboot/network change on the user's host. Missing VM remains BLOCKED, not waived by local launch.
- [ ] **Package verification:** Create a uniquely named local ZIP from final publish, include required notices/licenses/workflows and selected verified component metadata; unpack to a new run-owned directory and compare relative paths+SHA-256 for every file. Launch the unpacked EXE and verify version, author/contact area and normal close. Tool payload distribution awaits provenance gate and separate publication permission.
- [ ] **Final report/commit:** Write acceptance table covering all 26 IDs, original vs new evidence, 024 disposition, four gate statuses, actual test totals and any unresolved matrix entries. Set desktop-ready true only when all required fixes and all four gates pass with no hidden skips; platform-ready remains outside scope. Stage only `docs/validation/desktop-stabilization-acceptance.md`, commit `docs: record desktop stabilization acceptance`. No push, merge or release action.

## Plan self-review

- [x] Preflight R1–R8 incorporated after coordinator rulings: narrow Task2 GREEN, immutable intent then final request, live component-set activation, shielded host transaction/recovery, registry capability, all discovered loopback callers, production selection/orchestration seams, native suspended child streams.
- [x] Unknown SRT duration is nullable; finite-positive conversion and invalid caller controls are explicit.
- [x] App/Infrastructure file allowlists and dependent tests/commands include every ruling; R7 primary evidence exercises production services, R8 pipe readers come from native handles, and R4 prevents killing the pointer-swap owner.

- [x] All VG-AUD-001 through VG-AUD-026 are mapped exactly once in the coverage table; 024 has a conditional evidence-first investigation, not a promised fix.
- [x] All seven P1 defects have initial RED and output-preservation/fail-closed acceptance; default-route SSRF is separated from adapter selection and cannot close by argument inspection alone.
- [x] The known blocked VG-AUD-021 fixture is repaired and independently validated before any production patch; failed setup explicitly blocks that task.
- [x] Six-part acceptance requires correct source hashes for proven binding and null ordinal for unknown binding; it no longer asserts the observed buggy allocation as desired behavior.
- [x] Four release gates remain independent; private access, clean Windows and provenance are not replaced with mocks, SDK-machine launch or computed hashes.
- [x] New interfaces are defined in their owning task and downstream names match: ResolvedHlsLeaf, BrowserPageLease, BrowserQueueContext, OperationOutcome and OperationCompletion.
- [x] No platform auth/TG/payments implementation, no destructive user cleanup, no public publication, no package-version churn.
- [x] No placeholder steps: test filters, actual assertions, concrete file paths, commands, status rules and commit boundaries are supplied.

## Known implementation risks and stop conditions

1. Native Windows process creation/Job Object pipe inheritance must be proven with the fixed fixture; introducing a racy assignment after launch fails the requirement.
2. Default egress enforcement can reduce unsupported extractor compatibility. The implemented supported path must prove external tool behavior; if controlled local-only FFmpeg cannot be guaranteed, VG-AUD-020 stays BLOCKED rather than weakening the boundary.
3. Job filesystem checks must reject junction escape, held handles and collision without damaging foreign files. Deterministic disk-full tests prove error paths, not a real-volume environmental gate.
4. Removing automatic failed/cancelled recovery changes existing recovery tests and user messaging. Preserve partials and explain the change; never present partial media as completed.
5. Component pointer activation changes ToolLocator and packaging assumptions; test legacy fallback, entire-set selection and crash journal replay together. Never hot-switch individual executables during an active job.
6. Four environmental gates may require user-provided authorized access or an isolated Windows machine. Record concrete BLOCKED reasons and exact next action; completion of a plan or unit tests does not itself make the desktop ready.
