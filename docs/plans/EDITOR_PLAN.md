# VideoGrabber Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Добавить в проверенный VideoGrabber.exe безопасный и понятный FFmpeg-редактор для предпросмотра, обрезки, разбиения, склейки и извлечения аудио без перезаписи исходников.

**Architecture:** Editor Core описывает проекты редактирования, интервалы, совместимость и экспортные планы без зависимости от FFmpeg или WinUI. Infrastructure строит и выполняет безопасные FFmpeg/FFprobe-команды через существующий ProcessRunner и MediaValidator. WinUI-вкладка отображает предпросмотр, временную шкалу и очередь экспорта, но не содержит медиалогики.

**Tech Stack:** C# 14, .NET 10, WinUI 3, Windows MediaPlayerElement for preview, existing FFmpeg/FFprobe boundary, xUnit, existing portable win-x64 build.

**Spec:** C:\Users\Oleg\Documents\Codex\2026-09-06\new-chat\outputs\VideoGrabber-design.md

## Global Constraints

- This plan starts only after the downloader milestone passes its offline and approved network acceptance.
- Project root: D:\CODEX\Projects\2026-09-06\video-grabber.
- Target: Windows 10 Enterprise LTSC 2021 21H2 build 19044 x64 and Windows 11 x64.
- The editor remains inside the same VideoGrabber.exe and portable folder.
- Every export uses a new destination path; source files are never overwritten.
- Fast mode uses stream copy and explicitly reports keyframe-boundary limitations.
- Frame-accurate mode re-encodes and displays selected encoder, estimated size and quality settings before export.
- Incompatible concatenation requires explicit confirmation of normalization/re-encoding.
- External process arguments use ProcessStartInfo.ArgumentList and never a shell command string.
- Temporary media remains recoverable until the output passes MediaValidator.
- Every task ends with focused tests and a local commit; no push or publication.

---

## File Map

### Core editor domain

- src/VideoGrabber.Core/Editing/EditModels.cs — sources, time ranges, operations, profiles, plans and results.
- src/VideoGrabber.Core/Editing/EditProject.cs — immutable project state and validation.
- src/VideoGrabber.Core/Editing/IEditPlanner.cs — probe-to-export-plan contract.
- src/VideoGrabber.Core/Editing/IEditExecutor.cs — execution contract.
- src/VideoGrabber.Core/Editing/ConcatCompatibility.cs — compatibility report.

### Infrastructure

- src/VideoGrabber.Infrastructure/Editing/EditPlanner.cs — chooses copy, precise encode, normalization or rejection.
- src/VideoGrabber.Infrastructure/Editing/FfmpegEditExecutor.cs — executes trim, split, concat and audio extraction.
- src/VideoGrabber.Infrastructure/Editing/EncoderCatalog.cs — tested hardware/software encoders.
- src/VideoGrabber.Infrastructure/Editing/TimelineThumbnailService.cs — bounded thumbnail generation.
- src/VideoGrabber.Infrastructure/Editing/EditProjectStore.cs — local autosave without media duplication.

### App

- src/VideoGrabber.App/Editing/EditorPage.xaml and EditorPage.xaml.cs — editor UI.
- src/VideoGrabber.App/Editing/EditorViewModel.cs — project state and commands.
- src/VideoGrabber.App/Editing/TimelineView.xaml and TimelineView.xaml.cs — markers and seek interaction.
- src/VideoGrabber.App/Editing/ConcatQueueView.xaml and ConcatQueueView.xaml.cs — ordered merge list.
- src/VideoGrabber.App/Editing/ExportDialog.xaml and ExportDialog.xaml.cs — mode/profile/output confirmation.

### Tests and fixtures

- tests/VideoGrabber.Core.Tests/Editing — project and compatibility tests.
- tests/VideoGrabber.Infrastructure.Tests/Editing — FFmpeg command and output tests.
- tests/VideoGrabber.App.Tests/Editing — view-model tests.
- tests/fixtures/editor — tiny owned compatible and incompatible sources.
- tests/smoke/Invoke-EditorSmoke.ps1 — packaged editor acceptance.

