# История изменений

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии следуют [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]
- Preview.27 ships a full self-contained media runtime with the application: yt-dlp, FFmpeg, FFprobe, Deno, whisper.cpp CLI and a multilingual `ggml-base` model. Normal use no longer downloads or installs these components at runtime; a missing bundled tool is reported as a damaged/incomplete distribution instead of starting a background installer.
- Local transcription now always uses the bundled Whisper CLI/model. The Components page is now an informational `Встроенные инструменты` page, and the `Звук и текст` block explains MP3 vs text+SRT clearly. Its pause button is now actually visible.
- Local media controls explicitly explain when they are disabled by an active course/download operation and re-enable automatically afterward. Invalid/corrupted media now gets a direct FFprobe-readable error instead of being confused with a file that merely has no audio track.
- The full bundled editor runtime was verified with a real FFmpeg trim+join integration test, and bundled Whisper was verified end-to-end on a real video fragment producing both TXT and SRT.
- Preview.26 shortens lesson verification files to `VG.lesson.json`; after a 100% successful final course audit, all per-lesson manifests are atomically consolidated into one root `VG.verify.json` and the per-lesson JSON files are removed. Legacy `VideoGrabber.lesson.json` remains readable and is migrated automatically.
- Pause controls now keep a fixed 145 px width in both states. While work is running, `⏸ Пауза` is yellow with dark text; when paused it becomes green `▶ Продолжить` without changing size.
- Preview.25 adds course integrity verification and interactive pause/resume: every lesson records a live verification manifest with expected video/material counts; multi-video lessons require every numbered `Видео 01…NN` output, old archives without manifests are rechecked from the live course once, and a final audit automatically retries missing content before verified cache cleanup.
- Course downloads now have an independent quality ceiling (`до 360p`, `до 480p`, `до 720p`, `best`) persisted with resume state; when an exact HLS height is absent the closest available lower variant is selected for the course.
- Pause/resume controls are available in the main download area, browser video area, whole-course area and audio/text tools. The button switches from `⏸ Пауза` to green `▶ Продолжить`; process-backed downloads/transcoding and course loops pause without discarding resumable state.
- Course image archiving now detects actual image type from MIME/signature, repairs misleading extension-like names such as `Рис. 1`, writes proper `.jpg/.png/.webp/.gif` files, and adds local image/attachment references to saved HTML.
- Custom-domain GetCourse schools can inherit the standard GetCourse/Kinescope CDN route family only when the routed page is actually a GetCourse `/teach/control/` page; ordinary custom sites remain isolated.
- Preview.24 fixes GetCourse servicecdn routing: *.servicecdn.ru now inherits the course network session, so HLS segments no longer fall through to the VPN/system route. Adds portable auto-physical routing that selects Ethernet/Wi-Fi dynamically, excludes VPN/Tunnel/WSL/virtual adapters, falls back to the system route only when physical paths fail, and automatically migrates unavailable adapter IDs to Auto on another computer.
- Preview.23 automatically recovers transient whole-course network failures instead of stopping the run, persists state before retry, retries incomplete lessons/videos in additional passes, hides .vg-job workspaces, discards zero-byte part/ytdl checkpoints before a fresh retry, and treats permanent 4xx attachment links as source-unavailable markers rather than endless retry blockers.
- Preview.22 makes course folder order deterministic and site-like: 00 - Общая информация, then 01 - МОДУЛЬ №1 through 08 - МОДУЛЬ №8; nested lessons and trainings share one sequence instead of restarting at 01. Resume now refuses a mismatched course state and can auto-select a matching state from a direct child folder. The embedded-browser button is dark green with explicit scroll/login guidance, and whole-course controls explain that GetCourse authorization is required before downloading.
- Preview.21 reconciles legacy course archives against local DOCX/HTML/media/attachment files before resuming, jumps to the first truly incomplete lesson, keeps fallback-quality partials in separate stable resume workspaces, and preserves persistent VideoGrabber.course.json crash recovery plus repeated fresh-master retries for transient GetCourse fragment failures.
- Preview.20 adds persistent crash-resume for whole GetCourse archives: VideoGrabber.course.json is written atomically after each completed lesson, restart can reopen a course folder, stable per-video .vg-job keys allow yt-dlp --continue to reuse partials, legacy random job folders are migrated, verified completed media are recovered, and transient GetCourse CDN fragment failures retry up to four times with refreshed signed masters and quality fallback when Best is selected.
- Preview.19 fixes attachment finalization by closing temporary streams before promotion, adds elapsed time since course start, logs safe manual-download metadata for future diagnosis, keeps the custom crash-free course progress bar, and preserves resumable/global-cancel/cache-cleanup behavior.
- Preview.18 adds resumable course progress with module/lesson/file status, overall percentage and ETA, red global cancel, safe temp cleanup, verified recovery of completed .vg-job media, compact DOCX output without blank DOM spacing, stricter GetCourse comment/answer cleanup, attachment HTTP/1.1 routed downloads with scoped cookies, and robust owned-output recovery when yt-dlp reports an untrusted filepath.
- Preview.17 resolves GetCourse videos from lesson DOM data-iframe-src and extensionless /api/playlist/master endpoints, routes protected attachments directly through the selected adapter with WebView cookies, cleans lesson archives to real .lite-page content, normalizes module/lesson folder titles, groups root lessons under 00 - Вводные материалы, repairs UTF-8 attachment names, and writes into a separate - Полный архив course folder.
- Preview.16 archives each GetCourse lesson to its own folder (DOCX + sanitized HTML + images + downloadable attachments + video), retries sign-player response bodies directly instead of depending on loadingFinished, retains player retries across transient loadingFailed, and shortens no-video waits.
- Preview.15 waits through GetCourse redirect NavigationCompleted ConnectionAborted/OperationCanceled events instead of treating them as final lesson failures.
- Preview.14 fixes whole-course GetCourse DOM extraction on live training pages: valid embedded JavaScript, generic GetCourse link attributes, WebView JSON-result compatibility, and safe structure diagnostics.
- Preview.13 adds one-click transcription of the last verified download and whole-course GetCourse traversal with nested module folders, ordered lesson filenames and resume-by-existing-output.
- Platform release readiness now records protocol compatibility, exact source SHA, Local/Managed/API/Worker manifests and fail-closed BLOCKED live gates.
- Preview.12 fixes exact GetCourse player ordering by iframe URL, allows queue edits during active downloads, keeps work files in the chosen output folder, and improves course filenames.


