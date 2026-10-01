# Журнал подготовки релизов

## Three.js geometric hero after preview.60 - 2026-09-30

- Replaced the former custom WebGL enhancement with a standalone geometric Three.js hero while keeping the rest of the public site as ordinary HTML/CSS/JS.
- Scene geometry: PBR planet sphere, three torus orbits, six extruded rounded feature cards, central VG badge and subtle particle accents.
- Added self-hosted planet diffuse/bump/emissive maps and physically lit cold-blue materials; approved raster artwork remains the instant fallback.
- Pinned self-hosted Three.js `0.186.1` with MIT license; canonical `scripts/Build-Web3D.ps1` bundles the scene through esbuild `0.28.2`.
- Production bundle is tree-shaken (~596 KB raw / ~152 KB gzip estimate) and carries the SHA-256 of `hero-three.js`; regression verifies the bundle matches current source. Source hashing is canonical UTF-8/LF so Windows and Linux CI calculate the same marker.
- Three.js loads lazily after the approved hero art using idle scheduling, so the first visual is not blocked by the 3D runtime.
- Added ASP.NET Core Brotli/Gzip response compression for JS/CSS/JSON over HTTPS.
- Semantic hotspots remain real HTML links and update the scene accent/caption; rendering pauses offscreen and on hidden tabs.
- With reduced-motion enabled the Three.js runtime is not loaded at all and the approved static hero remains visible. Normal mobile keeps the geometric scene animated with a ~30 FPS cap and lower DPR instead of disabling 3D entirely.
- Strengthened visual depth after live review: faster planet/orbit motion, physically lit thicker orbit tubes, varied card Z-depth, hover pop-out, stronger pointer camera parallax, subtle scroll camera move, extra back-rim lighting and deterministic depth stars.
- After Three.js is ready, the approved raster fallback fades to 3.5% opacity so the geometric scene is visually unmistakable while still preserving continuity.
- Obvious-3D browser measurement: temporal motion changes roughly half the canvas, pointer parallax ~80% and focused-card pop/emissive ~78%; mobile remains animated and overflow-free.
- Targeted regression 14/14 PASS, source/bundle syntax PASS, Three.js Edge smoke PASS, desktop/mobile/reduced-motion browser smoke PASS with zero severe console errors.

## [0.1.10-preview.60-rc.1] - 2026-09-29

- Status: owner-admin exclusivity and user-facing terminology cleanup.
- Owner admin is reconciled against one configured verified authentication subject. That account is promoted to `owner_admin`; any other `owner_admin` account is demoted to its normal guest/user role with an audit event.
- Windows Information/Settings/Course UI now describes capabilities instead of exposing internal downloader/transcoder/transcription/runtime names or source links.
- Web account billing no longer exposes the deployment environment (`stage`) or payment-provider branding/error details. Backend provider identifiers remain internal only.
- Targeted API/app builds and UI regression suites pass with zero errors.
- Added a PostgreSQL integration test that starts with two owner-admin accounts and verifies reconciliation leaves exactly one configured owner.
- All preview.52–59 fixes remain included.

## [0.1.10-preview.59-rc.1] - 2026-09-29

- Status: Windows installer upgrade/Unicode hotfix.
- NSIS source is now UTF-8 with BOM, fixing corrupted Russian text in the installer UI.
- Before copying files the installer force-closes both current `VideoGrabber.Managed.exe` and legacy `VideoGrabber.exe`, waits for DLL handles to release, then performs the upgrade. The same guard is used by uninstall.
- Live failure root cause: preview.58 attempted to overwrite `C:\\Program Files\\VideoGrabber\\CoreMessagingXP.dll` while preview.57 was still running from that directory.
- Deployment regression tests enforce both the UTF-8 BOM and process-close-before-copy ordering.
- Added `scripts/Build-WindowsInstaller.ps1` as the canonical Setup builder; it forces `/INPUTCHARSET UTF8` and `/WX` and validates BOM, Managed input, PE output, ProductVersion and SHA-256.
- All preview.52–58 fixes remain included.

## [0.1.10-preview.58-rc.1] - 2026-09-29

- Status: release-pipeline anti-rollback hardening on top of preview.57.
- `deploy/platform/update_windows_downloads.sh` now defaults to the repository `VERSION` instead of a hardcoded preview, preventing an omitted CLI argument from republishing an older Windows build.
- Deployment tests lock this behavior and reject a return of the old preview.56 default.
- `Build-Release.ps1` supports explicit `-SkipRestore` for controlled builds from already-restored locked assets; normal release behavior still performs locked restore.
- Live OLEG diagnostics already confirm pause/settings-change/restart/cancel behavior, and the current course tree contains zero `.vg-asr-*`, `.vg-course-transcript-*`, or unfinished downloader temp files.
- All preview.52–57 fixes remain included.

## [0.1.10-preview.57-rc.1] - 2026-09-29

- Status: final live-validation hotfix after preview.56.
- Fixes stale course busy state after `Cancel all`: queue/current state is cleared immediately and controls unlock without restarting the application.
- Live OLEG validation confirmed pause -> Base-to-Small change -> current Whisper cancellation -> workspace cleanup -> restart with Small; test data was restored afterward and Base was restored as the preferred profile.
- All preview.56 website, installer, TOTP admin, TXT-only ASR, hardened retry and recursive cache-cleanup changes remain included.

## [0.1.10-preview.56-rc.1] - 2026-09-29

- Status: final VideoGrabber 10 consolidation build from merged main.
- Includes the final stale-ASR sweep on course resume in addition to explicit cache cleanup and per-file workspace cleanup.
- Includes the public feature-oriented website, real NSIS installer endpoint, owner TOTP 2FA admin console, paused/stopped transcription settings restart, TXT-only background course transcription and hardened Whisper retry.
- Release is intentionally new rather than overwriting preview.55, because preview.55 packaging was cut before the last stale-workspace sweep reached the merged main branch.

## [0.1.10-preview.55-rc.1] - 2026-09-28

