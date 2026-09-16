# VideoGrabber desktop stabilization acceptance

Date: 2026-09-16. Validated production HEAD: `1eb3fe171487d09d0aa7c898563281189e7297c6` (`fix: validate stream copy editing contracts`).

This report records observed evidence only. `desktopReady` is **false** because required environmental/release gates remain BLOCKED. Platform auth/Telegram/payments remain outside this desktop plan.

## Final gate summary

| Gate | Status | Observed evidence | Limitation / next action |
|---|---|---|---|
| Synthetic media source integrity | PASS | 12/12 copied six-parts MP4 hashes match audit source; every file passed ffprobe and full FFmpeg decode. Evidence: Task 19 `six-parts-source-verification.json`. | None for the synthetic source set. |
| Automated regression | BLOCKED | Discovery: 604 cases. Sequential final run: Core 21/21 PASS; Infrastructure 582 PASS, 0 FAIL, 1 SKIP. All 604 discovered cases have an outcome. | `WhisperIntegrationTests.Real_whisper_generates_meaningful_text_and_timed_subtitles` is SKIP because whisper-cli/model/sample are not available. Strict gate requires zero skipped. |
| Release build / local publish | PASS | Release build 0 warnings / 0 errors; self-contained publish succeeded; 524 published files, 234,419,219 bytes. | Local publish is inspection output, not publication permission. |
| Diagnostics regression | PASS | `scripts/Test-Diagnostics.ps1` PASS, including daily/weekly/monthly, 512/513, single/aggregate byte caps, malformed/no-data/privacy/retention. | None. |
| Installer transaction regression | PASS | `scripts/Test-InstallComponents.ps1` PASS: trusted local Prepare, wrong digest, missing asset, exclusive-copy failure, unchanged pointer and no leaked failed generation. | None. |
| PowerShell AST / diff hygiene | PASS | All `scripts/*.ps1` parse with zero AST errors; `git diff --check` completed without content errors. | CRLF conversion warning is informational. |
| Tool provenance | BLOCKED | yt-dlp 2026.08.19 exact official EXE digest matches. FFmpeg/ffprobe exact executables match files extracted from official `autobuild-2026-09-06-13-06` GPL archive whose upstream SHA-256 matches. Deno 2.9.6 exact executable matches official release archive whose upstream SHA-256 matches. | Whisper executable/model provenance absent. GPL distribution/license/source-offer materials are not yet proven complete for a redistributable release package. |
| Full GUI acceptance | BLOCKED | Published EXE and unpacked EXE each remained alive after launch and accepted `CloseMainWindow`; version is `0.1.10-preview.12+1eb3fe1…`. | No supported visual/accessibility observation channel was available to prove 100/125/150%, focus/tab, theme readability, clipping and all interactive scenarios. |
| Private GetCourse acceptance | BLOCKED | Synthetic six-part scenarios cover binding/quality/queue/session contracts. | No explicitly authorized live lesson/session was supplied for this final gate. No credential/session copying was attempted. |
| Clean Windows acceptance | BLOCKED | Self-contained package launches on the development Windows host. | No verified clean supported Windows VM without SDK/development runtimes was available. |
| Package integrity | BLOCKED | Local ZIP round-trip is hash-identical: 527 staged/unpacked files; ZIP SHA-256 `af9cd4191a840b3834bd1fd3f9f3fcbdd23e53b81745edb0d9db959b6521a79a`; unpacked EXE starts and closes normally. | Author/contact rendering and clean-machine launch still require GUI/clean-Windows gates; distribution of tool payloads remains subject to provenance/license gate. |

## Tool provenance observed on the validation host

| Tool | Local version / SHA-256 | Upstream evidence | Result |
|---|---|---|---|
| yt-dlp | `2026.08.19`; `66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a` | Official immutable GitHub release `2026.08.19`; the published `yt-dlp.exe` digest is the same value; signed checksum assets are present. | Artifact match PASS. |
| FFmpeg | `N-126435-gf93cd72dde-20260906`; `5f0118f94a7e92d01a86ea07ed533dee9cb45789d135a4365e98cefec3ca8847` | Official BtbN tag `autobuild-2026-09-06-13-06`; GPL static ZIP checksum `cd75d5135fdd98f386043607179f4d5d471dee1707dc63509e292dff170586f3`; extracted official `ffmpeg.exe` hash equals local. | Artifact match PASS; redistribution materials still BLOCKED. |
| FFprobe | `N-126435-gf93cd72dde-20260906`; `c845a650062e6c774db15a1ab6393b945768097db405a6dc88ab73f344331520` | Same verified BtbN GPL archive; extracted official `ffprobe.exe` hash equals local. | Artifact match PASS; redistribution materials still BLOCKED. |
| Deno | `2.9.6`; `2ff9493dfa356be2975f477025ea770088e9e9cb2c83d983236d13561b96b7a6` | Official Deno release `v2.9.6`; Windows x86_64 ZIP upstream checksum `15e5300b0ba3c3695a7621d90160a746ec9e710228cee639afa9d580f6e3cd11`; extracted official `deno.exe` hash equals local. Release commit is GitHub-verified. | Artifact match PASS. |
| Whisper | Not available | No executable/model/sample supplied in the final validation environment. | BLOCKED. |

Upstream references used for provenance: `https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19`, `https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-09-06-13-06`, `https://github.com/denoland/deno/releases/tag/v2.9.6`. Downloaded archives and checksum files are external Task 19 evidence and are not committed.

## Audit finding disposition

