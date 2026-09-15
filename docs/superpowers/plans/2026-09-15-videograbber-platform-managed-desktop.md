# VideoGrabber Managed Desktop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a separate managed Windows build with a working account page, authorization at every protected entry point and a restart-safe queue.

**Architecture:** Reuse the existing WinUI shell and downloader through an authorization coordinator. Put testable policy and lease verification in Core and HTTP/DPAPI/queue storage in Infrastructure; select Local or Managed edition at build time without modifying existing published binaries.

**Tech Stack:** .NET 10/C# 14, existing WinUI 3/WebView2, HttpClient, System.Security.Cryptography.ProtectedData 10.0.0, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-15-videograbber-platform-auth-licensing-design.md`; program/rulings: `docs/superpowers/plans/2026-09-15-videograbber-platform-program.md`.

## Global Constraints

- Authentication providers: Google, Apple, Yandex, Telegram, plus verified email as recovery/fallback.
- One VideoGrabber account may have several linked identities.
- Guest accounts remain guests until a paid entitlement exists; gifts do not change the role to purchaser.
- Owner/Admin has permanent unlimited usage and does not consume download credits.
- Guest Windows device limit: 1 active computer.
- Admin Windows device limit: 3 active computers.
- Offline grace after a successful online license check: 24 hours for Guest/User, 72 hours for Admin.
- Download-credit spending requires an online reservation; offline clients may use time-based access but may not spend shared credits.
- Existing stable/local releases remain separate from the managed edition; no attempt is made to retroactively lock old binaries.
- No Supabase service-role key or payment secret is embedded in the desktop application, bot Mini App or public repository.
- Access/refresh tokens, OAuth codes, Telegram login signatures, cookies and signed media URLs are redacted from logs.
- Linking a new identity requires proof of control of both the existing account session and the new provider identity. Accounts must not auto-merge merely because provider emails match.
- Do not attempt DRM, CAPTCHA, paywall or account-control bypass.
- Do not delete existing user media when access expires.
- Existing repository baseline: SDK `10.0.400`, C# `14.0`, `net10.0`; WinUI target `net10.0-windows10.0.19041.0`, `win-x64`; nullable, warnings-as-errors and package locks remain enabled.
- Apply all Rulings in `2026-09-15-videograbber-platform-program.md`. Implement only the files assigned to the current task; audit fixes in the shared checkout may be in flight.
- Real credentials, provisioning, external messages, production migration, publication and payments require their separately authorized live gate. Local emulators and disposable test data require no production access.

---

## Increment and prerequisites

P1+P2 local gates are required. Covers VG-GAP-009/031 plus client portions of 003/008. Existing audit repairs may touch the same MainWindow partials: re-read current git diff and preserve them. The account browser is the system browser; media discovery WebView2 keeps its own site sessions. No new UI framework. Add the DPAPI package centrally after dependency qualification; do not install during planning.

## Ownership map

Modify `src/VideoGrabber.App/VideoGrabber.App.csproj`, `src/VideoGrabber.Core/VideoGrabber.Core.csproj`, `src/VideoGrabber.Infrastructure/VideoGrabber.Infrastructure.csproj`, `MainWindow.xaml.cs` (BuildShell, Download page, RunEditAsync), `MainWindow.Download.cs` (DownloadSourceAsync), `MainWindow.BatchDownload.cs` (DownloadCandidateAsync, DownloadQueuedCandidatesAsync, DownloadAllVisibleCandidatesAsync), `MainWindow.MediaActions.cs` (RunLocalMediaAsync), and `scripts/Build-Release.ps1` for separate output naming. Core references Platform.Contracts for lease DTOs; Infrastructure references Contracts for HTTP DTOs; neither references Platform.Api/Persistence. Add focused files below; do not rewrite the shell. Existing `IVideoDownloader.DownloadAsync(DownloadRequest,IProgress<DownloadProgress>?,CancellationToken)` and `DownloadResult(bool Success,string Message,string? OutputPath,string? Details)` stay usable unchanged by local edition.