- Status: Whisper workspace cleanup completion.
- Explicit course cache cleanup now removes owned `.vg-asr-*` directories and `.vg-course-transcript-*` temporaries recursively.
- Course background transcription also removes its owned ASR workspace after each file result so failed/cancelled attempts do not accumulate hidden folders.
- Ready media/documents remain protected; legacy `.vg-job-*` containing possible completed media are preserved unless empty/temporary-only.

## [0.1.10-preview.54-rc.1] - 2026-09-28

- Status: public-site/installer/admin-MFA/course-settings completion release.
- Public Web now presents product capabilities rather than three unexplained transcription-model cards, and `/download/windows` is wired to a real NSIS setup executable while portable ZIP remains separate.
- Owner admin console is linked only for `owner_admin`, requires TOTP 2FA for privileged changes, and supports manual grants, account controls, audit and per-feature overrides.
- Admin TOTP secrets use a dedicated 32-byte encryption key secret (`VG_ADMIN_MFA_ENCRYPTION_KEY`).
- Paused course transcription unlocks model/language settings; a changed setting cancels and discards the unfinished ASR work and requeues that video for a clean restart. A stopped/cancelled project also unlocks settings immediately without closing VideoGrabber.
- preview.53 TXT-only background course transcription and preview.52 hardened retry remain intact.
- Targeted desktop/platform regression suites pass and Local/Managed/API builds complete with zero warnings/errors.

## [0.1.10-preview.53-rc.1] - 2026-09-28

- Status: course background transcription is now TXT-only by design.
- The course worker no longer requests or validates SRT, so a valid transcript is not discarded because whisper.cpp emitted out-of-order subtitle timestamps (`cue-order`).
- Manual/local `text + SRT` remains unchanged and still requires a valid SRT.
- Text-quality validation and the hardened retry from preview.52 remain active.

## [0.1.10-preview.52-rc.1] - 2026-09-28

- Status: hardened second-pass recovery for background course transcription.
- Retry 2/2 now changes decoder/VAD behavior instead of repeating the same deterministic Whisper invocation: decoder context is reset, temperature fallback is disabled, and Silero VAD uses tighter splitting/padding.
- Transcript/SRT quality gates remain strict; repeated hallucination loops and invalid timestamp order are still rejected rather than promoted as finished TXT.
- Live reproduction on two preserved failed course audios recovered both a repetition-loop case and a cue-order case. Local/Managed builds complete with 0 warnings/errors; targeted Whisper promotion tests pass 6/6.

## [0.1.10-preview.51-rc.1] - 2026-09-27

- Status: Whisper model cache/dedup UX finalization.
- Additional models use one canonical cache file per profile and are reused across selections and application updates. A normal re-selection of an already verified model performs no network transfer and creates no duplicate file.
- The UI explicitly states that optional models download once from a public source, remain cached locally, and exposes a live `Model source` link for users who want to inspect provenance.
- Live re-selection of verified Small produced no `.download`, no timestamp change and no additional `transcription.model` log entry.

## [0.1.10-preview.50-rc.1] - 2026-09-27

- Status: verified Whisper model downloader Windows promotion fix.
- The first real Small-model selection reached the end of network transfer but Windows rejected atomic promotion because the `.download` FileStream was still open with `FileShare.None`.
- The download/read/hash streams are now disposed before size/SHA verification and `File.Move`; failed/cancelled partials are still cleaned and never replace a valid model.
- Targeted model-catalog/course-wiring tests: 6/6 PASS; Local build 0 warnings/errors.

## [0.1.10-preview.49-rc.1] - 2026-09-27

- Status: selectable Whisper model profiles with verified on-demand download.
- The whisper.cpp engine remains unchanged. Users choose a friendly profile: bundled Base, balanced Small Q5, or quality-oriented Medium Q5.
- Non-bundled models download automatically from the pinned public model source only when selected, are cached under LocalAppData across application updates, and are promoted atomically only after exact byte-size and SHA-256 verification.
- UI explains relative quality, speed and disk usage and exposes an optional live `Model source` link without showing raw infrastructure URLs in the normal workflow.
- Model selection is shared by local/manual transcription and full-course background transcription and is locked while an operation is active.

## [0.1.10-preview.48-rc.1] - 2026-09-27

- Status: transcription pause observability / active-time hotfix.
- Course transcription elapsed time is now backed by a pause-aware Stopwatch instead of wall-clock `UtcNow - started`, so suspended hours are excluded from the visible elapsed value.
- Global pause/resume stops/starts the active transcription stopwatch and writes `operation.pause` diagnostics with ordinal/total/queue state only.
- Live incident diagnosis confirmed the 4th course video was paused, not hung: all Whisper threads were suspended, CPU was flat, and the UI button read `Continue`; resuming restored full CPU activity immediately.

## [0.1.10-preview.47-rc.1] - 2026-09-27

- Status: Russian-course language guard + public download-surface cleanup.
- When course transcription language is configured as `auto`, Cyrillic course/path evidence selects `ru` explicitly; non-Cyrillic courses keep normal auto detection.
- Removed the public Windows checksum/SHA button and related user-facing checksum instructions. The checksum endpoint may remain operational internally, but it is no longer linked or mentioned in the public site.
- Removed GitHub/repository wording and raw links from packaged user-facing README/third-party notices while retaining component names and license information.
- Regression protects public Web from checksum-link/GitHub leakage and protects course transcription from losing the Russian-language heuristic.

## [0.1.10-preview.46-rc.1] - 2026-09-27

- Status: transcription quality / VAD / application-icon hotfix.
- Rebuilt `VideoGrabber.ico` from the canonical blue VideoGrabber PNG and embedded it through the existing ApplicationIcon/AppWindow paths.
- Bundled `ggml-silero-v6.2.0.bin` and enabled whisper.cpp VAD plus non-speech token suppression for local transcription.
- Added transcript text-quality validation: `[BLANK_AUDIO]`-dominated output and long duplicate-line loops are rejected instead of promoted beside course media.
- Existing same-name TXT files are revalidated on resume; invalid historical transcripts are queued again and overwritten only after a validated replacement succeeds.
- Real first-video sample: old auto mode produced mostly blank-audio markers; base+VAD auto produced normal Russian text with zero blank markers.
- Real second-video diagnosis: old SRT repeated one phrase ~300 times over ~6.5 minutes, while PCM analysis found zero duplicate one-second source-audio blocks. Base+VAD eliminated the loop completely on the same tail segment.
- Tested small-q5_1+VAD as well; quality was strong but ~2.6x slower on the real tail, so base+VAD was selected for the production course pipeline.