`VERIFIED` below means the defect-specific implementation/regression evidence is present and the final available regression did not fail it. It does **not** override the BLOCKED release gates above.

| Audit ID | Owning task | Disposition | New evidence |
|---|---:|---|---|
| VG-AUD-001 | 4 | VERIFIED | Binding resolver tests plus final 604-case discovery/outcome set. |
| VG-AUD-002 | 5 | VERIFIED | Selection/merge regression tests plus final regression. |
| VG-AUD-003 | 7 | VERIFIED | Immutable quality data-flow / downloader tests plus final regression. |
| VG-AUD-004 | 10 | VERIFIED | Operation lifetime/coordinator cancellation regression. |
| VG-AUD-005 | 10 | VERIFIED | Repeated-click / operation ownership regression. |
| VG-AUD-006 | 10 | VERIFIED | Queue/close/cancel completion ordering regression. |
| VG-AUD-007 | 9 | VERIFIED | Browser page lease/lifetime tests plus final regression. |
| VG-AUD-008 | 8 | VERIFIED | VOD duration/end-list semantics tests. |
| VG-AUD-009 | 8 | VERIFIED | Live/window duration semantics tests. |
| VG-AUD-010 | 5 | VERIFIED | Candidate selection/merge behavior and six-part integration evidence. |
| VG-AUD-011 | 11 | VERIFIED | Queue navigation/session epoch and cookie-selection regression. |
| VG-AUD-012 | 9 | VERIFIED | Navigation/page lifetime cancellation regression. |
| VG-AUD-013 | 13 | VERIFIED | Component Prepare lifecycle cancellation and recovery tests. |
| VG-AUD-014 | 6 | VERIFIED | Selected HLS leaves fail closed; zero downloader call on unresolved selection. |
| VG-AUD-015 | 7 | VERIFIED | Per-video quality survives preparation and reaches final request. |
| VG-AUD-016 | 3 | VERIFIED | Redirect/cookie scope/provider regression. |
| VG-AUD-017 | 1 | VERIFIED | Job-owned workspace/output preservation regressions. |
| VG-AUD-018 | 1 | VERIFIED | Cancel/failure preserves foreign/pre-existing files. |
| VG-AUD-019 | 2 | VERIFIED | Output contract/media verification regression. |
| VG-AUD-020 | 14 | VERIFIED | Mandatory managed egress: private DNS/redirect/nested HLS/raw proxy/forged capability blocked; real local fixtures use explicit owned capability. |
| VG-AUD-021 | 12 | VERIFIED | Native suspended process + Job Object ownership; cancel/timeout/callback/root-exit descendant reaping regressions. |
| VG-AUD-022 | 15 | VERIFIED | SRT validator/promotion tests; final real-Whisper control itself remains BLOCKED because fixture files are absent. |
| VG-AUD-023 | 2 | VERIFIED | Download output contract and failed/cancelled promotion rules. |
| VG-AUD-024 | 18 | CONFIRMED + VERIFIED | Real FFmpeg characterization reproduced both defects; `1eb3fe1` adds stream metadata, trim bounds and stream-copy compatibility validation; focused 7/7 and final regression passed. |
| VG-AUD-025 | 16 | VERIFIED | Diagnostics omitted-file accounting and incomplete/no_data precedence tests. |
| VG-AUD-026 | 17 | VERIFIED | Verified component generations, pointer/journal transaction, crash/cancel rollback and same-window immutable service activation. |

## Final automated commands and evidence

- Discovery: `dotnet test VideoGrabber.slnx -c Release --no-restore --list-tests` → 604 discovered cases (`Task19/test-discovery.txt`).
- Final sequential solution run: `dotnet test VideoGrabber.slnx -c Release --no-restore -m:1 --logger trx` → Core 21 PASS; Infrastructure 582 PASS / 1 SKIP / 0 FAIL. The first parallel/shared-TRX attempt was discarded because its testhost aborted; the isolated Infrastructure rerun passed 582/1 and the sequential solution rerun completed successfully.
- Clean-HEAD confirmation after restoring two incidental timeout-only test edits: process-focused launcher/installer tests 9/9 PASS; sequential solution rerun again completed with Core 21 PASS and Infrastructure 582 PASS / 1 Whisper SKIP / 0 FAIL.
- Release build: `dotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release --no-restore` → 0 warnings / 0 errors.
- Self-contained local publish succeeded into external Task 19 evidence.
- `scripts/Test-Diagnostics.ps1` and `scripts/Test-InstallComponents.ps1` both PASS under Task 19 evidence roots.
- Package integrity: `VideoGrabber-desktop-validation-1eb3fe1-20260916.zip`, SHA-256 `af9cd4191a840b3834bd1fd3f9f3fcbdd23e53b81745edb0d9db959b6521a79a`; staged vs unpacked 527-file manifest is identical.
- Published and unpacked EXEs each started on this host, remained alive for the smoke interval and closed via `CloseMainWindow`.

## Readiness decision

- `desktopReady = false`
- `platformReady = false` (platform implementation is outside this desktop plan and remains outstanding)
- Desktop code stabilization Tasks 1–18 are implemented and regression-covered.
- Task 19 is documented with PASS/BLOCKED evidence; release approval remains blocked until zero-skip Whisper validation, full GUI observation, explicitly authorized private GetCourse acceptance, a clean supported Windows VM gate, and complete distribution/provenance materials are independently satisfied.
- No push, merge, public release, production deployment, credential copying or installed-tool replacement was performed by this validation.