### Task 1: Testable managed operation coordinator and separate build identity

**Files — Create:** `src/VideoGrabber.Core/Licensing/ManagedOperationCoordinator.cs`, `IManagedAccessClient.cs`, `ManagedOperation.cs` in that directory; `src/VideoGrabber.Infrastructure/Licensing/LicensingApiClient.cs`; `tests/VideoGrabber.Infrastructure.Tests/ManagedOperationTests.cs`. Modify App csproj, named MainWindow operation entry points, and Build-Release.ps1.

**Interfaces:**
```csharp
public sealed record ManagedOperation(Guid IntentId, string RequestHash, string Kind,
    string Executor, Guid? DeviceId);
public sealed record OperationPermit(Guid IntentId, Guid? ReservationId, bool Offline);
public interface IManagedAccessClient
{
    Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken);
    Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken);
}
public sealed class ManagedOperationCoordinator(IManagedAccessClient access)
{
    public async Task<T> RunAsync<T>(ManagedOperation operation,
        Func<CancellationToken,Task<T>> run, CancellationToken cancellationToken)
    {
        var permit = await access.AuthorizeAsync(operation, cancellationToken);
        try { var value = await run(cancellationToken); return value; }
        catch (OperationCanceledException) { await access.ReportAsync(permit, "cancel_requested", CancellationToken.None); throw; }
    }
}
```
Complete reporting in GREEN: caller provides truthful verified media result; an HTTP failure is never silently converted into an offline credit permit. ApiClient uses P2 AccessSnapshot and ReservationReceipt via a new project reference to Platform.Contracts only (no server dependencies).

- [ ] **RED — Add a fake IManagedAccessClient which throws UnauthorizedAccessException; assert protected work never begins.**
```csharp
[Fact]
public async Task Missing_access_prevents_the_download_delegate()
{
    var started = false;
    var coordinator = new ManagedOperationCoordinator(new DeniedAccessClient());
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => coordinator.RunAsync(
        new ManagedOperation(Guid.NewGuid(), "hash", "download", "desktop_worker", Guid.NewGuid()),
        _ => { started = true; return Task.FromResult(0); }, CancellationToken.None));
    Assert.False(started);
}
private sealed class DeniedAccessClient : IManagedAccessClient
{
    public Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken)
        => throw new UnauthorizedAccessException("no_grant");
    public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```
Add theory for direct, browser candidate, selected/all queue, edit, MP3, transcription and server-bound commands. Assert cancellation before admission does not reserve; signed-out shell remains readable.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Infrastructure.Tests/VideoGrabber.Infrastructure.Tests.csproj --filter FullyQualifiedName~ManagedOperationTests`.
- [ ] **GREEN — Wire one coordinator at the common execution boundary and guard earlier expensive protected preparation.** Admission before tool process, cookies export or server processing; queue rechecks at each new intent. Use an already-issued permit when an outer browser/queue call reaches DownloadSourceAsync so it cannot reserve twice. Report successful result only after existing media verification; failures are reports, not unilateral refund. Finally blocks enable actions according to current gate, not blindly true.

Build selection:
```xml
<PropertyGroup>
  <VideoGrabberEdition Condition="'$(VideoGrabberEdition)' == ''">Local</VideoGrabberEdition>
</PropertyGroup>
<PropertyGroup Condition="'$(VideoGrabberEdition)' == 'Managed'">
  <DefineConstants>$(DefineConstants);VIDEOGRABBER_MANAGED</DefineConstants>
  <AssemblyName>VideoGrabber.Managed</AssemblyName>