## [0.1.10-preview.45-rc.1] - 2026-09-27

- Status: Whisper SRT tail-validation hotfix.
- A real 02h01m52s course video produced a final Whisper cue ending only 1.655 seconds beyond FFprobe duration. The old +1s bound rejected an otherwise valid transcript and needlessly started attempt 2/2.
- SRT end-time tolerance is now 3 seconds. Larger overruns are still rejected as `duration-bound`.
- Added regression coverage for tolerated 2.5s tail and rejected 3.1s tail.

## [0.1.10-preview.44-rc.1] - 2026-09-27

- Status: course-transcription opt-in / live-progress / throughput follow-up.
- Whole-course transcription is now an explicit pre-run checkbox. New installs default to off; the selection is remembered, locked while the course is active, and actually gates whether Whisper is started/waited at completion.
- The UI explains that download and transcription overlap, but CPU load rises and the final completion moment can be delayed by hours on large courses.
- The active transcription item is now persisted independently of queue-enqueue UI updates, fixing the misleading 0/576 «stuck» appearance while Whisper is actually processing a long first video.
- Course transcription UI shows current ordinal, filename, retry attempt, live elapsed time and queue size, refreshed every second.
- Initial backfill scans the full course first and starts exactly one ordered Whisper worker afterwards, so queue construction cannot overwrite the active-item display.
- Whisper uses up to 6 CPU threads. On the target i5-12400F the real 30-second benchmark improved from 4.36s at 4 threads to 3.36s at 6 threads (~23% faster).
- Live diagnosis: the first queued video is 03h10m14s / 1.64 GB; whisper-cli remained CPU-active and had prepared a 365 MB WAV, so the prior UI symptom was presentation rather than a stalled process.

## [0.1.10-preview.43-rc.1] - 2026-09-27

- Status: automatic whole-course transcription pipeline.
- Every successfully downloaded course video is enqueued into one ordered background Whisper worker while the next video continues downloading.
- Successful course transcription leaves only `<video-basename>.txt` beside the media file. Whisper SRT is generated only for internal validation and removed after promotion of the validated text.
- The course UI has a second independent progress lane showing transcription progress, current media, queued count, completed texts and failures.
- Resume/backfill scans already downloaded course media and skips videos with an existing non-empty same-name TXT. A fully downloaded course can therefore complete local transcription without reopening an authenticated GetCourse session.
- Global pause affects both download and transcription child processes; global cancel cancels both. Completion actions wait for transcription drain and are suppressed when transcript failures remain.
- Local and Managed desktop editions share the same collapsible advanced-tools layout and course-transcription behavior.
- Qualification: Local/Managed Release builds 0 warnings/errors; Infrastructure 686 passed / 14 expected environment skips / 0 failed.

## [0.1.10-preview.42-rc.1] - 2026-09-27

- Status: final whole-course integrity follow-up.
- Course verification now treats any manifest/media count mismatch as incomplete, including the previously unprotected case where a valid downloaded video exists but an old manifest expected zero.
- Revisited lessons preserve existing valid media in the new expected-video count: `max(current candidates, saved HTML video evidence, ready media files)`.
- Live archive audit: 161/161 lessons present, zero lessons below expected media/assets; 576/576 media files passed ffprobe with zero invalid containers. One 30-second historical video was the only file present beyond the old 575-video manifest total and is now covered by the new rule.

## [0.1.10-preview.41-rc.1] - 2026-09-27

- Status: GetCourse/Kinescope course-integrity hotfix.
- Whole-course integrity now cross-checks saved GetCourse HTML for declared `o-lt-lesson-video` blocks. A stale lesson manifest with zero expected videos can no longer make a video lesson appear complete.
- Kinescope embed URLs are discovered directly from lesson DOM and handed to the normal yt-dlp browser-download pipeline, so a WebView iframe reset no longer prevents extraction when Kinescope itself remains downloadable.
- Partial discovery is fail-closed: when the page declares more video blocks than VideoGrabber resolved, the lesson stays incomplete and automatic course recovery revisits it.
- Live archive audit found eight affected lessons across modules 7–8 with 149 declared video blocks and no downloaded media; this exact false-completion pattern is now detected.
- Qualification: direct Kinescope embed probe PASS; targeted regression 13/13; full Infrastructure 686 passed / 14 expected environment skips / 0 failed; Local and Managed Release builds 0 warnings/errors.

## [0.1.10-preview.40-rc.1] - 2026-09-26

- Status: YouTube SABR fallback follow-up.
- YouTube analysis and server download now request `mweb,default` player clients. This preserves anonymous mweb + PO-token handling while allowing yt-dlp's current default/visionOS path when mweb is placed into YouTube's SABR-only experiment.
- Live probe through the production social-egress resolved real Shorts formats from 144p through 2160p and completed a 360x640 MP4 download with audio, without YouTube cookies.
- Instagram Reel, current TikTok and Pinterest video-pin anonymous probes remain successful.

## [0.1.10-preview.39-rc.1] - 2026-09-26

- Status: portrait/social quality correctness follow-up.
- Portrait media quality is orientation-aware: 360p/720p/1080p use yt-dlp `res:<N>` instead of literal video height.
- Storyboards and non-video formats are excluded from the quality list, preventing false 180p choices and review_required retries.
- Public Shorts, Instagram Reel, TikTok video and Pinterest video-pin anonymous extraction were rechecked through the production social path.
- Public product surfaces continue to expose no repository URL; Windows package and checksum stay on the VideoGrabber domain.

## [0.1.10-preview.38-rc.1] - 2026-09-25