- preview.11: real GetCourse player-to-HLS binding and page-order numbering, per-video quality, editable/resumable queue, queue-wide cancel, isolated job folders, verified final-file promotion, and duration in filenames.
- GetCourse master candidates can be downloaded without manual playback after automatic clear-HLS preflight; 360p and 480p quality caps are available.
- Added friendly ordered filenames, successful-output recovery, batch download, in-app help/information, and persisted system/light/dark themes.

### Добавлено

- универсальный HLS-sniffer во встроенном WebView2 для GetCourse/Kinescope, включая extensionless manifests и раздельные video/audio дорожки;
- точечная маршрутизация GetCourse/CDN через выбранный сетевой адаптер без изменения системного VPN.

### Исправлено

- скачивание найденных HLS теперь использует direct/generic extractor, временные cookies/Referer и fail-closed проверку шифрования;
- исправлены lifecycle CDP iframe, порт GetCourse 3001, split-HLS merge, MP3 cleanup и обработка callback/timeout дочерних процессов.

## [0.1.9] - 2026-09-09

### Изменено

- документация установки, использования, диагностики и переноса на другой компьютер;
- GitHub Actions больше не запускает повторную сборку для тега того же коммита.
- служебные Actions обновлены до проверенных Node.js 24-версий и закреплены по commit SHA.
- в интерфейс добавлены заметная подпись автора и кликабельный Telegram-контакт `@Velkoshkin`.

## [0.1.8] - 2026-09-08

### Исправлено

- живой процент, скорость и оставшееся время теперь безопасно обновляются в интерфейсе;
- подтверждена загрузка открытого видео Fathom.

## [0.1.4] - 2026-09-06

### Исправлено

- обновления прогресса направляются в UI-поток WinUI.

## [0.1.3] - 2026-09-06

### Добавлено

- разбор прогресса yt-dlp.

## [0.1.2] - 2026-09-06

### Исправлено

- автоматическая установка медиакомпонентов при первой загрузке.

## [0.1.1] - 2026-09-06

### Изменено

- светлая доступная тема и фирменный значок приложения.

## [0.1.0] - 2026-09-06

### Добавлено

- первый публичный выпуск VideoGrabber для Windows x64.

[Unreleased]: https://github.com/velkomi/video-grabber/compare/v0.1.9...HEAD
[0.1.9]: https://github.com/velkomi/video-grabber/compare/v0.1.8...v0.1.9
[0.1.8]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.8
[0.1.4]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.4
[0.1.3]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.3
[0.1.2]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.2
[0.1.1]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.1
[0.1.0]: https://github.com/velkomi/video-grabber/releases/tag/v0.1.0
