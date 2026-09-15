# VideoGrabber Entitlements, Ledger, Gifts and Device Leases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a usable gift-based platform with deterministic rights, a transactional shared credit balance, admin operations and signed Windows leases.

**Architecture:** Pure policy code lives in Platform.Core; transactional SQL state and immutable journal live in Persistence. HTTP/admin UI use the same account context and current database role; no client-supplied role or balance is trusted.

**Tech Stack:** .NET 10, PostgreSQL/Npgsql, built-in ECDsa, ASP.NET Core static admin UI, existing xUnit.

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

P1 local acceptance is required. Covers VG-GAP-005/006/007/008/010/021/025/026/028/029. This increment works through a real admin page and HTTP gift/reservation endpoints before desktop, Telegram or payments. No payment integration is needed. No additional package is required beyond P1. P1 owns ApiFixture, AccountProfile, the migration runner and account/auth interfaces; every test below imports their namespaces.

## Shared contracts (created by Task 1)

`src/VideoGrabber.Platform.Contracts/AccessContracts.cs` owns:
```csharp
public sealed record AccessSnapshot(bool CanDownload, bool CanEdit, bool Unlimited,
    DateTimeOffset? ValidUntil, long RemainingDownloads, DateTimeOffset? OfflineUntil, string Reason);
public sealed record GrantRequest(Guid AccountId, string Kind, int Days, long Credits,
    DateTimeOffset? ExpiresAt, string Reason, Guid IdempotencyKey);
public sealed record GrantReceipt(Guid GrantId, Guid AccountId, string Source);
public sealed record ReservationRequest(Guid IntentId, string RequestHash, string Operation,
    string Executor, Guid? DeviceId);
public sealed record ReservationReceipt(Guid ReservationId, Guid IntentId, string State,
    bool UsesCredit, DateTimeOffset ExpiresAt);
public sealed record FinalizeReservation(Guid ReservationId, Guid AttemptId,
    long Fence, string Outcome, string EvidenceId);
```
`src/VideoGrabber.Platform.Core/Access/Grant.cs` defines `Grant(Guid Id,string Kind,string Source,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,long Available,long Reserved,bool Revoked,DateTimeOffset CreatedAt)`.


### Task 1: Deterministic access and real admin gifts

**Files — Create:** AccessContracts.cs and Grant.cs above; `src/VideoGrabber.Platform.Core/Access/AccessEvaluator.cs`; `src/VideoGrabber.Platform.Persistence/GrantStore.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/010_grants.sql`; `src/VideoGrabber.Platform.Api/Access/AccessEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/EntitlementTests.cs`. Modify API Program.cs.

**Interfaces:** `AccessEvaluator.Evaluate(AccountProfile account,IReadOnlyList<Grant> grants,DateTimeOffset now)` returns AccessSnapshot. GrantStore exposes `Task<GrantReceipt> GiftAsync(Guid adminId,GrantRequest,CancellationToken)` and `Task<AccessSnapshot> EvaluateAsync(Guid accountId,CancellationToken)`. Endpoints GET /v1/access and POST /v1/admin/grants use those methods, admin fresh MFA required.

- [ ] **RED — Write evaluator and gift HTTP tests.**
```csharp
[Fact]
public async Task Gifted_guest_keeps_role_and_has_exact_credits()
{
    await using var f = await ApiFixture.StartAsync();
    var guest = await f.AccountAsync("telegram", "guest-1");
    var admin = await f.AdminAsync();
    var response = await admin.PostAsJsonAsync("/v1/admin/grants",
        new GrantRequest(guest.Id, "credits", 0, 3, null, "acceptance gift", Guid.NewGuid()));
    response.EnsureSuccessStatusCode();
    var profile = await guest.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
    var access = await guest.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
    Assert.Equal("guest", profile!.Role);
    Assert.Equal(3, access!.RemainingDownloads);
    Assert.False(access.CanEdit);
}
```
Also write boundary tests for now==expiry, future grant, blocked owner, hybrid=3 downloads before expiry (not unlimited days), owner without depletion, time extension across contiguous windows and earliest-expiry credit selection.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~EntitlementTests`.
- [ ] **GREEN — Implement pure evaluator and actual SQL grant insert.** Its ordered decision begins:
```csharp
if (account.Blocked)
    return new(false, false, false, null, 0, null, "account_blocked");
if (account.Role == "owner_admin")
    return new(true, true, true, null, 0, now.AddHours(72), "owner_unlimited");
var active = grants.Where(g => !g.Revoked && g.StartsAt <= now &&
    (g.EndsAt is null || now < g.EndsAt)).ToArray();
