# VideoGrabber Platform Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the specified account platform through seven working, independently accepted increments.

**Architecture:** Keep the existing Windows downloader and add a .NET modular API with a separate PostgreSQL business database. Supabase Auth is a provider broker; licensing, accounting, jobs, Telegram and payments belong to the API. Clients share versioned contracts without sharing site sessions.

**Tech Stack:** Existing .NET 10/C# 14/WinUI 3; ASP.NET Core; Npgsql SQL migrations; xUnit; HTML/CSS/JavaScript Mini App; isolated media workers.

**Spec:** `docs/superpowers/specs/2026-09-15-videograbber-platform-auth-licensing-design.md`, fully read 2026-09-15. Audit inputs: `04-platform-gaps.md`, `07-security-payments.md`, `08-release-readiness.md`, `10-final-report-ru.md` in the audit artifact directory named in the execution report.

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

## Baseline and scope check

At planning time the solution is `VideoGrabber.slnx`, with App/Core/Infrastructure and Core.Tests/Infrastructure.Tests only. There is no server API, database migration, product bot, managed account gate, ledger, payment adapter or worker service. Existing `scripts/Analyze-Logs.ps1` Telegram messaging is diagnostic only. All platform paths below are **new** unless explicitly marked Modify.

The broad approved architecture spans independent subsystems. Scope Check therefore produces seven separate implementation plans plus this index; none is a promise that the absent implementation already works. Existing 25 confirmed local defects, the media-edit hypothesis and the four outstanding live/GUI/provenance checks remain a separate repair/release track. A plan's written coverage does not close its audit gap.

## Execution order and acceptance owners

|Plan|Working increment|Depends on|Primary gaps|
|---|---|---|---|
|[P1 Foundation](2026-09-15-videograbber-platform-foundation.md)|Account/profile/link/recovery API and isolated PostgreSQL; provider emulators|Local tool/dependency gate|001,002,003,004,027|
|[P2 Access](2026-09-15-videograbber-platform-entitlements.md)|Usable gift/admin console, shared ledger, Windows registration and leases|P1 local acceptance|005,006,007,008,010,021,025,026,028,029|
|[P3 Managed desktop](2026-09-15-videograbber-platform-managed-desktop.md)|Separate managed EXE, account gate and restart-safe local queue|P1+P2|009,031; 003,008|
|[P4 Telegram](2026-09-15-videograbber-platform-telegram.md)|Secure bot/Mini App accounts, gifts/admin and destination management|P1+P2; media controls become usable in P5|012,014; 006,013,021|
|[P5 Workers/delivery](2026-09-15-videograbber-platform-workers-delivery.md)|Durable media jobs, server and desktop workers, every parity operation and large-file delivery|P2+P3+P4; local media security fixes verified|011,013,015,016,017,030|
|[P6 Payments](2026-09-15-videograbber-platform-payments.md)|Stars + external sandbox adapter, refund/reconciliation/renewal|P2+P4+P5 gifts end-to-end acceptance|018,019,020|
|[P7 Operations](2026-09-15-videograbber-platform-recovery-deployment.md)|Reproducible isolated deployment, backup/restore, monitors and release proof|P1–P6|022,023,024|

001 means VG-GAP-001; all 31 unique audit gaps have a concrete destination above. P1 and P2 can be used through their HTTP/admin UI without either client. P3 works against P2 before server workers exist. P4 exposes working account/admin/destination functions and reports media capability unavailable until P5; it does not pretend inert media buttons satisfy parity. P6 does not hold gifts hostage.

## Rulings: executable safe defaults

These resolve the six policy gaps for implementation under the approved program. They are versioned behavior, never an inference that live integrations passed.

### R025 — Logical item and prices (VG-GAP-025)

One new user-confirmed download intent for one selected media item and quality costs one credit when no time/unlimited grant applies. Use a client-created UUID `intent_id`, persist it before dispatch, and keep it through retry/resume. Canonical request hash covers account, media identity, selected quality, output mode and executor; an existing key with different bytes gives HTTP 409. A second intentional quality is a new intent and costs one credit; the confirmation UI states this. Six independent parts create six intents. HLS fragments, retry, transport split and re-send cost zero extra. Analyze/status/queue management are free. MP3 extraction from an owned retained result, trim, join and ASR require active time/unlimited access and cost zero download credits; credit-only access rejects these with `time_access_required`. No unpriced operation runs. **Cost of error:** conservative denial for credit-only editing; no undisclosed billing.