</PropertyGroup>
```
Startup constructs Local bypass coordinator only in Local compile branch; Managed has no config/env switch that silently disables licensing. Use separate settings/queue directories and package names. New editor/ASR requests obey R025; local media browsing/opening and completed files remain available.
- [ ] **GREEN run:** filtered tests then `dotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release -p:VideoGrabberEdition=Managed` and same with Local. Assert both independent output identities.
- [ ] **Commit:** exact files; `git commit -m "feat: gate managed desktop operations"`.

### Task 2: Browser sign-in, DPAPI session storage and offline validation

**Files — Create:** `src/VideoGrabber.Infrastructure/Licensing/WindowsSessionStore.cs`, `SystemBrowserSignIn.cs`, `OfflineAccessCache.cs` in same folder; `src/VideoGrabber.Core/Licensing/LeaseVerifier.cs`; `tests/VideoGrabber.Infrastructure.Tests/ManagedSessionTests.cs`; `tests/VideoGrabber.Core.Tests/LeaseVerifierTests.cs`. Modify LicensingApiClient.cs and central package versions.

**Interfaces:** WindowsSessionStore `SaveAsync(byte[] secret,CancellationToken)`→Task, `ReadAsync(CancellationToken)`→Task<byte[]?>; SystemBrowserSignIn `SignInAsync(CancellationToken)`→Task<ApiSession> consumes P1 BeginSignIn/CompleteSignIn. `LeaseVerifier.Validate(SignedOfflineLease lease,IReadOnlyDictionary<string,string> publicKeys,Guid accountId,Guid deviceId,DateTimeOffset now,DateTimeOffset lastSeenUtc)` returns P2 OfflineLeaseClaims or throws. OfflineAccessCache exposes `TryAuthorize(ManagedOperation operation,DateTimeOffset now,out OperationPermit? permit)`→bool.

- [ ] **RED — Add signature/account/device/expiry/clock rollback tests with ephemeral ECDsa keys.**
```csharp
[Fact]
public void Clock_rollback_cannot_extend_an_offline_lease()
{
    var lastSeen = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    Assert.Throws<UnauthorizedAccessException>(() =>
        LeaseVerifier.Validate(new SignedOfflineLease("synthetic", "key-1"),
            new Dictionary<string,string>(), Guid.NewGuid(), Guid.NewGuid(),
            lastSeen.AddHours(-1), lastSeen));
}
```
Add true signed positive lease, exactly-expired rejection, changed signature/alg/aud/iss/version/kid, fresh key rotation, credit-only denial, another Windows user cannot unprotect cache, refresh replay revoked. Define test signing helper in LeaseVerifierTests using ECDsa.Create and JWT codec used by P2.
- [ ] **RED run:** Core.Tests filter LeaseVerifierTests and Infrastructure.Tests filter ManagedSessionTests.
- [ ] **GREEN — Use CurrentUser DPAPI and atomic same-directory replacement.**
```csharp
var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
    secret, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
await File.WriteAllBytesAsync(stagingPath, protectedBytes, cancellationToken);
File.Move(stagingPath, sessionPath, overwrite: true);
```
stagingPath/sessionPath are private per-user managed app paths computed in WindowsSessionStore; no plaintext backup. Session refresh token, device private key and lease cache use separate files and restrictive user ACL. Store last successful server UTC and local monotonic checkpoint; reject wall-clock rollback >2 minutes and require online validation after reboot with a suspicious clock, rather than extending expiry. DPAPI/in-memory protections cannot make a hostile local client unbreakable; server balances remain authoritative.

Launch provider flow through ProcessStartInfo UseShellExecute=true; loopback listener binds 127.0.0.1 ephemeral port, exact path, one state-bound completion, timeout five minutes. Server callback redirects only to the allowlisted loopback scheme/host and initiated port stored in the flow; reject external redirect. Never use media WebView2 for account login. Logout revokes API session and removes only protected session cache through explicit sign-out semantics, never user media.
- [ ] **GREEN run:** tests, then actual managed EXE sign-in using emulator, restart and offline lease acceptance; capture 24h/72h logical-clock evidence without changing Windows system time.
- [ ] **Commit:** exact files; `git commit -m "feat: protect managed sessions and offline leases"`.

### Task 3: Account page and durable safe queue

**Files — Create:** `src/VideoGrabber.App/MainWindow.Account.cs`; `src/VideoGrabber.Core/Licensing/SavedQueueItem.cs`; `src/VideoGrabber.Infrastructure/Licensing/ManagedQueueStore.cs`; `tests/VideoGrabber.Infrastructure.Tests/ManagedQueuePersistenceTests.cs`. Modify BuildShell and browser queue handlers in MainWindow.xaml.cs/MainWindow.BatchDownload.cs. Existing `BrowserDownloadQueue` is in-memory and remains the UI ordering engine.

**Interfaces:**
```csharp
public sealed record SavedQueueItem(Guid IntentId, string PageOriginPath, string MediaIdentity,
    int Ordinal, string Quality, string OutputMode, string State);