- Status: unified public-social delivery for Web and Windows Managed.
- Public YouTube/Shorts, Instagram/Reels, TikTok and Pinterest links use the qualified server social pipeline without site cookies by default.
- Managed Windows downloads the verified server artifact into the selected local folder; explicit site-cookie selection keeps the local/browser-session fallback.
- Social operations no longer double-reserve Free quota in the Windows client.
- Public reverse proxy dynamically resolves the API Docker service to survive container recreation without 502.
- Public distribution remains on the VideoGrabber domain; the project's own repository URL is not exposed in the product/site/docs.

## [0.1.10-preview.37-rc.1] - 2026-09-25

- Status: social-video / Windows reservation correctness candidate.
- Public URL pipeline: YouTube and Shorts, Instagram/Reels, TikTok, Pinterest video pins.
- YouTube server path: Deno 2.9.6 + bgutil PO-token provider 2.0.0 + mweb player client + isolated WARP/wireproxy social egress for datacenter-IP bot challenges.
- Social egress is userspace-only and scoped to yt-dlp traffic; it does not replace the VPS host route or SSH path.
- Managed Windows dispatches authorized local UI work back through DispatcherQueue before touching WinUI controls.
- TikTok / Instagram / Pinterest path: yt-dlp browser impersonation backed by curl_cffi.
- Windows Managed: refreshes expired API session during reservation; failed/cancelled local work releases the reservation; successful local work commits the Free credit through a dedicated account/device-scoped endpoint.
- Support correction before this release: six preview.36 UI failures were released from reserved back to available, leaving the user Free grant at 10 available / 0 reserved.
- Distribution privacy: the public site exposes no repository URL; Windows ZIP and current SHA-256 are served directly by the VideoGrabber domain.
- Qualification: public YouTube Shorts, Instagram Reel, TikTok video and Pinterest video pin were all resolved through the anonymous social-video path; ledger DB tests 6/6, Worker 31/31, Core 32/32, Infrastructure 691 passed / 1 fixture skip.

## [0.1.10-preview.36-rc.1] - 2026-09-25

- Status: YouTube server-runtime / Free-reservation hotfix.
- Previous version: 0.1.10-preview.35-rc.1.
- API image now contains Python 3 and Node.js; SourceAnalysisService invokes yt-dlp with the Node JavaScript runtime.
- Worker image also contains Node.js and uses the same runtime for actual downloads.
- Source analysis reports stable user-facing reasons: source_unavailable, source_login_required, source_rate_limited, source_runtime_incomplete.
- Failed source analysis happens before job admission and does not reserve Free quota.
- Windows desktop-worker refreshes an expired managed session once before treating a device as revoked/offline.
- Support correction: one legacy review-required desktop reservation from the preview.35 migration window was released with an auditable ledger release; Free returned to 10 available / 0 reserved.

## [0.1.10-preview.35-rc.1] - 2026-09-25

- Status: direct web download / simplified desktop UX candidate.
- Previous version: 0.1.10-preview.34-rc.1.
- Web: ordinary video and paid MP3 can run on the server worker and download directly in the browser; Windows is optional unless the user explicitly selects Windows delivery.
- Browser delivery: completed server artifacts are exposed only through account-owned jobs and five-minute HMAC-scoped download tickets; large files stream normally instead of being buffered as a page Blob.
- Managed Windows: the main downloader stays compact; MP3, GetCourse, page-video discovery and transcription are collapsed under an explicit «Дополнительные возможности» section. Whole-course buttons remain visible after expansion and explain prerequisites/tariff state.
- Desktop worker: signed-in Windows clients accept explicitly-addressed jobs by default; a remembered opt-out remains available in Account.
- Branding: the actual VideoGrabber application icon is used by Web and Telegram Mini App.
- Diagnostics: the earlier OLEG offline state was caused by missing desktop-worker enrollment, not by the application process being closed.

## [0.1.10-preview.34-rc.1] - 2026-09-24

- Status: interactive tariff guidance / Windows download candidate.
- Previous version: 0.1.10-preview.33-rc.1.
- Web: pricing cards are clickable and open a plan detail dialog; MP3 / Full Course remain selectable and explain plan requirements rather than disappearing.
- Download: official /download/windows route points to the current GitHub Release package; the site also surfaces Windows 10/11 x64 requirements, release hashes and macOS development status.
- Managed Windows: tariff-gated actions stay clickable and open a contextual plan dialog; Pause and Cancel explain their use when no operation is running.
- Entitlement: top-level audio-only download now reserves premium_media, closing the remaining Free-to-MP3 presentation path.
- Information page: documents all four plans, common button behavior and direct tariff/payment navigation.
- Payment UX: Windows directs plan selection to the official website; server-side payment verification remains authoritative.

## [0.1.10-preview.33-rc.1] - 2026-09-24

- Status: unified account / subscription / Telegram Mini App synchronization candidate.
- Previous version: 0.1.10-preview.32-rc.1.
- Mini App: redesigned in the dark VideoGrabber style; account, downloads and subscription tabs share the same backend account/access state.
- Auth/session: media and payment modules wait for verified Telegram session creation; the previous startup race that surfaced HTTP 401 is removed.
- Free policy: one account receives 10 lifetime ordinary video downloads. MP3, editor/transcription and whole-course access are paid capabilities; enforcement is server-side and mirrored in Web, Windows and Telegram UI.
- Managed Windows: download controls require a restored authenticated session; whole-course download is routed through Managed authorization and requires Full Course.
- Verification: Core 32/32; Platform 328/328; Worker 31/31; Windows Infrastructure 677 passed / 14 environment integration skips / 0 failed; Managed Release build 0 warnings/errors.

## [0.1.10-preview.27] - 2026-09-19

