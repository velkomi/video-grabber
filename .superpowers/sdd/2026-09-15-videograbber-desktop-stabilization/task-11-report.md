# Task 11 — Queue lifetime and session identity

## Scope and implementation

Base: `bff286236a613dab0e31aa7e1bb2b14168f9b77b`, branch `fix/full-audit-20260915`.

- Queue entries capture Source, ordinal, per-video quality, page URI, local session epoch and page metadata. Manifest variants/audio renditions, section-title lists and player-slot lists are copied into read-only collections. The selection dictionary contributes a captured quality string; it is not retained. The queue does not expose its mutable backing list.
- Discovery reset retains queue entries and ordering. Discovery/binding/duration updates no longer rewrite queued candidates. Rendering and suggested filenames use captured metadata; queued preparation bypasses live binding refresh and uses the saved page in cookie scope.
- Production `BrowserDownloadOperation.OnNavigation` cancels the active operation and clears pending run intent. `OnSessionChanged` also records the changed session epoch. `CanContinue` and `RunQueuedAsync` reject missing/stale context, changed Source, changed quality or mismatched intent epoch before preparation/downloader calls.
- Both direct and queued execution delegate to the same `RunAsync` implementation. Direct intent validation continues to use the page generation. Queued validation uses the saved local session epoch, while the live page lease still cancels work on navigation. These two counters are deliberately separate.
- App navigation, cookie-selector changes and browser destruction call the production lifecycle methods. An App navigation version also guards a posted StartQueue callback and the interval after awaited completion, preventing automatic continuation or queue removal after navigation.
- Navigation displays a paused-queue explanation. Old session entries remain visible with `Требуется повторный выбор сессии`; users must explicitly select the intended session and add that video again. Closing the embedded browser can now cancel an active browser operation rather than refusing to close it.
- Incomplete queues retain the selected embedded-cookie mode so same-session Continue remains possible. Normal reset still occurs once the queue is empty. Manually changing the selector invalidates the captured epoch.

The epoch represents local session selection/destruction, not detection of a website account switch. Disk persistence is not implemented. No restart-persistence claim is made.

## Authorized allowlist extensions

The original eight-file list was extended by the parent with:

1. `src/VideoGrabber.App/MainWindow.Download.cs`: dispatch queued entries through `RunQueuedAsync`, preserve direct dispatch, guard posted queue start after navigation.
2. `src/VideoGrabber.App/BrowserDownloadPreparation.cs`: keep queued binding/metadata/page snapshots and separate session validation from direct page-generation validation.
3. `src/VideoGrabber.App/MainWindow.HlsPreflight.cs`: only remove the obsolete `SyncQueuedCandidate(updated)` call. No HLS verification policy was changed.

Cost: three extra App files avoid duplicating preparation and operation orchestration in the batch partial. Production/test changes are limited to these eleven files plus this report. No packages, external/private media, infrastructure changes, push, release or GUI session effects were used.

## RED → GREEN evidence

Evidence directory:

`C:\Users\Oleg\Documents\Codex\2026-09-15\videograbber-codex-astra-gpt6-full-audit\outputs\artifacts\full-audit\20260915-01a0a373\fix-execution\task-11`

| Evidence | Result | Meaning |
| --- | --- | --- |
| `t11-red-snapshot.trx` | 1 failed | Mutating original manifest lists emptied the queued candidate's lists on the base implementation. |
| `t11-red-contract.log` | compile RED | New context and lifecycle APIs were absent. |
| `t11-red-lifecycle.trx` | 8 failed | With placeholder lifecycle methods, queued session epoch was incorrectly treated as a page generation; explicit resume/mismatch results and App reset wiring failed. The blocked-preparation case timed out because preparation never began under that incorrect epoch comparison. |
| `t11-green.trx` | 45 passed | Required production lifecycle, queue, session and coordinator group. |
| `t11-green-final.trx` | 47 passed | Expanded lifecycle callback-only checks, without resetting the page lease. |
| `t11-broad-core.trx` | 12 passed | Core regression. |
| `t11-broad-infrastructure-final.trx` | 525 passed, 3 failed, 1 skipped | Only the same three future-task failures as Task 10 remain. |
| `t11-app-build-final.log` | 0 errors, 0 warnings | WinUI App build with portable SDK and no restore. |