public sealed record SavedQueue(int Version, Guid AccountId, SavedQueueItem[] Items);
```
ManagedQueueStore `SaveAsync(SavedQueue,CancellationToken)`→Task and `LoadAsync(Guid accountId,CancellationToken)`→Task<SavedQueue>. Storage version=1. Source refresh is an explicit MainWindow action using existing browser discovery; no file or provider secret is stored inside queue items.

- [ ] **RED — Add save/reload order, mixed account, malformed JSON, interrupted write and sensitive URL tests.**
```csharp
[Fact]
public async Task Restored_queue_requires_revalidation_and_keeps_intent()
{
    var root = Path.Combine(Path.GetTempPath(), "vg-queue-test-" + Guid.NewGuid().ToString("N"));
    var store = new ManagedQueueStore(root);
    var account = Guid.NewGuid(); var intent = Guid.NewGuid();
    await store.SaveAsync(new SavedQueue(1, account,
        [new(intent, "https://school.example/lesson", "part-2", 2, "720p", "video", "pending")]),
        CancellationToken.None);
    var restored = await store.LoadAsync(account, CancellationToken.None);
    Assert.Equal(intent, restored.Items.Single().IntentId);
    Assert.Equal("needs_revalidation", restored.Items.Single().State);
}
```
Temporary test directory remains owned by test; cleanup only targets that verified directory. Assert tokens/query/cookies are absent from serialized content; foreign account load returns no items and never executes them.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Infrastructure.Tests/VideoGrabber.Infrastructure.Tests.csproj --filter FullyQualifiedName~ManagedQueuePersistenceTests`.
- [ ] **GREEN — Implement atomic JSON persistence and account UI.**
```csharp
var restoredItems = saved.Items.Select(item => item with { State = "needs_revalidation" }).ToArray();
return new SavedQueue(1, accountId, restoredItems);
```
Before writing validate HTTPS page origin/path, remove query/fragment/userinfo, reject unrecognized state, quality length>32, >500 items, nonmatching account and relative local paths. Persist on add/reorder/remove/state transitions. On crash leave last good snapshot and quarantine corrupt queue by renaming within its managed directory, no auto-delete. Continue button performs fresh discovery and asks for a new selection if media identity/quality cannot be matched; no stale signed URL replay or silent new debit.

Account page uses BuildAccountPage() in the new partial, with sign-in/out, provider link/unlink, access expiry/credits/reason, devices/revoke, Telegram linked state and offline deadline. Dispatch UI changes onto DispatcherQueue; show busy/empty/network error without falsely enabling protected actions. Preserve author/contact card in the shell.
- [ ] **GREEN run:** targeted tests plus actual EXE: queue two items with different qualities, restart, observe needs_revalidation, Continue, cancel, no auto-start; account A→B never inherits queue or lease. Test UI scaling/keyboard at 100%/150% and narrow window.
- [ ] **Commit:** exact files; `git commit -m "feat: add managed account page and durable queue"`.

## Acceptance

Run all Core/Infrastructure/Platform tests and both build editions; sign out and exercise direct/browser/queue/edit/ASR gates through real UI; sign in and gift access using P2 admin, verify displayed exact balance. Use network fault to prove only valid signed time leases allow work. Test expired/blocked/revoked cases preserve existing files and that a successful job consumes one reservation only.

Publish only to local artifact folders for GUI/clean-Windows acceptance; do not tag/upload. A build or source string test cannot substitute for actual EXE behavior. Save test/GUI evidence separately; P5 desktop companion enrollment must use this same device/session/cache and queue intent contract.