### R026 — Started jobs, expiry, block and ambiguity (VG-GAP-026)

Check authorization again at admission and attempt start. A job admitted under time access may finish its current attempt after natural expiry; a new retry requires current rights. A reserved credit remains attributable to its original grant even if that grant expires. Successful verified output atomically commits that reservation. Block/revoke prevents new admission/attempts immediately; running online work receives cancellation at heartbeat (15s, lease 60s), private delivery is withheld while blocked. If success wins the database finalization race, charge once and retain the private artifact; if confirmed cancellation wins, release only to the original still-valid grant. Offline work can continue only within a signed lease and cannot initiate credit work. Ambiguous desktop failure enters `review_required`, retaining the hold: only independent proof of pre-start failure or an audited admin adjustment resolves it. **Cost of error:** credit can remain held for support; a dishonest failure cannot mint credits.

### R027 — Supabase email isolation (VG-GAP-027)

Use **five isolated Supabase Auth projects/instances**, one each for Google, Apple, Yandex, Telegram, verified email. Social partitions disable email/password/OTP registration and every other identity provider; the email partition has no social providers. Never call upstream identity-link APIs. Resolve the business identity by validated broker issuer + immutable provider subject, never email or an unqualified Supabase user ID. Freeze broker user-to-provider-subject bindings; reject changed bindings, unexpected providers, multi-identity broker users and uncertain Telegram OIDC-to-numeric-ID mappings. Business links are created only by two fresh proofs in the Licensing API. This topology avoids ordinary cross-provider email auto-link and has explicit live same-email collision tests, including two distinct subjects within one partition; any failed collision test disables that provider. A complete deterministic issuer emulator provides local work; it is never accepted outside `Development`. **Cost of error:** five broker environments and fail-closed account denial; no claim that default Supabase auto-link is safe.

### R028 — Block and purchaser history (VG-GAP-028)

Persist `base_role` as guest/user/owner_admin and `blocked_at` separately. Effective role is blocked while the flag is set; unblock restores the preserved base role. The first verified purchase sets `first_purchase_at` and guest→user permanently; refund or expiry does not erase purchaser history and does not preserve expired access. Admin bootstrap requires explicit internal account ID from an offline administrative command; no first-signup owner. **Cost of error:** a refunded account keeps the harmless user label, not a credit or download right.

### R029 — Grant order, extension and revocation (VG-GAP-029)

Block precedes all grants; then explicit owner unlimited, permanent grants, active time windows, then credit/hybrid grants ordered by earliest expiry (null last), creation time and UUID. Time extensions append grants starting at max(now, latest contiguous active time end); independent packages keep their stated valid_from. A hybrid grant means credits usable before expiry, not unlimited time access. Gifts never change purchase grants. Revoke removes only unreserved unspent availability; an existing reservation keeps its original grant reference and follows R026. Releases from expired/revoked grants enter expired/void buckets, never available. Payments/refunds append adjustments, never rewrite historical entries; spent refunds do not create a negative usable balance. **Cost of error:** conservative availability and a visible support adjustment for already consumed refunded value.

### R030 — Uncertain Telegram delivery (VG-GAP-030)

Persist delivery attempt before send. A timeout after bytes may have reached Telegram becomes `delivery_unknown`; do not automatically send again. Reconcile from any known message/file IDs once, otherwise show an explicit user action “Повторить доставку: возможен дубликат”. That action creates a new delivery attempt for the same artifact, with a new audit reason and no new media job/reservation. Retry transient pre-send failures at 5s/30s/120s, then stop. No exactly-once delivery promise. **Cost of error:** delayed delivery/manual action or disclosed duplicate, never duplicate credit spending.

### Additional concrete contracts