- Status: full self-contained desktop runtime / built-in transcription candidate.
- Previous version: 0.1.10-preview.26.
- Distribution: full Local package includes `tools\yt-dlp.exe`, `tools\ffmpeg.exe`, `tools\ffprobe.exe`, `tools\deno.exe`, plus `tools\whisper\whisper-cli.exe`, required whisper/ggml CPU DLLs and multilingual `ggml-base.bin`. Runtime payload is about 539 MB before the app/framework files.
- Runtime ownership: a complete bundled `tools` directory is authoritative over stale AppData component generations. Normal download flow no longer launches `Install-Components.ps1`; if a bundled tool is missing, VideoGrabber reports an incomplete/corrupted distribution.
- UI: `Компоненты` is renamed to `Встроенные инструменты`. The old `Установить или обновить` button is removed from ordinary UI; the page shows the actual built-in tool paths and offers only a status check. Tool updates arrive with a new VideoGrabber build.
- Transcription: `Получить текст + SRT` uses the bundled Whisper CLI and model automatically. Manual EXE/model selectors are removed from normal UX; only speech language remains configurable. A real isolated bundle test produced both TXT and SRT from a real video fragment.
- Audio/text controls: the previously-created-but-not-rendered pause button is now displayed next to MP3/text actions. When another operation or a whole-course download is active, the section explicitly explains why its buttons are temporarily unavailable.
- Invalid input diagnostics: FFprobe failure now reports that the file is damaged or not a valid audio/video container. The user-selected `D:\Torrent\14+.avi` is 1,471,320,064 bytes but starts with zero-filled header bytes and FFprobe reports `Invalid data found when processing input`; this is a source-file problem independent of the UI.
- Editor verification: real FFmpeg integration test generated media, trimmed a fragment and joined two fragments successfully with the bundled runtime.
- Verification before packaging: focused bundled-runtime/media/UI suite 51/51; full pre-version gate Core 32/32, Infrastructure 678 passed / 0 failed / 1 environment-fixture skip, Worker 31/31; Local/Managed Release builds 0 warnings/errors. Real bundled Whisper and editor checks also passed outside the skipped fixture-specific test.
## [0.1.10-preview.26] - 2026-09-19

- Status: compact course manifests and pause-button UX follow-up.
- Previous version: 0.1.10-preview.25.
- Per-lesson verification files are now named `VG.lesson.json`.
- Backward compatibility: existing `VG.lesson.json` is still accepted; the next write migrates it to `VG.lesson.json`.
- Final cleanup: only after every planned lesson passes the final integrity audit, all lesson manifests are consolidated atomically into one `VG.verify.json` in the course root. The root index is re-read and validated before per-lesson manifests are deleted.
- Resume/recheck after completion continues to work from `VG.verify.json`, so removing per-lesson JSON files does not make a completed course look incomplete.
- Pause button: fixed 145 px width in both `⏸ Пауза` and `▶ Продолжить` states; active/running state is yellow with dark text, paused state is green with white text.
- Verification before final packaging: targeted manifest/pause/wiring suite 36/36; Local Release build 0 warnings/errors.
## [0.1.10-preview.25] - 2026-09-19

- Status: course integrity / pause / quality / archive portability candidate.
- Previous version: 0.1.10-preview.24.
- Image repair: malformed image names that looked like extensions are normalized from MIME/file signatures. Existing archive audit found 11 such files; all 11 were confirmed JPEG/JFIF and safely renamed to `.jpg` with no collisions. Seven existing lesson HTML files were augmented with local image references.
- Pause/resume: shared `⏸ Пауза` / green `▶ Продолжить` control is exposed in the main downloader, selected browser video area, whole-course area and audio/text area. Active owned child processes and logical course/archive loops are paused and resumed without treating pause as cancellation.
- Course quality: whole-course UI now offers 360p ceiling, 480p ceiling, 720p ceiling and best available. The selected tier persists in course state; HLS course downloads choose the best available track not exceeding the selected ceiling.
- Integrity manifests: every visited lesson writes `VG.lesson.json` from the live page with expected video/material counts and selected quality. A lesson with multiple videos is complete only when every `Видео 01…NN` file is present and non-empty.
- Pre-run reconciliation: every whole-course start checks the saved files against lesson manifests. Legacy lessons without manifests are intentionally revisited once to rebuild authoritative expectations instead of being trusted from stale HTML.
- Final verification: after an apparently complete pass the entire course is reconciled again. If anything is missing the UI reports that final verification found incomplete content and automatic downloading continues. `.vg-job-*` and course temporary files are purged only after the final verification confirms all lessons.
- Module 3 audit at packaging time: Day 1 currently lacks videos 03/16/21; Day 2 lacks 01/06/12/16. These seven are therefore not considered complete by the new numbered-video verification.
- GetCourse portability: standard course traversal is already host-agnostic; custom-domain GetCourse pages now also receive GetCourse/Kinescope CDN route inheritance when the source page is a real `/teach/control/` page. Provider/CDN hosts cannot become a root session, and ordinary routed sites remain isolated.
- Verification: targeted new-feature suite 67/67; route regression recheck 28/28; full gate Core 32/32, Infrastructure 660 passed / 0 failed / 14 existing live-tool skips, Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors before final packaging.

## [0.1.10-preview.24] - 2026-09-19

- Status: GetCourse CDN routing fix
- Previous version: 0.1.10-preview.23
- Root cause: GetCourse player/master hosts were routed through the selected Ethernet session, but HLS segments on `vh-*.servicecdn.ru` were not part of the session family and therefore used the Windows/VPN route. Live diagnostics on all seven currently missing videos showed system route timeout (HTTP 000) and direct Ethernet success (HTTP 206).
- Fixed: `servicecdn.ru` is now a GetCourse session family and an HLS provider family, so all its subdomains inherit the same route as the course.
- Portable routing: new `auto-physical` mode dynamically discovers physical Ethernet/Wi-Fi, excludes common VPN/Tunnel/WSL/virtual adapters, tries physical interfaces first and then the system route only as a last fallback.
- Portability: settings store `auto-physical`, not a machine IP. Existing unavailable adapter GUIDs are automatically converted to Auto on another computer. The displayed IPv4 address is only the current address of the adapter and is never the portable identity.
- Current missing media: Day 1 videos 03/16/21 and Day 2 videos 01/06/12/16. Their fresh 1080p first segments are reachable over Ethernet; automatic retry can now fetch them through the corrected session route.
- Verification: live auto-route probe succeeded through Ethernet with VPN adapters excluded; Core 32/32; Infrastructure 650 passed / 0 failed / 14 existing live-tool skips; Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors.

## [0.1.10-preview.23] - 2026-09-19

