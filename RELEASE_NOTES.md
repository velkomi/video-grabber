# Журнал подготовки релизов
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
