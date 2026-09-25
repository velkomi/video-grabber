# История изменений

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии следуют [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

## [0.1.10-preview.35-rc.1] - 2026-09-25

### Добавлено

- прямое скачивание обычных видео через сайт без обязательного Windows-приложения: server worker готовит файл, после чего браузер получает короткоживущую подписанную streaming-ссылку;
- отдельный выбор «В браузер — без приложения» / «На Windows-компьютер через приложение»;
- фирменный значок VideoGrabber на сайте, favicon и в Telegram Mini App;
- раскрываемый блок «Дополнительные возможности» в Windows-приложении для MP3, курсов, видео со страниц и транскрибации;
- отдельная видимая карточка полного курса с кнопками «Открыть курс во встроенном браузере» и «Скачать весь курс»;
- автоматическое включение приёма явно отправленных Windows-заданий после входа, с запоминаемым ручным отключением.

### Изменено

- основной интерфейс Windows оставлен сверху и упрощён; дополнительные инструменты можно развернуть и снова свернуть;
- полный курс остаётся Windows-функцией и не маскируется под обычное браузерное скачивание;
- сайт больше не требует online Windows-устройства для обычного видео или MP3; устройство требуется только при явном выборе отправки в Windows;
- исправлен одновременный показ формы входа и уже открытого кабинета за счёт корректной обработки hidden;
- статус Windows-устройства теперь относится только к режиму отправки заданий на компьютер, а не к обычному скачиванию через сайт.


## [0.1.10-preview.34-rc.1] - 2026-09-24

### Добавлено

- кликабельные карточки тарифов на сайте с подробным диалогом возможностей каждого плана и переходом к оформлению;
- заметный блок скачивания Windows-версии с постоянным маршрутом /download/windows;
- статус macOS «в разработке» и ссылка на Telegram для пожеланий пользователей Mac;
- единый desktop-диалог для функций, недоступных по тарифу, с объяснением причины и переходом к подходящему плану;
- подробное описание Free / Start / Unlimited Video / Full Course в разделе «Информация» Windows-приложения.

### Изменено

- тарифные функции в Managed больше не выглядят как сломанные серые кнопки: MP3, редактор, транскрибация и полный курс остаются нажимаемыми и объясняют требования к тарифу;
- кнопка «Пауза» остаётся интерактивной до запуска операции и объясняет, когда и как она работает; во время операции сохраняется обычная пауза/продолжение;
- «Отменить всё» при отсутствии активной операции показывает пояснение вместо молчаливого неактивного состояния;
- сайт больше не отключает варианты MP3 / Full Course в select: при попытке запуска показывает подходящий тариф;
- Windows-клиент направляет выбор тарифа на официальный сайт, где будет подключён единый checkout;
- верхний режим «Скачать MP3 (только звук)» теперь явно проходит через premium_media и не может расходовать Free-квоту как обычное видео.


## [0.1.10-preview.33-rc.1] - 2026-09-24

### Добавлено

- переработанный Telegram Mini App в тёмном стиле VideoGrabber Managed с вкладками «Аккаунт», «Загрузки» и «Подписка»;
- единый визуальный статус синхронизации Web · Windows · Telegram, текущего тарифа и остатка Free-лимита;
- отдельная подсказка/блокировка Mini App до привязки Telegram к основному Google/e-mail аккаунту;
- серверная операция `premium_media` для явного разделения бесплатных обычных видео и платных расширенных функций.

### Исправлено

- устранена гонка запуска Mini App: media/payments теперь ждут завершения Telegram session, поэтому случайный `HTTP 401` при открытии разделов больше не возникает;
- исправлен сбой общего Mini App refresh из-за обращения к отсутствующей кнопке `#media-action`;
- Free теперь серверно разрешает 10 обычных загрузок видео на единый аккаунт и не открывает MP3/редактор/транскрибацию/полный курс;
- сайт блокирует MP3/Full Course согласно тому же `AccessSnapshot`, который используют Telegram и Windows;
- VideoGrabber Managed блокирует загрузку до авторизации, показывает права текущего аккаунта и не позволяет обойти тариф прямыми локальными кнопками;
- MP3 в Managed теперь резервирует ту же серверную `premium_media`-квоту, что Web/Telegram: Start учитывает дневной лимит одинаково на всех поверхностях, а Free не может обойти paywall через Windows;
- прямое «Скачать весь курс» в Managed теперь проходит через общий Managed authorization coordinator и требует Full Course.

### Проверено

- Core: 32/32;
- Platform: 328/328;
- Worker: 31/31, включая реальный HLS proxy test с yt-dlp/FFmpeg/FFprobe;
- Windows Infrastructure: 677 passed, 14 предусмотренных integration skips, 0 failed;
- Managed Windows Release build: 0 warnings, 0 errors;
- Mini App JavaScript и Web JavaScript: синтаксическая проверка Node.js пройдена.

## [0.1.10-preview.32-rc.1] - 2026-09-23

### Добавлено

- единый VideoGrabber account для Web, Managed Windows и Telegram;
- публичный Web-интерфейс на `https://videograbber.srv1902378.hstgr.cloud/` с кабинетом, устройствами, очередью и тарифами;
- вход через существующий Supabase Auth: Google OAuth и passwordless e-mail Magic Link;
- одноразовый безопасный Web → Managed Windows handoff без передачи Supabase token в loopback callback;
- self-service привязка Telegram через `/link` в `@VideoGra_bot`;
- тарифы Free / Start / Unlimited Video / Full Course с отдельным правом `course_download`;
- Free starter = 10 lifetime-загрузок один раз на внутренний account;
- Start = максимум 10 логических загрузок за UTC-сутки с конкурентной атомарной квотой;
- Web → Desktop local-only completion: готовые видео/курсы остаются на компьютере и не обязаны загружаться на VPS;
- online/offline Windows-device status по криптографически подтверждённому heartbeat;
- отдельный production egress proxy, runtime PostgreSQL roles/RLS, root-only secrets и readiness endpoint;
- maintenance/backup/retention timer с проверкой backup и safe-path gate перед очисткой server artifacts.

### Изменено

- Managed Windows использует тот же Web/Supabase login, что и сайт;
- Telegram-only аккаунт не может скачивать или покупать до привязки к основному Google/e-mail account;
- при merge/link временный Free starter не переносится и не удваивает lifetime-лимит;
- Telegram Bot API настроен для `@VideoGra_bot`: webhook, Mini App menu, команды и профиль;
- публичная команда Stars-покупки скрыта до настройки реальных XTR-цен; прямой handler остаётся fail-closed;
- server artifact retention после успешной Telegram-доставки сокращается, а cleanup выполняется только после квалифицированного backup/dry-run.

### Проверено

- Platform tests: 325/325;
- Core: 32/32;
- Worker: 31/31;
- Windows Infrastructure: 677 passed, 14 прежних integration skips, 0 failed;
- production readiness: `/health/ready` = 200;
- production acceptance временным внутренним account: Free-10, тарифный каталог, desktop source, waiting-for-worker job, Full Course denial на Free и полный cleanup;
- Managed автономный bundle с yt-dlp, FFmpeg, FFprobe, Deno и Whisper.

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