var permanent = active.Any(g => g.Kind == "permanent");
var timeEnd = active.Where(g => g.Kind == "time").Select(g => g.EndsAt).DefaultIfEmpty(null).Max();
var credits = active.Where(g => g.Kind is "credits" or "hybrid").Sum(g => g.Available);
var timed = permanent || timeEnd > now;
var offline = timed ? (timeEnd is { } end && end < now.AddHours(24) ? end : now.AddHours(24)) : (DateTimeOffset?)null;
return new(timed || credits > 0, timed, permanent, timeEnd, credits, offline,
    timed ? "time_access" : credits > 0 ? "online_credit_required" : "no_grant");
```
Empty time grants return null. Grants table includes account FK, kind/source checks, available>=0, reserved>=0, total>=0, grantId, valid_from/until, revoked_at. Unique (admin_id,idempotency_key) plus canonical payload hash: same key/same body returns original receipt, changed body 409. Grant add + ledger add + audit event share one transaction (ledger is introduced in Task 2; first task adds grant audit with original amount retained). Gift source is always admin_gift, never purchase. Admin permanent rights are an explicit rule and bootstrap audit, never 999999 credits.
- [ ] **GREEN run:** repeat targeted tests; build Platform.Api. Inspect a gift through GET /v1/access and /v1/me.
- [ ] **Commit:** exact files; `git commit -m "feat: evaluate access and grant admin gifts"`.

### Task 2: Reserve/commit/release with real concurrent PostgreSQL accounting

**Files — Create:** `src/VideoGrabber.Platform.Persistence/CreditLedger.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/011_ledger_reservations.sql`; `src/VideoGrabber.Platform.Api/Access/ReservationEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/LedgerConcurrencyTests.cs`; `tests/VideoGrabber.Platform.Tests/LedgerConservationTests.cs`. Modify GrantStore.cs to append original credit issuance.

**Interfaces:** `CreditLedger.ReserveAsync(Guid accountId,ReservationRequest,CancellationToken)` returns Task<ReservationReceipt>; `FinalizeAsync(Guid accountId,FinalizeReservation,CancellationToken)` returns Task<ReservationReceipt>; `ReleaseUnstartedAsync(Guid reservationId,CancellationToken)` returns Task<bool>. Public POST /v1/reservations accepts account context, not AccountId in body. Finalize endpoint requires scoped worker attempt capability from P5; in this increment only an internal test worker issuer can create a valid capability, never an ordinary user `success=true` body. Desktop request reports can enter review_required but cannot mint release proof.

- [ ] **RED — Write 100-way race and conservation tests.**
```csharp
[Fact]
public async Task Last_credit_is_reserved_once_across_one_hundred_requests()
{
    await using var f = await ApiFixture.StartAsync();
    var user = await f.AccountAsync("telegram", "shared-last-credit");
    var admin = await f.AdminAsync();
    (await admin.PostAsJsonAsync("/v1/admin/grants",
        new GrantRequest(user.Id, "credits", 0, 1, null, "race", Guid.NewGuid()))).EnsureSuccessStatusCode();
    var tasks = Enumerable.Range(0, 100).Select(i => user.Client.PostAsJsonAsync(
        "/v1/reservations", new ReservationRequest(Guid.NewGuid(), i.ToString("D64"),
            "download", "server_worker", null)));
    var responses = await Task.WhenAll(tasks);
    Assert.Single(responses.Where(r => r.IsSuccessStatusCode));
    var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
    Assert.Equal(0, access!.RemainingDownloads);
}
```
This first test uses server reservations so Task 2 has no dependency on Task 3. In Task 3 extend it with an actually registered ECDsa device, alternating desktop_worker requests with its DeviceId and server_worker requests without a DeviceId; assert exactly one winner across both clients. Run at least once with 20 and once with 100 concurrent requests, no in-memory lock as the correctness mechanism. Add replay same intent, changed payload 409, expiry-after-reserve, revoke-after-reserve, duplicate commit/release, failed transaction rollback and owner zero journal debit.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter "FullyQualifiedName~LedgerConcurrencyTests|FullyQualifiedName~LedgerConservationTests"`.
- [ ] **GREEN — In one transaction lock account and grant, create reservation and immutable event.**
```sql
select account_id from licensing.accounts where account_id=@account for update;
select grant_id from licensing.entitlement_grants
where account_id=@account and kind in ('credits','hybrid')
 and available > 0 and revoked_at is null and valid_from <= @now
 and (valid_until is null or valid_until > @now)
order by valid_until nulls last, created_at, grant_id
for update limit 1;
update licensing.entitlement_grants
set available=available-1,reserved=reserved+1
where grant_id=@grant and available>0;
```
Recheck blocked/access while locked. Reservations unique(account_id,intent_id); hash immutable; no charge for repeat. Ledger rows have UUID, account/grant/reservation IDs, event kind, integer available/reserved/spent/void deltas, actor/evidence/reason, created_at. Database denies UPDATE/DELETE to API, worker and admin roles. Each event unique(reservation_id,event_kind) where applicable. Commit moves reserved→spent; release moves reserved→available only if original grant remains active/unrevoked, otherwise reserved→void. Sum all buckets equals issued+audited adjustments. A time/owner admission returns UsesCredit=false and no credit debit. Unknown desktop failures retain reserved bucket as review_required. Retry 40001/40P01 at most three times with jitter; after exhaustion 503, never start work.
- [ ] **GREEN run:** same filtered suites; assert persisted rows and bucket invariants after each crash/replay fixture, not only HTTP responses.
- [ ] **Commit:** exact files; `git commit -m "feat: make shared credit reservations transactional"`.

