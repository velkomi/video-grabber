# Отчёт изменений

| Версия / дата | Результат | Файлы и компоненты | Ответственный | Реализация | Проверка | Ссылки | Ограничения |
|---|---|---|---|---|---|---|---|
| 0.1.10-preview.64-rc.1 / 2026-10-09 | Информация/контакты/согласия/права на курс, подготовка российского запуска | Info/Web/Mini App/Windows/Bot/Persistence066 | Oleg | Codex | CI37959772908 success;57JS/12consent/32identity; VPS066; installed64 own13files | docs/releases/preview64.md; docs/compliance/owner-next-steps.md | полный комплект исходников медиаутилит и native GUI остаются отдельными gates |
| 0.1.10-preview.63-rc.1 / 2026-10-09 | Опубликованы подписанные обновления Windows; установлен preview.63; подготовлен подтверждаемый перенос адреса | Core/Contracts, клиент/кэш/загрузка, WinUI, API, publisher | Oleg | Codex | CI37929040010 success; Core98; Infra744/1skip; publisher17; API15; 557 файлов; единый аккаунт | docs/releases/preview63.md; docs/platform/client-updates-runbook.md | preview; GUI QA blocked automatic approval; новый домен не задан |
| 0.1.10-preview.62-rc.1 / 2026-10-08 | Опубликованы рефералка, бонусный баланс и промокоды; обновлены VPS и установленный Windows | API, PostgreSQL 064/065, Web, Mini App, Windows, Telegram | Oleg | Codex | CI 37802388814 success; живой аккаунт/quotes; 557 установленных файлов; публичные ZIP/EXE SHA-256 | docs/releases/preview62.md; GitHub Release preview.62 | preview; управление окнами остановлено пользователем Esc; реального списания не было; Stars-цены не настроены |
| 0.1.10-preview.61-rc.1 / 2026-10-08 | Studio-интерфейсы, Windows auth, восстановление WebGL, Telegram-меню и полный контакт разработчика | Windows, Web, Mini App, API, Telegram, telemetry, tests | Oleg | Codex | Свежие проверки и пакеты: docs/releases/preview61.md | RELEASE_NOTES.md | preview; ручная приёмка нового OAuth и реального Telegram-видео ожидается |
| 0.1.8 / 2026-09-08 | Живой прогресс загрузки и проверенный переносимый Windows-релиз | Downloader, WinUI, tests, release ZIP | Oleg | Codex | 21 тест; локальная сборка; ручная загрузка Fathom; GitHub Actions | релиз | без цифровой подписи; без DRM-обхода |
| 0.1.8-docs.1 / 2026-09-09 | Полная публичная инструкция и оптимизация CI | README, GitHub metadata/workflow, release records | Oleg | Codex | Release build; 21/21 tests; PowerShell syntax; GitHub Actions pending | pending | бинарник не изменён |
| 0.1.8-ci.1 / 2026-09-09 | Актуальные Node.js 24 Actions, закреплённые по commit SHA | GitHub workflow | Oleg | Codex | GitHub API verified commits; workflow pending | pending | бинарник не изменён |
| 0.1.9-dev.1 / 2026-09-09 | Подпись автора и кликабельный Telegram-контакт в интерфейсе | WinUI shell, version, documentation | Oleg | Codex | Release build; 21/21 tests; packaged EXE UI Automation; screenshot; SHA-256 | pending | цифровая подпись EXE отсутствует |
| 0.1.9 / 2026-09-09 | Публичный выпуск с авторским блоком и Telegram-контактом | WinUI shell, version, documentation, release ZIP | Oleg | Codex | Release build; 21/21 tests; packaged EXE UI Automation; GitHub Actions | релиз | без цифровой подписи; без DRM-обхода |

Человеческая приёмка изменений `0.1.8-docs.1`: ожидается.

| 0.1.10-preview.56-rc.1 / 2026-09-29 | Консолидация сайта, установщика, owner TOTP и транскрибации курса | Web, installer, Admin, course ASR | Oleg | Codex | Targeted suites + Release builds | RELEASE_NOTES.md | preview |
| 0.1.10-preview.60-rc.1 / 2026-09-29 | Эксклюзивный owner-admin, очистка пользовательской терминологии, release anti-rollback | API/Admin, Windows UI, Web, deploy | Oleg | Codex | API/App builds, PostgreSQL owner integration test | RELEASE_NOTES.md | preview |
| post-preview.60 / 2026-10-01 | Настоящий Three.js hero, Raycaster, единый GSAP story-stage, deterministic visual QA | Public Web, Three.js, GSAP, deployment smoke | Oleg | Codex | GitHub Actions + browser/design QA recorded in design-qa.md | design-qa.md | physical-device/field-vitals evidence отдельно |

