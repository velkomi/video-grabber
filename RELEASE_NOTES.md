# Журнал подготовки релизов

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
