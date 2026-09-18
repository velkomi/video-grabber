# VideoGrabber release acceptance

A build artifact is not sufficient evidence that the platform is releasable.

Run scripts/platform/Test-PlatformRelease.ps1 with -SourceCommit <exact-git-sha>, -EvidenceDirectory <absolute-path>, and -Mode Local or Stage.

The script writes release-readiness.json. Overall status is READY only when every mandatory check is PASS. Any FAIL or BLOCKED keeps the result NOT_READY.

## Automated gates

The gate verifies the exact HEAD SHA and a clean worktree, locked package restore, all Core/Infrastructure/Platform/Worker tests, Release builds for Local/Managed/API/Worker, PowerShell and deployment JSON syntax, Mini App JavaScript syntax, and four release packages.

Each package contains manifest.json with the exact source SHA plus release-files.sha256. Package archives are produced separately for Local, Managed, API, and Worker targets.

The command output and TRX files are stored below the selected evidence directory. A later successful command never replaces an earlier failure.
## Live/manual evidence

ManualEvidencePath points to JSON whose required boolean fields must be explicitly true. Local mode requires cleanWindowsLocalManaged, authorizedCourseDownload, telegramMiniApp, providerCollisionFlows, and sandboxPaymentsRecurringRefunds.

Stage mode additionally requires stageDeployment, recoveryDrill, alertDelivery, and capacityBaseline.

Missing evidence is BLOCKED, never SKIP. Use -ReportOnly during engineering review when you want the JSON map without turning a blocked manual gate into a nonzero process exit.

## Release boundary

Public push, merge, tag/release publication, production migration, and live payment enablement remain distinct release actions. The gate prepares evidence for those actions; it does not silently perform them.

For schema changes, prefer forward-compatible additive migrations. Do not use a destructive rollback migration. If code rollback is schema-compatible it may be used; otherwise roll forward after the approved restore drill and data-impact review.