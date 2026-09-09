# Журнал подготовки релизов

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