- Reservation admission hold 10 minutes; working attempts heartbeat every 15 seconds with 60-second fencing lease; maximum media runtime 2 hours. Expired unstarted holds release safely; elapsed started holds require terminal proof or review, not blind refund.
- Local queue persists order, intent ID, page origin/path without sensitive query, selected media identity/quality and state. It never persists cookie/header/token/signed URL. On restart show `needs_revalidation`; explicit Continue reacquires the source and preserves the intent only if the canonical media/quality still match.
- Server artifact retention: 72 hours after completion, delivery logs 30 days, security audit 180 days; payment/ledger retention 5 years pending jurisdiction-specific compliance review before live selling. Cleanup applies only to server-owned UUID roots and never to desktop output.
- Subscription default: 30-day time access; cancellation prevents renewal but preserves the paid period. Verified refund revokes unused time, keeps history and follows R029. External automatic capture requires stored provider consent; no implicit renewal enrollment.
- API supports `v1` and the immediately previous compatible managed-client minor for at least 90 days. Unsupported protocol returns 426 `client_upgrade_required`. Local edition is outside licensing rollout.
- Profile/link limits: 10/min per account and 30/min per IP; login 5/min per IP; jobs 20/min per account; payment mutations 5/min per account. Admin sensitive actions require a server-verified MFA session no older than five minutes.

## Common task mechanics

Every task owns RED→GREEN tests and an explicit fileset commit. Existing tests use xUnit 2.9.3, not a new framework. Server tests use real disposable PostgreSQL for locking/RLS/ledger/restore; no SQLite or in-memory substitute qualifies. Test fixture guards require database name starting `vg_test_`, loopback or an explicitly passed isolated test host, and no production DSN fallback. Fixtures create new named schemas/databases and never drop shared/user data.

Run from the worktree repository, writing a fresh per-run directory under `D:\CODEX\Artifacts\videograbber-platform`. No command in these plans was executed during planning. For new dependencies verify official release/advisory status and existing .NET compatibility before restore; pin selected versions and commit lockfiles. Listed package baselines below are concrete choices, not an instruction to install now.

## Dependencies and build boundaries

|Component|New dependency selected|Purpose|
|---|---|---|
|Platform.Core, Contracts|none beyond .NET 10|Immutable records, policies, API DTOs|
|Platform.Persistence|Npgsql 10.0.0, centrally pinned|PostgreSQL transactions and migrations, no ORM|
|Platform.Api|Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0|Broker token validation; short own API sessions|
|Platform.Tests|Microsoft.AspNetCore.Mvc.Testing 10.0.0; existing xUnit packages|Hosted API integration tests|
|Managed client|System.Security.Cryptography.ProtectedData 10.0.0|Windows CurrentUser DPAPI cache|
|Bot, workers, payment adapters|no SDK dependency; typed HttpClient and System.Text.Json|Small explicit protocol adapters|
|Mini App/admin UI|no npm dependency|Native modules and responsive HTML/CSS|
|Infrastructure tools|PostgreSQL 17.6 container; existing Docker if available; official local Telegram Bot API; yt-dlp/FFmpeg/Whisper|Only disposable isolated integration; manifests pin digest/hash at qualification|

.NET 10 package baselines must pass current advisory review before implementation; a security patch in the same major may replace the baseline in the owning task with lockfile evidence. Supabase hosted partition URLs, broker versions, official Bot API binary SHA and VPS sizing are recorded by live qualification commands, not invented in this document. Existing media tool binaries do not prove Linux compatibility or redistribution readiness.

## Program verification and status

- [ ] P1: positive/negative accounts and isolation locally; each provider has a separate live verdict.
- [ ] P2: 100 concurrent last-credit requests yield one reservation; gifts/admin/device/lease acceptance.
- [ ] P3: managed EXE GUI gate, all protected paths, restart queue, unchanged local edition.
- [ ] P4: real local webhook emulator and Mini App browser account/admin tests.
- [ ] P5: server and desktop synthetic media outputs verified by ffprobe/full decode; complete parity; delivery unknown and retention.
- [ ] P6: callback/refund/renewal/reconciliation races in PostgreSQL and sandbox.
- [ ] P7: isolated restore with measured RPO/RTO and deployment/monitoring proof.
- [ ] Full existing Core/Infrastructure regression, applicable GUI and authorized lesson/clean-Windows/tool-provenance gates.
- [ ] Live evidence requires explicit target authorization; publishing requires separate authorization.

## Planning self-review

All spec sections 1–23 map as follows: 1–4→P1; 5–8→P2; 9→P3; 10→P4/P5; 11–12→P5; 13→P6; 14–16→P1/P2/P5/P6/P7; 17→P2/P3/P5; 18→P2/P4/P6; 19–20→all acceptance sections; 21–23→global constraints/P7. Audit gap count is **31 planned, zero runtime closures claimed**. Six rulings resolve planning ambiguity. Shared interface names live in the owning plan and are repeated at consumers.

