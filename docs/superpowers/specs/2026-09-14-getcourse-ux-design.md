# VideoGrabber GetCourse UX / Theme / Help Design

## Goal
Сделать найденные master-HLS видео пользовательскими сущностями: выбирать качество и скачивать без ручного Play, получать понятные имена файлов, пакетно скачивать найденное, иметь встроенную справку и полноценную светлую/тёмную тему.

## Observed live behavior
- GetCourse lesson exposes six master HLS candidates immediately (#1-#6), each advertises 360p/480p/720p.
- Child media playlists only appear after playback (#7/#8), and those currently download.
- Live downloads complete in yt-dlp, but output path may be lost; files are created with token/query-heavy names and UI reports false failure.
- DRM/encrypted HLS must remain unsupported and fail closed.

## Download model
- Show master candidates as primary videos; hide child media playlists from normal list when they belong to a known master.
- Selecting a master + quality resolves a concrete child playlist without requiring manual playback.
- Before download, fetch/inspect the selected child manifest and allow only clear HLS.
- Add explicit 360p and 480p options; 720p/1080p/4K/best remain supported where available.
- Add “Скачать все найденные” for primary video candidates, sequentially, using the selected quality and current auth/session mode.
## Naming
- Never place query strings, JWT, signed parameters, CDN internals or cookies in filenames.
- Prefer page/module/part/topic metadata from the lesson DOM when available.
- Stable fallback: `NN - Видео NN - <quality>.mp4`.
- Preferred example: `02 - Часть 2 - Название темы - 720p.mp4`.
- Sanitize Windows-invalid characters and cap filename length.
- Preserve ordering for batch downloads with a two-digit ordinal prefix.

## Information / Help
- Add `ⓘ Информация` in the app chrome and `ⓘ Как скачать?` beside the embedded browser controls.
- Information view contains: app version, creator “Создано Валерием”, Telegram `@Velkoshkin`, privacy/local processing, supported workflows and limitations.
- GetCourse instructions: paste lesson URL → open embedded browser → authenticate on the site if required → wait for primary videos → choose video/quality or “Скачать все найденные”. Manual Play should not be required for primary masters.
- Document direct HLS/DASH/MP4 and sites supported by installed yt-dlp as best-effort, not guaranteed.
- State clearly: DRM/encrypted HLS/key extraction is not supported.

## Theme
- Theme selector: `Как в Windows`, `Светлая`, `Тёмная`; persist locally.
- Dark theme must style app background, sidebar, cards, borders, text, fields, buttons, selected states and progress with sufficient contrast.
- Do not inject dark CSS into third-party WebView pages; site content keeps its own theme.
## Safety / release constraints
- Preserve existing per-site Ethernet routing and system VPN isolation.
- Passwords are entered only in the embedded InPrivate site; VideoGrabber does not read/store them.
- Temporary embedded-session cookies remain opt-in per download and are deleted after use.
- Keep privacy-safe logs: host/type/status only; no signed path/query/cookie/token leakage.
- Stable `v0.1.9` and previous previews are never overwritten.
- Next package is `0.1.10-preview.10` in a separate folder/ZIP.
- Do not commit `src/VideoGrabber.App/SmokeWindow.cs` or scratch artifacts.
- Do not push/publish GitHub without a separate user instruction.
- One local commit only after live acceptance passes.

## Acceptance
- Six primary GetCourse videos appear as useful entries, not raw technical stream IDs.
- At least 360p and 720p from a master can download without manually playing the video first.
- Output filename is human-readable and contains no secret/query material.
- Batch download queues all primary videos sequentially.
- Information/help views explain GetCourse auth and download flow.
- Light/dark/system themes render legibly and persist.
- Closing after active media session produces no user-visible shutdown error.
- Final package passes full Release suite with real FFmpeg/FFprobe/yt-dlp/Whisper integration, 0 skipped, plus FFprobe and full FFmpeg decode on live downloaded media.
