# VideoGrabber GetCourse UX / Theme / Help Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make GetCourse master videos directly downloadable by chosen quality without manual playback, with safe filenames, batch download, built-in help, and persistent light/dark/system themes.

**Architecture:** Keep the existing browser/CDP/WebResource discovery and downloader boundaries. Add a preflight manifest resolver between UI selection and `DownloadRequest`, carry display/title metadata separately from signed URLs, keep child media playlists internal when they belong to a master, and add UI-only help/theme modules without touching third-party WebView content.

**Tech Stack:** C#/.NET 10, WinUI 3, WebView2, yt-dlp, FFmpeg/FFprobe, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-14-getcourse-ux-design.md`

## Global Constraints
- DRM/encrypted HLS remains fail-closed; no key extraction.
- No password/cookie/token/query leakage to logs or filenames.
- Preserve per-site Ethernet routing and system VPN behavior.
- Next package is `0.1.10-preview.10`; never overwrite stable/prior previews.
- No GitHub push/publish; no intermediate commits. One local commit only after live acceptance.
- Exclude `src/VideoGrabber.App/SmokeWindow.cs` and scratch artifacts from final commit.

---

### Task 1: Master HLS preflight and quality download
**Files:** create `Browser/HlsDownloadPreflight.cs`; modify `MediaDownloadPlanResolver.cs`, `MainWindow.Browser.cs`, `MainWindow.Download.cs`; tests in new `MasterHlsDownloadTests.cs` and browser wiring tests.
**Produces:** selected master + requested quality resolves to a verified clear child playlist without manual Play.
- [ ] Write failing tests for master 360/480/720 selection, child-manifest encryption rejection, and no-Play preflight.
- [ ] Run targeted tests and confirm RED.
- [ ] Implement preflight fetch using existing proxy/cookie/referer/user-agent session data; parse with `HlsManifestParser` and enforce `HlsDownloadPolicy`.
- [ ] Update browser click path so a master performs preflight before `DownloadSourceAsync`; hide known child media candidates from normal list.
- [ ] Add 360p/480p quality choices and keep existing quality caps.
- [ ] Run targeted tests to GREEN.

### Task 2: Reliable output capture and safe human filenames
**Files:** modify `DownloadRequest.cs`, `YtDlpDownloader.cs`; create `DownloadFileName.cs`; add `DownloadOutputPathTests.cs` and naming tests.
**Produces:** downloader reliably discovers the actual created file and uses a sanitized suggested title/ordinal/quality without query/JWT material.
- [ ] Write RED tests for generic HLS output where after_move path is absent/different, token-heavy title sanitization, and Windows filename constraints.
- [ ] Add `SuggestedBaseName` / ordinal metadata to `DownloadRequest` without putting it into URL/query handling.
- [ ] Use a deterministic safe output template; discover final file by yt-dlp print and bounded post-run directory delta fallback.
- [ ] Ensure FFprobe validation succeeds on the discovered file and false “file not found” is eliminated.
- [ ] Run targeted tests to GREEN.

### Task 3: Primary-video list and batch download
**Files:** create `Browser/MediaCandidatePresentation.cs`; modify `MainWindow.Browser.cs` and download orchestration; tests in `MediaCandidatePresentationTests.cs` and wiring regression tests.
**Produces:** master candidates are numbered, readable, primary; internal child streams are hidden; “Скачать все найденные” downloads primary videos sequentially.
- [ ] Write RED tests for master-vs-child presentation, stable ordering, duplicate suppression, and batch selection.
- [ ] Extract lesson/page heading metadata via safe WebView script returning text only; no credentials or HTML persistence.
- [ ] Generate display labels and suggested filenames from ordinal + part/topic/title metadata.
- [ ] Add `Скачать все найденные` and sequential cancellation-aware loop using current quality/auth mode.
- [ ] Run targeted tests to GREEN.
### Task 4: Information/help experience
**Files:** create `MainWindow.Info.cs`; modify shell/browser controls; tests in `InformationContentTests.cs` and wiring tests.
**Produces:** `ⓘ Информация` and `ⓘ Как скачать?` surfaces with version, creator, supported workflows, auth instructions, privacy and limitations.
- [ ] Write RED tests for required content strings/version/author/GetCourse steps and button wiring.
- [ ] Build a WinUI dialog/panel using app-local text; include version from assembly/VERSION source.
- [ ] Add contextual browser help explaining primary videos, quality, batch, auth and DRM limits.
- [ ] Run targeted tests to GREEN.

### Task 5: Persistent system/light/dark theme
**Files:** create `AppThemeSettings.cs` and `MainWindow.Theme.cs`; modify `MainWindow.xaml.cs`; tests in `ThemeSettingsTests.cs` and UI wiring tests.
**Produces:** system/light/dark selector persisted under LocalAppData, with contrast-safe app brushes; WebView page content is untouched.
- [ ] Write RED tests for settings parse/persist/default and presence of theme selector/wiring.
- [ ] Centralize theme palette instead of static light-only brushes; apply to root/sidebar/cards/text/borders/controls/progress/author card.
- [ ] Persist `system|light|dark`; apply system theme on startup and selector change.
- [ ] Run targeted tests to GREEN.

### Task 6: Release verification and live acceptance
**Files:** update VERSION, CHANGELOG, RELEASE_NOTES, `docs/MEDIA_WORKFLOWS.md` after feature verification.
- [ ] Run full Release build/tests with real FFmpeg/FFprobe/yt-dlp/Whisper; require 0 failed / 0 skipped.
- [ ] Run `git diff --check`, privacy/secret regression tests, and targeted stress tests for proxy/shutdown.
- [ ] Set `VERSION=0.1.10-preview.10`; build with `scripts/Build-Release.ps1`; install in a new separate folder and calculate SHA-256.
- [ ] Live GetCourse: download primary master at 360p and 720p without Play, verify human filename, FFprobe video+audio, full FFmpeg decode exit 0.
- [ ] Test batch start/cancel, information/help, light/dark/system theme persistence, and closing after active media without error.
- [ ] Only after live acceptance: final status/secret scan and one local commit excluding SmokeWindow/scratch. Do not push.