---

### Task 1: Define editor domain and immutable project behavior

**Files:**
- Create: src/VideoGrabber.Core/Editing/EditModels.cs
- Create: src/VideoGrabber.Core/Editing/EditProject.cs
- Create: src/VideoGrabber.Core/Editing/IEditPlanner.cs
- Create: src/VideoGrabber.Core/Editing/IEditExecutor.cs
- Test: tests/VideoGrabber.Core.Tests/Editing/EditProjectTests.cs

**Interfaces:**
- Consumes: existing media path and safe output-path policies.
- Produces: EditSource, TimeRange, EditOperation, ExportMode, ExportProfile, EditPlan, EditProgress, EditResult and EditProject methods.

- [ ] **Step 1: Write failing project tests**

Cover positive duration, start below zero, end past duration, zero/negative ranges, source/output equality, output outside selected folder, overlapping split segments, stable concat order and dirty-state transitions.

~~~csharp
[Fact]
public void SetTrim_rejects_empty_range()
{
    var project = EditProject.Create(source, TimeSpan.FromSeconds(10));
    var result = project.TrySetTrim(new TimeRange(
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
    Assert.False(result.IsValid);
}

[Fact]
public void Export_path_must_not_equal_source()
{
    var project = EditProject.Create(source, TimeSpan.FromSeconds(10));
    Assert.False(project.ValidateOutput(source).IsValid);
}
~~~

- [ ] **Step 2: Run tests and confirm failure**

~~~powershell
dotnet test .\tests\VideoGrabber.Core.Tests -c Debug --filter EditProjectTests
~~~

- [ ] **Step 3: Implement exact contracts**

~~~csharp
public readonly record struct TimeRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
}

public enum ExportMode { FastCopy, FrameAccurate }
public enum EditOperationKind { Trim, Split, Concat, ExtractAudio }
public sealed record EditSource(string Path, MediaProbe Probe);
public sealed record ExportProfile(string Container, string VideoCodec,
    string AudioCodec, int? Crf, string? HardwareEncoder);
public sealed record EditPlan(EditOperationKind Operation, ExportMode Mode,
    IReadOnlyList<EditSource> Sources, IReadOnlyList<TimeRange> Ranges,
    string OutputPath, ExportProfile Profile, bool RequiresConfirmation);
public sealed record EditPlanResult(bool IsValid, EditPlan? Plan,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
public sealed record EditProgress(double? Percent, TimeSpan? Processed,
    double? FramesPerSecond, TimeSpan? Eta);
public sealed record EditResult(bool IsSuccess,
    IReadOnlyList<string> OutputPaths, IReadOnlyList<string> FailedPaths,
    string? SafeMessage);

public interface IEditPlanner
{
    Task<EditPlanResult> CreatePlanAsync(EditProject project,
        ExportRequest request, CancellationToken cancellationToken);
}

public interface IEditExecutor
{
    Task<EditResult> ExecuteAsync(EditPlan plan,
        IProgress<EditProgress> progress, CancellationToken cancellationToken);
}
~~~

- [ ] **Step 4: Implement EditProject**

Use immutable collections and methods returning validation results instead of mutating on invalid input. Normalize paths with Path.GetFullPath. Keep only references and edit decisions; never copy source media into project storage.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Core.Tests -c Debug --filter EditProjectTests
git add src/VideoGrabber.Core/Editing tests/VideoGrabber.Core.Tests/Editing
git commit -m "feat: define editor project model"
~~~

---

### Task 2: Probe sources and report concatenation compatibility

**Files:**
- Create: src/VideoGrabber.Core/Editing/ConcatCompatibility.cs
- Create: src/VideoGrabber.Infrastructure/Editing/EditPlanner.cs
- Test: tests/VideoGrabber.Core.Tests/Editing/ConcatCompatibilityTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/EditPlannerTests.cs
- Create: tests/fixtures/editor/Generate-Fixtures.ps1
- Create: tests/fixtures/editor/README.md

**Interfaces:**
- Consumes: FfprobeClient.ProbeAsync and MediaProbe.
- Produces: ConcatCompatibility.Compare(IReadOnlyList<MediaProbe>) and EditPlanner.CreatePlanAsync.

- [ ] **Step 1: Generate deterministic owned fixtures**

Generate five-second files: h264-aac-320x180-25.mp4, matching-second.mp4, h264-aac-640x360-30.mp4, vp9-opus-320x180-25.webm, and audio-only.m4a. Commands use lavfi testsrc2 and sine; write exact commands and hashes to README.md.

- [ ] **Step 2: Write failing compatibility tests**

Matching video/audio codecs, pixel format, resolution, time base, frame rate, sample rate, channel layout and container compatibility yields CanStreamCopy=true. Any mismatch lists exact fields and yields RequiresNormalization=true.

~~~csharp
var result = ConcatCompatibility.Compare([firstProbe, secondProbe]);
Assert.True(result.CanStreamCopy);
Assert.Empty(result.Mismatches);

var mismatch = ConcatCompatibility.Compare([firstProbe, differentResolution]);
Assert.False(mismatch.CanStreamCopy);
Assert.Contains(mismatch.Mismatches, x => x.Field == "Resolution");
~~~

- [ ] **Step 3: Implement compatibility comparison**

Compare normalized rational numbers, not display strings. Missing probe values are Unknown and prevent stream-copy concat. Return user-facing Russian labels separately from invariant field ids.

- [ ] **Step 4: Implement EditPlanner**

FastCopy trim produces a plan only when source streams can be copied into the chosen container. FrameAccurate trim selects a verified encoder. Concat selects FastCopy when compatibility passes; otherwise returns a normalization plan with RequiresConfirmation=true.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug --filter "ConcatCompatibilityTests|EditPlannerTests"
git add src/VideoGrabber.Core/Editing src/VideoGrabber.Infrastructure/Editing tests/fixtures/editor tests
git commit -m "feat: plan compatible media edits"
~~~

---

### Task 3: Implement fast lossless trim and audio extraction

**Files:**
- Create: src/VideoGrabber.Infrastructure/Editing/FfmpegEditExecutor.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/FastTrimTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/AudioExtractionTests.cs

**Interfaces:**
- Consumes: IProcessRunner, FfprobeClient, MediaValidator and EditPlan.
- Produces: FfmpegEditExecutor.ExecuteAsync for FastCopy Trim and ExtractAudio.

- [ ] **Step 1: Write failing integration tests**

Trim seconds 1–4 from the five-second fixture, assert source hash unchanged, output duration within one GOP tolerance reported by the planner, streams copied, output path differs, and validation passes. Extract M4A and assert no video stream plus a decodable audio stream.

- [ ] **Step 2: Implement safe fast trim**

Build arguments equivalent to:

~~~text
-ss <invariant-start>
-i <source>
-t <invariant-duration>
-map 0:v?
-map 0:a?
-map_metadata 0
-c copy
-avoid_negative_ts make_zero
-n
<temporary-output>
~~~

Use the temporary output convention from MediaValidator. Never accept raw FFmpeg arguments from UI text.

- [ ] **Step 3: Implement audio extraction**

For M4A copy AAC when compatible; for MP3 use libmp3lame with explicit q:a 2; for Opus use libopus with 160k default. The UI request maps to an enum, never an arbitrary codec string.

- [ ] **Step 4: Add progress and cancellation**

Use FFmpeg -progress pipe:1 -nostats. Convert out_time_ms into a bounded 0–100 percent based on plan duration. Cancellation kills the tree and retains the temporary output with a .cancelled suffix for explicit cleanup.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "FastTrimTests|AudioExtractionTests"
git add src/VideoGrabber.Infrastructure/Editing tests/VideoGrabber.Infrastructure.Tests/Editing
git commit -m "feat: add lossless trim and audio extraction"
~~~

---

### Task 4: Implement frame-accurate trim with verified encoders

**Files:**
- Create: src/VideoGrabber.Infrastructure/Editing/EncoderCatalog.cs
- Modify: src/VideoGrabber.Infrastructure/Editing/FfmpegEditExecutor.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/EncoderCatalogTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/FrameAccurateTrimTests.cs

**Interfaces:**
- Consumes: IProcessRunner, MediaProbe and FrameAccurate EditPlan.
- Produces: EncoderCatalog.DetectAsync and frame-accurate executor path.

- [ ] **Step 1: Write encoder detection tests**

Parse ffmpeg -encoders, but mark hardware encoders usable only after a one-second encode/decode self-test. Preference order is h264_nvenc, h264_qsv, h264_amf, then libx264. Cache results for the current FFmpeg hash.

- [ ] **Step 2: Implement EncoderCatalog**

Return EncoderCapability with Name, IsHardware, IsAvailable, FailureReason and TestedFfmpegHash. Never infer hardware support from the encoder list alone.

- [ ] **Step 3: Write frame-accurate trim test**

Trim from 1.240 to 3.760 seconds using libx264 test profile. Require output duration within 50 ms, first decoded frame near requested position, source hash unchanged, audio present and validation successful.

- [ ] **Step 4: Implement frame-accurate FFmpeg arguments**

Seek after input for accuracy, map optional video/audio, encode H.264 with selected hardware profile or libx264 -preset medium -crf 18, encode AAC at 192k, copy metadata, add +faststart for MP4, and write only to a temporary destination.

- [ ] **Step 5: Handle hardware fallback**

If a verified hardware encoder fails before producing decodable output, delete no files, retain its safe log, retry once with libx264, and label the result Software fallback. Never silently reduce resolution or frame rate.

- [ ] **Step 6: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "EncoderCatalogTests|FrameAccurateTrimTests"
git add src/VideoGrabber.Infrastructure/Editing tests/VideoGrabber.Infrastructure.Tests/Editing
git commit -m "feat: add frame accurate trimming"
~~~

---

### Task 5: Implement splitting and concatenation

**Files:**
- Modify: src/VideoGrabber.Infrastructure/Editing/FfmpegEditExecutor.cs
- Create: src/VideoGrabber.Infrastructure/Editing/ConcatListWriter.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/SplitTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/ConcatTests.cs

**Interfaces:**
- Consumes: validated TimeRange list and compatibility result.
- Produces: Split and Concat execution paths.

- [ ] **Step 1: Write split tests**

Split a five-second fixture into 0–2 and 2–5 seconds. Require two uniquely named outputs, unchanged source hash, no overwrites, valid duration tolerance per selected mode, and validation of both outputs before either is reported Completed.

- [ ] **Step 2: Implement split as independent planned outputs**

Execute one range at a time. If range two fails, range one remains a valid completed artifact and the overall result reports PartialFailure with exact successful and failed paths. Restart skips already validated outputs only after revalidation.

- [ ] **Step 3: Write concat tests**

Test compatible MP4 stream-copy concat, path containing a single quote, duplicate source entries, incompatible-resolution refusal without confirmation, and confirmed normalization to 1920×1080 or source-first resolution according to the selected profile.

- [ ] **Step 4: Implement safe concat list generation**

Create the list in the job temp directory with UTF-8 no BOM. Escape FFmpeg concat-demuxer paths according to its file syntax, set -safe 1 when all sources share an allowed root and otherwise use an internally generated normalized intermediate list. Delete the list only after final validation.

- [ ] **Step 5: Implement normalization concat**

For each source, scale with force_original_aspect_ratio=decrease, pad to the selected even dimensions, set square pixels, chosen fps, yuv420p, AAC 48 kHz stereo, then concatenate normalized intermediates. Display the exact output profile before execution.

- [ ] **Step 6: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "SplitTests|ConcatTests"
git add src/VideoGrabber.Infrastructure/Editing tests/VideoGrabber.Infrastructure.Tests/Editing
git commit -m "feat: split and concatenate media"
~~~

---

### Task 6: Add timeline thumbnails, preview state, and project autosave

**Files:**
- Create: src/VideoGrabber.Infrastructure/Editing/TimelineThumbnailService.cs
- Create: src/VideoGrabber.Infrastructure/Editing/EditProjectStore.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/TimelineThumbnailServiceTests.cs
- Test: tests/VideoGrabber.Infrastructure.Tests/Editing/EditProjectStoreTests.cs

**Interfaces:**
- Consumes: FfprobeClient, IProcessRunner and EditProject.
- Produces: GenerateAsync(source, width, count, cacheDirectory), SaveAsync and RestoreAsync.

- [ ] **Step 1: Write thumbnail tests**

For a five-second fixture request six thumbnails at 160 px width. Assert bounded count, ordered timestamps, valid JPEG files, cache key includes source full path, size, last-write time and FFmpeg hash, and regeneration after source change.

- [ ] **Step 2: Implement bounded thumbnail generation**

Choose evenly spaced timestamps excluding the exact final frame. Limit count to 40, width to 320 px, concurrent FFmpeg processes to two, and cache size to 250 MB with least-recently-used eviction. Do not decode the entire source for each image.

- [ ] **Step 3: Write autosave tests**

Persist only source paths, probe fingerprints, ranges, order, mode, profile and output folder. Verify no media bytes, cookie, URL query token or technical logs are stored. Missing sources restore as Missing without removing project data.

- [ ] **Step 4: Implement EditProjectStore**

Save JSON atomically through a sibling .tmp file and File.Move overwrite only after successful serialization. Retain one .previous backup. Store under data\editor\projects and sanitize project filenames.

- [ ] **Step 5: Run tests and commit**

~~~powershell
dotnet test .\tests\VideoGrabber.Infrastructure.Tests -c Debug --filter "TimelineThumbnailServiceTests|EditProjectStoreTests"
git add src/VideoGrabber.Infrastructure/Editing tests/VideoGrabber.Infrastructure.Tests/Editing
git commit -m "feat: persist editor timeline state"
~~~

---

### Task 7: Build the intuitive editor UI

**Files:**
- Create: src/VideoGrabber.App/Editing/EditorPage.xaml
- Create: src/VideoGrabber.App/Editing/EditorPage.xaml.cs
- Create: src/VideoGrabber.App/Editing/EditorViewModel.cs
- Create: src/VideoGrabber.App/Editing/TimelineView.xaml
- Create: src/VideoGrabber.App/Editing/TimelineView.xaml.cs
- Create: src/VideoGrabber.App/Editing/ConcatQueueView.xaml
- Create: src/VideoGrabber.App/Editing/ConcatQueueView.xaml.cs
- Create: src/VideoGrabber.App/Editing/ExportDialog.xaml
- Create: src/VideoGrabber.App/Editing/ExportDialog.xaml.cs
- Modify: src/VideoGrabber.App/MainWindow.xaml
- Modify: src/VideoGrabber.App/Downloads/DownloaderPage.xaml
- Test: tests/VideoGrabber.App.Tests/Editing/EditorViewModelTests.cs

**Interfaces:**
- Consumes: IEditPlanner, IEditExecutor, TimelineThumbnailService, EditProjectStore and MediaProbe.
- Produces: OpenFileCommand, SetTrimCommand, AddSplitCommand, AddConcatFileCommand, MoveConcatItemCommand, ExportCommand and CancelExportCommand.

- [ ] **Step 1: Write view-model tests**

Cover opening file, missing/invalid media, marker clamping, start/end ordering, mode warning, incompatible concat confirmation, generated unique output name, export cancellation, partial split failure and opening a downloaded file from DownloaderPage.

- [ ] **Step 2: Implement EditorViewModel**

Do not expose raw FFmpeg options. Provide enum-backed mode/container/audio choices, formatted duration/current position, validation message, CanExport and progress. Generate destination as <source-name>-edited-<yyyyMMdd-HHmmss>.<ext> in the selected output folder and re-check uniqueness immediately before execution.

- [ ] **Step 3: Build the page layout**

Top: MediaPlayerElement preview with play/pause and current/total time. Middle: TimelineView with thumbnails, playhead, draggable start/end handles and keyboard-adjustable time fields. Bottom: operation selector, mode summary, destination and primary Export button. Concat mode replaces the timeline with an ordered drag/drop list.

- [ ] **Step 4: Connect preview safely**

MediaPlayerElement opens only local full paths already accepted by the editor. Unsupported preview codecs show Предпросмотр недоступен, но FFmpeg может обработать файл and retain editing controls based on FFprobe. Closing the project releases MediaSource handles before any file operation.

- [ ] **Step 5: Add accessibility and responsive behavior**

Use AutomationProperties, labeled sliders, keyboard steps of one second and Shift+Arrow steps of one frame when fps is known, visible focus and high contrast. At widths below 760 px place controls below preview; never hide the current export warning.

- [ ] **Step 6: Enable cross-navigation**

Downloader action Открыть в редакторе opens EditorPage only for a validated completed path. Editor back-navigation preserves project state. The previously disabled navigation item becomes enabled.

- [ ] **Step 7: Run tests and manual UI smoke**

~~~powershell
dotnet test .\VideoGrabber.slnx -c Debug
dotnet run --project .\src\VideoGrabber.App\VideoGrabber.App.csproj
~~~

Verify keyboard-only operation, 100–200% scaling, light/dark/high-contrast themes, missing preview codec state, and no source overwrite.

- [ ] **Step 8: Commit**

~~~powershell
git add src/VideoGrabber.App/Editing src/VideoGrabber.App/MainWindow.xaml src/VideoGrabber.App/Downloads tests/VideoGrabber.App.Tests/Editing
git commit -m "feat: add intuitive video editor"
~~~

---

### Task 8: Verify the complete EXE with editor

**Files:**
- Create: tests/smoke/Invoke-EditorSmoke.ps1
- Modify: scripts/Build-Portable.ps1
- Modify: docs/USER-GUIDE.md
- Modify: docs/THIRD-PARTY-NOTICES.md

**Interfaces:**
- Consumes: complete downloader and editor.
- Produces: final artifacts/VideoGrabber-win-x64/VideoGrabber.exe and editor verification report.

- [ ] **Step 1: Extend portable assertions**

Build-Portable.ps1 must include editor resources and verify the Redactor, runtime hashes, absence of user data, no console subsystem, and successful launch on build 19044.

- [ ] **Step 2: Implement the editor smoke script**

Run fast trim, frame-accurate trim, two-part split, compatible concat, confirmed normalized concat and audio extraction on owned fixtures through application services. Hash all sources before and after. Probe and decode every output.

- [ ] **Step 3: Run complete verification**

~~~powershell
dotnet restore --locked-mode
dotnet build .\VideoGrabber.slnx -c Release --no-restore
dotnet test .\VideoGrabber.slnx -c Release --no-build
pwsh -NoProfile -File .\scripts\Build-Portable.ps1
pwsh -NoProfile -File .\tests\smoke\Invoke-DownloaderSmoke.ps1 -ArtifactPath .\artifacts\VideoGrabber-win-x64
pwsh -NoProfile -File .\tests\smoke\Invoke-EditorSmoke.ps1 -ArtifactPath .\artifacts\VideoGrabber-win-x64
~~~

Expected: every automated test passes; all source hashes remain unchanged; every output passes FFprobe and decode checks; no output is silently overwritten; VideoGrabber.exe launches without a console.

- [ ] **Step 4: Perform manual acceptance**

On the target PC, open a downloaded video in the editor, move trim handles, compare preview positions, export in both modes, reorder two concat items, cancel one export, restart the app and restore the project. Confirm Russian messages are understandable and the main path requires no technical FFmpeg knowledge.

- [ ] **Step 5: Audit final artifact**

Verify the portable folder contains no source media, editor project history, browser profile, cookies, logs, personal absolute paths, test output or recovery files. Confirm FFmpeg license notice and exact binary hash remain documented.

- [ ] **Step 6: Commit**

~~~powershell
git add tests/smoke scripts docs
git commit -m "build: verify complete VideoGrabber editor"
~~~

The complete VideoGrabber milestone is finished only when the downloader milestone remains green and all editor verification steps pass on Windows build 19044.
