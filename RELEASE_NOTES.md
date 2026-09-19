# Журнал подготовки релизов

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