### Task 3: Device slots and signed offline lease issuance

**Files — Create:** `src/VideoGrabber.Platform.Contracts/DeviceContracts.cs`; `src/VideoGrabber.Platform.Persistence/DeviceStore.cs`; `src/VideoGrabber.Platform.Api/Access/OfflineLeaseService.cs`; `src/VideoGrabber.Platform.Api/Access/DeviceEndpoints.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/012_devices_leases.sql`; `tests/VideoGrabber.Platform.Tests/DeviceLeaseTests.cs`. Modify reservation endpoints to require registered desktop device.

**Interfaces:**
```csharp
public sealed record DeviceRegistration(string Name, string Platform, string PublicKey, Guid IdempotencyKey);
public sealed record DeviceReceipt(Guid DeviceId, string Name, bool Revoked);
public sealed record OfflineLeaseClaims(Guid AccountId, Guid DeviceId, Guid LeaseId,
    int Version, string RoleClass, string[] Features, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
public sealed record SignedOfflineLease(string Token, string KeyId);
```
DeviceStore `RegisterAsync(Guid,DeviceRegistration,CancellationToken)`→Task<DeviceReceipt>, `RevokeAsync(Guid,Guid,CancellationToken)`→Task. OfflineLeaseService `IssueAsync(Guid accountId,Guid deviceId,CancellationToken)`→Task<SignedOfflineLease>. Routes POST/GET /v1/devices, POST /v1/devices/{id}/revoke, POST /v1/devices/{id}/lease, GET /v1/lease-keys.