## Sources checked for implementation choices

Supabase currently documents automatic email linking; provider isolation above is a design mitigation that still requires live collision tests. Its custom providers support OAuth2/OIDC and optional email. [Identity linking](https://supabase.com/docs/guides/auth/auth-identity-linking) · [Custom providers](https://supabase.com/docs/guides/auth/custom-oauth-providers).

## Audit gap-to-task verification index

|Gap|Owning tasks|Required closure evidence|
|---|---|---|
|VG-GAP-001|P1 T1|Persisted versioned account API and fresh PostgreSQL migrations|
|VG-GAP-002|P1 T2/T4|Five emulator suites plus five separate live logins|
|VG-GAP-003|P1 T3; P2 T4|Two-proof link/recovery/unlink and audited merge|
|VG-GAP-004|P1 T4; P4 T3; P5 T3/T4; P6 T1|SQL/API/events/artifact/payment A/B isolation|
|VG-GAP-005|P2 T1|Deterministic block/time/credit/owner evaluation|
|VG-GAP-006|P2 T1/T4; P4 T2|Audited gifts visible identically in clients|
|VG-GAP-007|P2 T3|Concurrent one/three device enrollment and revoke|
|VG-GAP-008|P2 T3; P3 T2|Signed 24h/72h leases and hostile clock/cache cases|
|VG-GAP-009|P3 T1/T3|Real managed EXE gates and account UI|
|VG-GAP-010|P2 T2/T3|100-way mixed-client last-credit race and conservation|
|VG-GAP-011|P5 T1|Crash-safe admission/outbox and attempt fencing|
|VG-GAP-012|P4 T1/T2|Verified webhook/initData and working product handlers|
|VG-GAP-013|P5 T4; P6 T2/T4|Every parity.csv row has actual result evidence|
|VG-GAP-014|P4 T3; P5 T5|Ownership plus live user/bot publish rights|
|VG-GAP-015|P5 T5|Actual small/large/overlimit transport and recovery|
|VG-GAP-016|P5 T2|Linux execution, quotas and nested egress containment|
|VG-GAP-017|P5 T3|Scoped desktop pull/upload with no cookie transfer|
|VG-GAP-018|P6 T2|Stars confirmation/refund/support and replay|
|VG-GAP-019|P6 T3|YooKassa verified sandbox callbacks/reconciliation|
|VG-GAP-020|P6 T4|Recurring cancel/refund/order permutations|
|VG-GAP-021|P2 T4; P4 T2|Working admin/MFA/bootstrap/audit/recovery|
|VG-GAP-022|P7 T1|Qualified manifests and real isolated readiness|
|VG-GAP-023|P7 T2|Actual encrypted backup/restore/invariants/RPO/RTO|
|VG-GAP-024|P7 T3|Measured capacity and delivered alerts|
|VG-GAP-025|R025; P2 T1/T2; P5 T4|Explicit tariff/intent policy and both client tests|
|VG-GAP-026|R026; P2 T2; P5 T1/T3|Started-job event and financial race tests|
|VG-GAP-027|R027; P1 T2/T4|Provider-isolated broker and live collision denial|
|VG-GAP-028|R028; P2 T1/T4; P6 T1|Lossless block/purchaser history|
|VG-GAP-029|R029; P2 T1/T2/T4; P6 T4|Grant order/revoke/refund conservation|
|VG-GAP-030|R030; P5 T5|delivery_unknown and explicit duplicate-aware retry|
|VG-GAP-031|P3 T3|Durable safe queue and actual restart/Continue|

All snippets use namespaces from their file path: Platform.Contracts for public DTOs, Platform.Core.<folder> for policies, Platform.Persistence for stores, Platform.Api.<folder> for endpoints/services, and the named test project namespace. Add normal using directives (System.Net.Http.Json, System.Text.Json, Xunit and referenced namespaces) in each test file. All abbreviated filenames in a Files block resolve against the immediately preceding full directory in that same block. New projects receive package lockfiles; shared public types are never imported from API internals by desktop clients.


Telegram login and Mini App assertions are different protocols; Stars grants follow confirmed payments. [Telegram login](https://core.telegram.org/bots/telegram-login) · [Stars](https://core.telegram.org/bots/payments-stars). YooKassa adapter treats notifications as a hint and verifies objects by authenticated API read. [Notifications](https://yookassa.ru/developers/using-api/webhooks).