- Status: unattended auto-recovery candidate
- Previous version: 0.1.10-preview.22
- Root cause fixed: a transient selected-interface failure to api2.gcvh.ru bubbled as HttpRequestException and stopped the whole course at 79/161 (~49.1%). Whole-course mode now catches transient HttpRequestException/IOException/socket/timeout failures, persists state, waits with bounded exponential backoff and automatically resumes from the first incomplete lesson.
- Incomplete-pass retry: after a normal pass, any lesson/video still incomplete is automatically retried in later passes; completed lessons are skipped. This keeps the eight Module 3 fragment-1 failures in the queue without requiring the user to press Continue.
- Job folders: .vg-job workspaces are marked Hidden on Windows; zero-byte .part plus stale .ytdl metadata are pruned before a fresh retry; verified successful output still triggers normal workspace cleanup/removal.
- Permanent source errors: attachment HTTP 404/410 and other non-auth permanent 4xx responses create a small safe Недоступно marker instead of blocking the course forever. 401/403 remain authentication errors and are not silently auto-retried.
- Current evidence: the eight visible job folders contain only zero-byte .part files plus 50-71 byte .ytdl metadata, so deleting those current stale jobs loses no media data.
- Verification: targeted auto-recovery/resume tests 65/65; Core 32/32; Infrastructure 645 passed / 0 failed / 14 existing live-tool skips; Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors.

## [0.1.10-preview.22] - 2026-09-19

- Status: course-resume/layout hardening candidate
- Previous version: 0.1.10-preview.21
- Folder layout: root course materials are grouped under 00 - Общая информация; modules are numbered explicitly 01..08; nested lessons and training folders use one shared DOM-order sequence so duplicate 01/01 or 02/02 siblings are not created.
- Existing archive migrated in place with file-count/byte integrity checks; no media bytes were recopied.
- Resume safety: when a specific course URL is open, selecting a parent folder with a different VideoGrabber.course.json no longer starts the wrong project. Direct child states are searched for the matching canonical course URL; otherwise resume is refused with a clear message.
- UX: Open in embedded browser is dark green; the UI explicitly tells the user to scroll down after opening and to authenticate in the embedded browser before whole-course download/resume.
- Verification: targeted planner/state/UI tests 38/38; Core 32/32; Infrastructure 644 passed / 0 failed / 14 existing live-tool skips; Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors. DB-backed Platform tests remain environment-blocked because disposable PostgreSQL cannot bind a socket in this Windows session.

## [0.1.10-preview.21] - 2026-09-19

- Status: persistent crash-resume candidate
- Previous version: 0.1.10-preview.20
- Legacy reconcile: after the one-time structure scan, existing lesson folders are checked locally for non-empty DOCX/HTML, temp files, archived sign-player count versus ready media count, and saved attachment links versus ready attachments. Clearly completed lessons are immediately marked completed and skipped.
- Resume accuracy: the next index is the first locally incomplete lesson rather than lesson 1, so an existing large archive resumes near the actual interruption point without reopening every completed lesson.
- Video safety: fallback qualities use their own deterministic resume keys, preventing partial fragments from different HLS qualities from sharing one .part file.
- CDN resilience: the eight observed failed jobs are Module 3 / Test lessons Day 1 videos 02,03,16,21 and Day 2 videos 01,06,12,16. All failed with fragment 1 not found and contain only tiny .part/.ytdl metadata; they remain incomplete and are retried with a refreshed signed master on every Continue until successful.
- Current archive evidence: 75.24 GiB ready media, 148 media files, 58 DOCX, 9 PDF, 78 images, 57 HTML pages, 16 partial files in 9 job directories.
- Verification: crash-resume targeted tests 49/49; Core 32/32; Infrastructure 642 passed / 0 failed / 14 existing live-tool skips; Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors. DB-backed Platform tests remain environment-blocked because local PostgreSQL cannot bind a socket after this Windows reboot.

## [0.1.10-preview.20] - 2026-09-19

- Status: crash-resume candidate
- Previous version: 0.1.10-preview.19
- Persistent course project: VideoGrabber.course.json stores the validated course plan, completed lesson keys and next position; updates are atomic and survive app/Windows/power loss.
- Restart UX: Продолжить / открыть папку курса reloads the project from disk; older archives without state can be migrated by a one-time structure-only scan without re-downloading existing media.
- Partial resume: each course video has a deterministic resume key and stable .vg-job directory; yt-dlp runs with --continue so preserved .part/.ytdl files can resume after restart.
- Legacy recovery: existing random .vg-job folders are matched to the current lesson/video and migrated to the stable resume key; verified completed media are recovered with ffprobe.
- CDN resilience: all 8 observed historical video failures were fragment 1 not found / transient CDN failures, not DRM. Each video now gets up to four attempts, refreshes the signed GetCourse master, and for Best quality can fall back to the next available HLS height on later retries. Failed video keeps the lesson incomplete and is retried on future Continue runs.
- Existing archive observed after reboot: 75.24 GiB ready media, 148 media files, 58 DOCX, 9 PDF, 78 images, 57 HTML pages, 16 partial files in 9 job directories.
- Verification: Core 32/32; Infrastructure 642 passed / 0 failed / 14 existing live-tool skips; Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors. Platform DB-backed tests are blocked in this Windows session because the disposable local PostgreSQL cannot bind a listening socket after reboot.

## [0.1.10-preview.19] - 2026-09-19

- Status: live-fix candidate
- Previous version: 0.1.10-preview.18
- Attachments: closes HTTP input/output streams before moving .partial to the final PDF/DOCX/image path, fixing self-locking IOException.
- Course progress: adds a one-second elapsed timer from the initial course start, preserved across Continue; keeps overall percentage, module/lesson/action, ETA and current video progress.
- Diagnostics: manual WebView download attempts log only host/extension/fileservice flag; sensitive hashes and cookies are not logged.
- Real course evidence: the saved lesson DOM contains the provided b543...docx link plus additional DOCX/PDF links, so detection works; previous failure occurred during file promotion, not discovery.
- Verification: Core 32/32, Infrastructure 634 pass / 0 fail / 14 existing live-tool skips, Platform 286/286, Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors.