- [ ] **RED — Write guest/User 1, owner 3, concurrency and expiry tests.**
```csharp
[Theory]
[InlineData(24)][InlineData(72)]
public void Lease_maximum_is_a_hard_boundary(int hours)
{
    var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    var end = OfflineLeaseService.BoundExpiry(now, now.AddDays(7), hours == 72);
    Assert.Equal(now.AddHours(hours), end);
    Assert.Equal(now.AddHours(1), OfflineLeaseService.BoundExpiry(now, now.AddHours(1), hours == 72));
}
```
Add ten simultaneous registrations with unique keys (one/three succeed), replay same enrollment returns same ID, telegram no slot, revoked device cannot refresh lease, copied device ID with wrong private key rejected, signed lease contains no RemainingDownloads, issuer/audience/kid/alg/version tamper rejected.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~DeviceLeaseTests`.
- [ ] **GREEN — Lock account row to count active Windows devices and register server UUID.** Store friendly name <=80 chars, platform windows, public key, timestamps, revoked_at; never raw hardware identity. Require device private-key proof of nonce on refresh; nonce single-use, expires after 60s. Lease signing key stays server-side. This task tests signed claims with a test verifier; P3 owns the production desktop LeaseVerifier. Add the mixed desktop/server last-credit race defined in Task 2 after device registration is available.
```csharp
public static DateTimeOffset BoundExpiry(DateTimeOffset now, DateTimeOffset? grantEnd, bool admin)
{
    var maximum = now.AddHours(admin ? 72 : 24);
    return grantEnd is { } end && end < maximum ? end : maximum;
}
```
Bound issue time to actual server clock and earliest effective time end; credit-only accounts cannot receive a download lease. Claims issuer videograbber-licensing, audience videograbber-managed, ECDSA P-256 signature, kid, version=1, account/device/lease IDs and feature list only. Keep metadata/hash in offline_leases, not raw token secrets. Revocation increments account/device lease version for online checks; offline enforcement is bounded by expiry. Include keys until last issued lease under that key expires plus five minutes; reject unknown kid/algorithm.
- [ ] **GREEN run:** repeat tests; inspect decoded synthetic lease has minimum claims and no balance.
- [ ] **Commit:** exact files; `git commit -m "feat: enforce device limits and signed offline access"`.

### Task 4: Working admin console, support adjustments and account merge

**Files — Create:** `src/VideoGrabber.Platform.Api/Admin/AdminEndpoints.cs`; `AdminService.cs` in same folder; `src/VideoGrabber.Platform.Api/wwwroot/admin/index.html`, `admin.js`, `admin.css`; `src/VideoGrabber.Platform.Persistence/Migrations/013_admin_adjustments.sql`; `tests/VideoGrabber.Platform.Tests/AdminWorkflowTests.cs`; `scripts/platform/Set-PlatformOwner.ps1`; `docs/platform/admin-runbook.md`. Modify P1 IdentityLinkService.cs for full financial merge and audit contract.

**Interfaces:** AdminService methods `BlockAsync(Guid actorId,Guid targetId,bool blocked,string reason,CancellationToken)`, `RevokeGiftAsync(Guid actorId,Guid grantId,string reason,CancellationToken)`, `ResetDevicesAsync(Guid actorId,Guid targetId,string reason,CancellationToken)`, `AdjustAsync(Guid actorId,Guid reservationId,string evidenceId,string reason,CancellationToken)`, all Task. Routes GET /v1/admin/accounts?identity=, GET /v1/admin/accounts/{id}, POST /block, /unblock, /devices/reset, /grants/{id}/revoke, /reservations/{id}/adjust, GET /audit. Sensitive actions use fresh server-verified MFA and append actor/target/before/after/reason/correlation.

- [ ] **RED — Add complete gift→reserve→revoke→block→unblock scenario and non-admin attacks.**
```csharp
[Fact]
public async Task Normal_user_cannot_gift_even_with_forged_admin_fields()
{
    await using var f = await ApiFixture.StartAsync();
    var user = await f.AccountAsync("google", "ordinary");
    var response = await user.Client.PostAsJsonAsync("/v1/admin/grants",
        new { accountId = user.Id, role = "owner_admin", kind = "credits",
              credits = 100, reason = "forged", idempotencyKey = Guid.NewGuid() });
    Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
}
```
Add MFA absent/stale rejection, first signup guest, blocked owner deny, immutable payment/grant history, merge two accounts with active reservations rejected until terminal; completed history remains tied to original account but target gets audited ownership mapping without double issuance.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~AdminWorkflowTests`.
- [ ] **GREEN — Serve an actual admin UI with lookup and result panels.** Use authenticated fetch, anti-CSRF token, reason input, readback after each mutation, loading/empty/error states, keyboard labels, responsive 360px layout. No operation mutates on GET. Example rendering uses textContent:
```javascript
const result = await fetch("/v1/admin/accounts/" + encodeURIComponent(accountId), {
  credentials: "same-origin", headers: { "Accept": "application/json" }
});
if (!result.ok) throw new Error("Не удалось загрузить аккаунт");
const account = await result.json();
document.querySelector("#account-role").textContent = account.role;
```
Bootstrap script requires explicit AccountId, target environment and current MFA-admin provisioning authorization; it refuses owner assignment based on first signup, email or query parameter. For local tests only, AdminAsync seeds explicit account as owner. Merge acquires both account locks in UUID order, rejects blocked/conflicting active reservations, transfers only active unreserved remaining value with equal debit/credit transfer events, redirects identities after two proofs, preserves old payment/ledger account IDs and audit source; no history UPDATE. UI displays merged source history via authorized relation.
- [ ] **GREEN run:** full Platform.Tests; browser acceptance: find guest, gift seven days and three separate credits, reserve one, revoke unused gift, block/unblock, reset device, inspect audit. At 360px all controls remain operable. Save screenshots without account personal data.
- [ ] **Commit:** exact files; `git commit -m "feat: add audited admin access workflows"`.

## Acceptance

Real PostgreSQL proves one winner for the shared last credit; a replay cannot mint access; expired/revoked releases never resurrect credits; guest remains guest after gift; owner uses explicit unlimited; block preserves purchaser history; device and offline lease boundaries pass. The admin console completes the support workflow with MFA and audit evidence. Existing local regression tests stay green.

Run `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj -c Release`, Core.Tests and Infrastructure.Tests, then build API. P3 consumes AccessSnapshot, DeviceReceipt, SignedOfflineLease, reservation endpoints and public lease keys; P4 consumes the same profile/admin routes. Live offline clock/device/private-key behavior remains P3 acceptance, not a claim of this server-only phase.