Behavioral tests instantiate the real `BrowserDownloadQueue`, `BrowserPageLifetime`, `OperationCoordinator` and `BrowserDownloadOperation`. Only external preparation and downloader boundaries are faked. Tests cover active navigation cancellation, pending intent clearing, unchanged entries, same-session explicit resume with A's Source/quality, direct B execution under B's page generation, session-change cancellation without page reset, stale caller epochs, and zero preparation/downloader calls for rejected queue entries. Collection tests mutate the original lists/dictionary and attempt mutation through exposed collection interfaces. Source assertions only supplement App delegation checks; they are not the primary lifecycle evidence.

## Broad-run environment and adjudicated hypothesis

The first broad run had four failures: three existing future-task failures plus HTTP 404 in the synthetic six-parts fixture, because Task 11's evidence directory initially lacked fixture source files. Exactly 42 files from Task 10's recorded synthetic manifest were copied, each checked against the recorded SHA-256 before and after copying. `six-parts-copy-manifest.json` records the paths and hashes. The final broad run regenerates six successful rows in `six-parts-behavior.json`, including decoded frame/audio identity and requested heights.

`future-red-infrastructure.json` compares the exact final failure names with Task 10 and records zero unexpected failures. Existing failures are empty/reversed SRT acceptance and cancellation of a pipe-holding descendant after parent exit. The Whisper model-dependent integration remains skipped.

An initial hypothesis that a non-master HLS snapshot could not resume after cache reset was rejected after reading `HlsDownloadPolicy.AreSelectedTracksVerified`: a parsed clear leaf already passes before the non-master rejection branch in App preflight. The parent explicitly ruled out adding fresh revalidation policy in Task 11; that policy belongs to later transport work. HLS changes here only remove queued-snapshot mutation.

## Reproduction and limits

From the repository, dot-source the evidence directory's `run-tests.ps1`, then run:

```powershell
Run-VgTests 't11-green' 'FullyQualifiedName~BrowserDownloadOperationTests|FullyQualifiedName~BrowserDownloadQueueTests|FullyQualifiedName~BrowserSessionTests|FullyQualifiedName~OperationCoordinatorTests'
Run-VgTests 't11-broad-infrastructure-final' 'FullyQualifiedName~VideoGrabber'
Run-VgTests 't11-broad-core' 'FullyQualifiedName~VideoGrabber' 'tests/VideoGrabber.Core.Tests/VideoGrabber.Core.Tests.csproj'
& 'D:\CODEX\Portable\dotnet-sdk-10\dotnet.exe' build src/VideoGrabber.App/VideoGrabber.App.csproj --no-restore
```

The inherited Tee pipeline may return shell zero after test failure; TRX results and `test-summary.json` are authoritative. The final App build process returned zero. Whitespace/allowlist checks precede the exact-file commit.

Real-window A→B, session-selector/logout rendering and explicit Continue behavior still require the later GUI acceptance gate. No source test, unit test or build result is presented as actual WebView2/UI execution. The three broad-suite failures are unchanged and remain for their assigned tasks.

## Fix round 1 - 2026-09-16

Reviewer P2 root cause was confirmed: automatic one-use cookie selector cleanup fired SelectionChanged and incorrectly advanced the local session epoch, invalidating preserved queue entries even though the user had not changed identity/session.

Fix commit: `7d9f6ab` (`fix: preserve queued session during cookie cleanup`). The fix introduces `BrowserSessionLifetime`, suppresses only synchronous programmatic selector cleanup, keeps explicit invalidation for real user/session changes, captures `CookieSelection` into queued context, and validates queued intent against the captured cookie mode.

Fresh verification after the fix:
- focused queue/session/operation/coordinator: 51 passed, 0 failed;
- Core: 12 passed, 0 failed;
- Infrastructure with integration tools: 529 passed, 3 failed, 1 skipped; the three failures are the same assigned future REDs (two SRT validation cases and pipe-holding descendant cancellation), and the skip is Whisper integration;
- Release App build: 0 warnings, 0 errors;
- `git diff --check`: clean (line-ending conversion warnings only).

An earlier broad invocation on 2026-09-16 omitted `VIDEOGRABBER_EVIDENCE` and failed audit-isolation initialization; it is an invalid verification invocation and is not product evidence. The corrected runs above are authoritative.

A fresh `gpt-6-astra/high` Codex re-review was attempted from session `01a0a985-6949-7822-baea-7328e6516624`, but Codex returned the account usage-limit gate until 2026-09-19 11:30. Therefore Task 11 is locally verified and committed, while the independent Astra re-review gate remains externally blocked rather than silently substituted.