## [0.1.10-preview.18] - 2026-09-18

- Status: live-fix candidate
- Previous version: 0.1.10-preview.17
- Course UX: separate overall course progress bar, stage 1/2 structure scan, stage 2/2 lesson processing, module/lesson/current-file labels, N/total percentage and ETA.
- Resume/cancel: red global Отменить всё cancels course/current download/editor operations; Продолжить reuses the in-memory course plan and skips fully completed lessons.
- Cleanup/recovery: explicit temp cleanup removes only partial/ytdl/temp artifacts; verified finished media from stale .vg-job folders are recovered with ffprobe instead of re-downloaded.
- Download finalization: an untrusted yt-dlp filepath no longer discards a valid owned media output; only files discovered inside the owned job directory may be promoted.
- Documents: empty DOM spacing no longer creates empty Word paragraphs/pages; comments, answers and user-response UI are removed from the lesson archive.
- Assets: public fs-thb CDN assets use routed HTTP/1.1 without unnecessary cookies; protected same-origin files keep current WebView cookies; asset diagnostics record safe failure reasons.
- Verification: Core 32/32, Infrastructure 634 passed / 0 failed / 14 existing live-tool skips, Platform 286/286, Worker 31/31; Local/Managed/API/Worker Release builds 0 warnings/errors.

## [0.1.10-preview.17] - 2026-09-18

- Status: live-fix candidate
- Previous version: 0.1.10-preview.16
- Video: course mode reads signed GetCourse player URLs from lesson DOM, fetches sign-player via the selected adapter, accepts extensionless /api/playlist/master endpoints, verifies real #EXTM3U master manifests and then uses the existing HLS downloader.
- Attachments: direct RouteConnector transport replaces the failing nested SOCKS path; WebView cookies and Referer remain scoped per request.
- Archive: only real .lite-page lesson content is retained; comments, answers, CSS/style, editing controls and service UI are removed; lesson title comes from .lesson-title-value.
- Structure: root lessons move to 00 - Вводные материалы; module lesson-count/author metadata and Просмотрено status are removed from folder names; new output is isolated in a - Полный архив folder.
- Verified against live course artifacts: sign-player HTTP 200 on the selected Ethernet route; real master endpoint HTTP 200, #EXTM3U, 12 variants, no EXT-X-KEY; a real course image returned HTTP 200. Protected PDFs require the current WebView cookies as expected.

## [0.1.10-preview.16] - 2026-09-18

- Status: live-fix candidate
- Previous version: 0.1.10-preview.15
- Fixed: sign-player response body is read with bounded retries directly after responseReceived; transient loadingFailed no longer discards GetCourse player state; player config parser accepts nested JSON/JS/direct m3u8 variants.
- Added: per-lesson archive folder with Урок.docx, sanitized Страница.html, images, downloadable PDF/Office/archive attachments, and discovered videos. Lessons without video are archived instead of reported as failures.
- Performance: pages without a GetCourse player stop media waiting after about 3 seconds; pages with a player retain a longer bounded HLS wait.
- Verification: focused GetCourse/archive tests 51/51; Core 32/32; full Infrastructure 628 passed / 0 failed / 14 existing live-tool skips; Local and Managed Release builds 0 warnings/errors; embedded JavaScript syntax checked with Node.

## [0.1.10-preview.15] - 2026-09-18

- Status: live-fix candidate
- Previous version: 0.1.10-preview.14
- Fixed: GetCourse lesson/module redirects that surface transient WebView2 ConnectionAborted/OperationCanceled no longer count as failed navigation; the downloader waits for the final successful navigation before discovering media.
- Verification: focused GetCourse/planner/browser regression 32/32; Local Release build 0 warnings/errors.

## [0.1.10-preview.14] - 2026-09-18

- Status: live-fix candidate
- Previous version: 0.1.10-preview.13
- Fixed: whole-course GetCourse structure script no longer returns null because of malformed JavaScript regex; scans href/data-href/data-url/data-link/onclick and accepts WebView JSON-encoded results.
- Verification: focused GetCourse/browser regression 28/28; Local Release build 0 warnings/errors; embedded JavaScript syntax checked with Node.
- Live acceptance: re-login to the embedded InPrivate browser and retry the same course.

## [0.1.10-preview.13] - 2026-09-18

- Status: candidate
- Previous version: 0.1.10-preview.12
- Changed: whole GetCourse course traversal with nested training/module folders and ordered lesson filenames; one-click transcription for the last verified download; protocol/release readiness gate and exact package manifests.
- Safety: uses only the already-authorized embedded browser session; inaccessible lessons and DRM/encrypted HLS are not bypassed.
- Verification: automated regression and final package/release evidence are recorded by scripts/platform/Test-PlatformRelease.ps1.
- Rollback note: preview.12 and stable v0.1.9 remain untouched.

## [0.1.10-preview.12] - 2026-09-14

- Status: candidate
- Previous version: 0.1.10-preview.11
- Changed: exact iframe Referer-to-DOM binding for late GetCourse players; binding refresh on late iframe attachment and immediately before download; queue editing while downloads run; live queue consumption; work/partial files in the selected output folder; course filenames ordered module -> part -> remaining title -> quality -> duration.
- Verification: preview.12 regression tests 5/5; full component-enabled suite 258/258, 0 skipped.
- Live acceptance still required: closed iglyrazuma.ru lesson after a fresh InPrivate sign-in.
- Rollback note: published preview.11 and stable v0.1.9 remain untouched.
## [0.1.10-preview.11] - 2026-09-14

- Status: candidate
- Previous version: 0.1.10-preview.10
- Changed: real GetCourse player-to-HLS binding by CDP frameId/frame tree; page-order numbering; per-video quality; editable queue with move/delete/continue; queue-wide cancel; isolated download jobs; verified final promotion; duration in friendly filenames.
- Works: focused preview.11 regression tests 10/10; explicit component-enabled suite 253/253, 0 skipped; official self-contained release packaging succeeded; packaged EXE/WebView2 smoke passed with all preview.11 queue/quality controls present; the final GitHub release ZIP checksum is published alongside the downloadable asset.
- Does not work: DRM/encrypted HLS remains blocked by design; final packaged closed-GetCourse acceptance is still required.
- Rollback note: preview.10 and stable v0.1.9 remain untouched.

