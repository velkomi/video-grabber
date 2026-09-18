# История изменений

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии следуют [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]
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
