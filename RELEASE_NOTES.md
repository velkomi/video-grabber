# Журнал подготовки релизов

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