## [0.1.10-preview.10] - 2026-09-14

- Status: candidate
- Previous version: 0.1.10-preview.9
- Changed: GetCourse master download without manual playback, explicit 360p/480p/720p quality selection, friendly filenames, output recovery, batch download, information/help page, persisted system/light/dark themes.
- Works: automatic tests 244/244, 0 skipped; Release build succeeded; packaged UI smoke confirmed Information, help, and dark theme; ZIP SHA-256 E81A748E8191324F436D078955E3787E8C7109442DC43453CFCD7CC18DED7DD5.
- Does not work: DRM/encrypted HLS remains blocked by design; final closed-GetCourse packaged acceptance is still required.
- Rollback note: preview.9 and stable v0.1.9 remain untouched.

## [0.1.10-preview.9] - 2026-09-14

- Status: candidate
- Previous version: 0.1.10-preview.8
- Changed: verified WebResourceResponse HLS fallback for GetCourse/Kinescope when CDP media events are absent; safe window/proxy shutdown after active playback
- Works: automatic tests 228/228, 0 skipped; Release build pending; all five observed GetCourse videos play through scoped routing
- Does not work: live candidate-list/download acceptance for closed GetCourse still requires the preview.9 packaged test; DRM/encrypted HLS remains blocked
- Rollback note: preview.8 and stable v0.1.9 remain untouched

## [0.1.10-preview.8] - 2026-09-14

- Status: candidate
- Previous version: 0.1.10-preview.7
- Changed: WebView2/CDP HLS discovery для GetCourse/Kinescope, direct manifests, split video/audio, scoped Ethernet routing, privacy/lifecycle hardening
- Works: автоматические тесты 223/223, 0 skipped; Release build 0 warnings/errors; обычный и split HLS integration tests с FFmpeg/FFprobe; real Whisper integration
- Does not work: DRM/encrypted HLS не скачивается; живой закрытый GetCourse после упаковки ещё требует ручного входа и acceptance-проверки
- Rollback note: прежние preview и стабильная v0.1.9 не перезаписываются

## [0.1.9] - 2026-09-09 15:31 +03:00

- Status: complete
- Previous version: 0.1.9-dev.1
- Human owner: Oleg
- Implemented by: Codex
- Human reviewed by: approved for publication
- Changed: подготовлен публичный выпуск с авторским блоком «Создано Валерием» и кликабельным Telegram-контактом `@Velkoshkin`
- Works: авторский блок виден на всех страницах; Telegram-ссылка активна; загрузчик и редактор сохранены без изменений
- Does not work: коммерческий сертификат подписи кода Windows не включён; DRM и закрытые потоки не обходятся
- Files: WinUI shell, version source, README, changelog, release records
- Verification: Release build with 0 warnings and 0 errors; 21/21 tests passed; packaged EXE UI Automation; screenshot; ZIP SHA-256
- Commit/PR: feature commit `b8c2fa1`; release tag `v0.1.9`
- Rollback note: предыдущий стабильный выпуск `v0.1.8` остаётся доступен

## [0.1.9-dev.1] - 2026-09-09 14:54 +03:00

- Status: complete
- Previous version: 0.1.8-ci.1
- Human owner: Oleg
- Implemented by: Codex
- Human reviewed by: approved design
- Changed: в нижней части боковой панели добавлены жирная подпись «Создано Валерием» и ссылка `Telegram: @Velkoshkin`
- Works: подпись и контакт видны в переносимом EXE; Telegram-контакт распознан как активная гиперссылка
- Does not work: commercial Windows code-signing certificate is not included
- Files: src/VideoGrabber.App/MainWindow.xaml.cs, Directory.Build.props, README.md, CHANGELOG.md, RELEASE_NOTES.md, docs/CHANGE_REPORT.md
- Verification: Release build with 0 warnings and 0 errors; 21/21 tests passed; packaged EXE UI Automation; screenshot; ZIP SHA-256 `734B9F99AE3B820E5FDE844C1C6F00419FCAB77EF9E4C12BAD82E5403C197427`
- Commit/PR: pending
- Rollback note: блок автора изолирован в BuildAuthorCard и удаляется без влияния на загрузчик или редактор

## [0.1.8-ci.1] - 2026-09-09 14:40 +03:00

- Status: complete
- Previous version: 0.1.8-docs.1
- Human owner: Oleg
- Implemented by: Codex
- Human reviewed by: pending
- Changed: обновлены и закреплены по проверенным commit SHA официальные checkout, setup-dotnet и upload-artifact Actions
- Works: GitHub Actions использует актуальные Node.js 24-версии без предупреждения о принудительном обновлении Node.js 20
- Does not work: none
- Files: .github/workflows/build.yml, CHANGELOG.md, RELEASE_NOTES.md, docs/CHANGE_REPORT.md
- Verification: официальные релизы и подписи commit проверены через GitHub API; контрольный workflow pending
- Commit/PR: pending
- Rollback note: совместимо с прежним workflow; откат возможен одним коммитом

## [0.1.8-docs.1] - 2026-09-09 14:34 +03:00

- Status: complete
- Previous version: 0.1.8
- Human owner: Oleg
- Implemented by: Codex
- Human reviewed by: pending
- Changed: оформлена публичная страница проекта, инструкция переноса и экономный CI без повторной сборки тегов
- Works: опубликованный релиз v0.1.8 доступен вместе с ZIP и SHA-256; последние удалённые сборки успешны
- Does not work: цифровая подпись Windows отсутствует; DRM и закрытые потоки не поддерживаются
- Files: README.md, CHANGELOG.md, RELEASE_NOTES.md, docs/CHANGE_REPORT.md, .github, Directory.Build.props, scripts/Build-Release.ps1
- Verification: `dotnet restore`; Release build with 0 warnings and 0 errors; 21/21 tests passed; PowerShell syntax check; `git diff --check`
- Commit/PR: pending
- Rollback note: документацию и CI можно откатить одним коммитом; бинарник v0.1.8 не изменяется
