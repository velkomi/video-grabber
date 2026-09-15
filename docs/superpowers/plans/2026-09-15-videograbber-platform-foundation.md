# VideoGrabber Accounts and PostgreSQL Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run a versioned account/profile/link/recovery API with strict account isolation and locally testable adapters for all five identity providers.

**Architecture:** Create separate Contracts, Core, Persistence and Api projects under src; do not add server dependencies to the existing desktop Core. Keep business data in a private PostgreSQL schema; Supabase handles upstream sessions in isolated provider partitions.

**Tech Stack:** .NET 10, ASP.NET Core, Npgsql 10.0.0, JwtBearer 10.0.0, Mvc.Testing 10.0.0, existing xUnit.

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

Covers VG-GAP-001/002/003/004/027. Local completion means real account mutations and isolation through HTTP/PostgreSQL with a deterministic protocol emulator. Live provider support requires five separately recorded gates; no emulator is a substitute for them. Read the current Supabase changelog and provider docs before editing broker manifests. New packages are the four listed above; list them in Directory.Packages.props and qualify advisories before restore. No install/provision command is authorized by this planning commit.

## File map and interfaces

Create projects `src/VideoGrabber.Platform.Contracts/VideoGrabber.Platform.Contracts.csproj`, `src/VideoGrabber.Platform.Core/VideoGrabber.Platform.Core.csproj`, `src/VideoGrabber.Platform.Persistence/VideoGrabber.Platform.Persistence.csproj`, `src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj`, and `tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj`. Contracts/Core use Microsoft.NET.Sdk; API uses Microsoft.NET.Sdk.Web; all target net10.0. API references Core/Persistence/Contracts; Persistence references Core/Contracts; tests reference API/Persistence. Modify `VideoGrabber.slnx` and `Directory.Packages.props`; generated package lockfiles belong to their projects.

All new production files use file-scoped namespaces, sealed records/classes, constructor injection and CancellationToken, matching existing style. HTTP errors use ProblemDetails with stable `code`, no token/request-body echo. DTOs declared in this plan live in Platform.Contracts: AccountContracts.cs owns AccountProfile, VerifiedIdentity and IAccountStore; AuthContracts.cs owns BeginSignIn, SignInStart, CompleteSignIn and ApiSession; IdentityContracts.cs owns LinkChallenge, LinkProof and MergeRequest. Create these exact files in their owning tasks so desktop/Telegram never reference Platform.Api for contracts.


### Task 1: A functioning isolated profile API and test harness

**Files — Create:** `src/VideoGrabber.Platform.Contracts/AccountContracts.cs`; `src/VideoGrabber.Platform.Api/Program.cs`; `src/VideoGrabber.Platform.Api/Accounts/AccountEndpoints.cs`; `src/VideoGrabber.Platform.Persistence/AccountStore.cs`; `src/VideoGrabber.Platform.Persistence/MigrationRunner.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/001_accounts.sql`; `tests/VideoGrabber.Platform.Tests/ApiFixture.cs`; `tests/VideoGrabber.Platform.Tests/ProfileIsolationTests.cs`; `tests/VideoGrabber.Platform.Tests/AdjustableTimeProvider.cs`; `deploy/platform/postgres.test.yml`. Include the five project files/solution/packages named above.

**Interfaces:**
```csharp
public sealed record AccountProfile(Guid AccountId, string Role, bool Blocked,
    string[] LinkedProviders, DateTimeOffset? FirstPurchaseAt);
public sealed record VerifiedIdentity(string Issuer, string Provider, string Subject,
    string? VerifiedEmail, DateTimeOffset AuthTime, string Assurance);
public interface IAccountStore
{
    Task<AccountProfile> ResolveAsync(VerifiedIdentity identity, CancellationToken cancellationToken);
    Task<AccountProfile?> ReadAsync(Guid accountId, CancellationToken cancellationToken);
}
```
`AccountStore(NpgsqlDataSource dataSource)` implements IAccountStore. `MigrationRunner.ApplyAsync(NpgsqlDataSource,CancellationToken)` applies embedded ordered SQL under advisory lock and verifies stored SHA-256, transaction per migration, no destructive down migrations. `public partial class Program` supports WebApplicationFactory.

Test fixture implements IAsyncDisposable: `Task<ApiFixture> StartAsync()`, `Task<TestAccount> AccountAsync(string provider,string subject,string? verifiedEmail=null)`, `Task<HttpClient> AdminAsync()`, `NpgsqlDataSource Database`, `AdjustableTimeProvider Clock`, `Task RestartAsync()`. `TestAccount(Guid Id,HttpClient Client)` and clock `Advance(TimeSpan)` are defined in those test files. For Task 1 only, AccountAsync uses a test-host authentication handler that calls the real AccountStore and installs its returned account_id; it cannot be registered by the production Program. Task 2 replaces it with a synthetic issuer token and the real auth exchange. AdminAsync creates a test-only owner through migration-role SQL and a fresh MFA assertion. These helpers are only compiled into tests, not API routes. Database names must start vg_test_; absent isolated DSN is FAIL, never SKIP. Create a unique database for each fixture; leave disposal cleanup to an explicitly named disposable-test cleanup run, not shared databases.

- [ ] **RED — Write ProfileIsolationTests** including this initial case and HTTP 401 for no token, 426 for incompatible API version:
```csharp
[Fact]
public async Task Email_equality_never_selects_another_account()
{
    await using var f = await ApiFixture.StartAsync();
    var a = await f.AccountAsync("google", "subject-a", "same@example.test");
    var b = await f.AccountAsync("apple", "subject-b", "same@example.test");
    Assert.NotEqual(a.Id, b.Id);
    var response = await a.Client.GetAsync($"/v1/accounts/{b.Id}");
    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    var profile = await a.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
    Assert.Equal(a.Id, profile!.AccountId);
}
```
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~ProfileIsolationTests`. First failure is missing projects/types/routes; after harness exists the same-email test must fail for behavior before implementing resolution.
- [ ] **GREEN — Add migration and route; return an actual persisted guest profile.** Schema core:
```sql
create schema licensing;
create table licensing.accounts (
 account_id uuid primary key, base_role text not null
 check (base_role in ('guest','user','owner_admin')),
 blocked_at timestamptz, first_purchase_at timestamptz,
 created_at timestamptz not null default now());
create table licensing.identities (
 identity_id uuid primary key, account_id uuid not null
 references licensing.accounts(account_id),
 issuer text not null, provider text not null, provider_subject text not null,
 verified_email text, linked_at timestamptz not null default now(),
 unique(issuer,provider,provider_subject));
alter table licensing.accounts enable row level security;
alter table licensing.accounts force row level security;
alter table licensing.identities enable row level security;
alter table licensing.identities force row level security;
create policy account_read on licensing.accounts for select to vg_api
 using(account_id = nullif(current_setting('vg.account_id',true),'')::uuid);
create policy identity_owner on licensing.identities to vg_api
 using(account_id = nullif(current_setting('vg.account_id',true),'')::uuid)
 with check(account_id = nullif(current_setting('vg.account_id',true),'')::uuid);
```
Provision `vg_migrator` (schema owner), `vg_api` (NOBYPASSRLS, no CREATE, no role membership), `vg_identity` (only identity resolution operations) and `vg_admin` (audited server path only). Create roles before policies; add explicit INSERT/SELECT account+identity policies for vg_identity and audited administrative policies for vg_admin, with no role membership granted to vg_api. The authentication resolution service alone receives the vg_identity connection. Add a transaction-scoped account parameter with parameterized `select set_config('vg.account_id',@account,true)`; never concatenate SQL. Resolution uses a serializable transaction and unique identity constraint: on 40001/23505 roll back and retry up to three times, reading the existing mapping. RLS is defense-in-depth for API bugs, not protection against a compromised DB service role; browser clients receive no DSN.

API route behavior:
```csharp
app.MapGet("/v1/me", async (HttpContext http, IAccountStore store, CancellationToken ct) =>
{
    var accountId = Guid.Parse(http.User.FindFirst("account_id")!.Value);
    var profile = await store.ReadAsync(accountId, ct);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
}).RequireAuthorization();
```
Create compose test service postgres:17.6 bound only to 127.0.0.1:55439, DB `vg_test_platform`, synthetic test-only username/password `vg_test`, healthcheck pg_isready, no production volume. Document `docker compose -p vg-platform-test -f deploy/platform/postgres.test.yml up -d`; fail if the port/project is already unrelated. ApiFixture reads VG_TEST_POSTGRES_DSN (never logs it), validates its host/database and builds per-fixture database; no default to app production config.
- [ ] **GREEN run:** repeat RED command; then `dotnet build src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj -c Release`. Assert two accounts, immutable provider key and zero cross-account rows.
- [ ] **Commit:** stage only this task's exact files and generated package locks; `git commit -m "feat: add isolated account profile API"`.

### Task 2: Five provider adapters and short-lived API sessions

**Files — Create:** `src/VideoGrabber.Platform.Api/Auth/BrokerTokenValidator.cs`, `BrokerPartitionOptions.cs`, `ProviderFlow.cs`, `SessionEndpoints.cs`, `SessionStore.cs` in that Auth directory; `src/VideoGrabber.Platform.Persistence/Migrations/002_sessions.sql`; `tests/VideoGrabber.Platform.Tests/ProviderAssertionTests.cs`; `tests/VideoGrabber.Platform.Tests/BrokerEmulator.cs`; `deploy/platform/auth-partitions.example.json`; `docs/platform/provider-qualification.md`. Modify Program.cs and ApiFixture.cs for real validator/emulator wiring.

**Interfaces:**
```csharp
public sealed record BeginSignIn(string Provider, Uri ReturnUri, string ClientChallenge);
public sealed record SignInStart(Guid FlowId, Uri AuthorizationUri, DateTimeOffset ExpiresAt);
public sealed record CompleteSignIn(Guid FlowId, string Code, string State, string ClientVerifier);
public sealed record ApiSession(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
public interface IBrokerTokenValidator
{
 Task<VerifiedIdentity> ValidateAsync(string token, string expectedProvider,
     string expectedNonce, CancellationToken cancellationToken);
}
```
ProviderFlow exposes `Task<SignInStart> BeginAsync(BeginSignIn,CancellationToken)` and `Task<ApiSession> CompleteAsync(CompleteSignIn,CancellationToken)`. Routes POST /v1/auth/start, /complete, /refresh, /logout. BrokerTokenValidator uses a configured partition issuer, expected aud, algorithm allowlist and fetched/cached JWKS; no dynamic issuer URL from incoming token.

- [ ] **RED — Write ProviderAssertionTests** using five [InlineData] providers and complete roundtrips via BrokerEmulator. Emulator exposes `Issue(string provider,string subject,string? email,string nonce,DateTimeOffset expiresAt)` and `WithClaim(string token,string name,string value)` (re-sign with its test key so issuer/aud/state tests exercise validation, not only broken signatures):
```csharp
[Theory]
[InlineData("google")][InlineData("apple")][InlineData("yandex")]
[InlineData("telegram")][InlineData("email")]
public async Task Repeated_verified_identity_has_one_account(string provider)
{
    await using var f = await ApiFixture.StartAsync();
    var first = await f.AccountAsync(provider, "stable-subject");
    var second = await f.AccountAsync(provider, "stable-subject");
    Assert.Equal(first.Id, second.Id);
}
```
Add bad issuer/aud/expiry/nonce/state/PKCE, consumed flow, changed frozen subject, unsigned JWT, duplicate subject identity array and unverified email cases; assert 401 or 409 and unchanged accounts count.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~ProviderAssertionTests`.
- [ ] **GREEN — Implement the protocol, not hard-coded success.** Use fixed registered HTTPS server callback per partition; desktop return is a one-use flow completion via loopback and bound PKCE S256. Store only hashes of state/client verifier and refresh secrets; flow expires in five minutes, state comparison fixed-time. Provider tokens stay in the server flow/session vault, never JSON profile or logs. API JWT expires in five minutes, audience videograbber-api; refresh rotates in one transaction, replay revokes its family. Resolve role from accounts on every mutation, not JWT metadata.

Core binding guard:
```csharp
if (brokerIdentities.Count != 1 ||
    brokerIdentities[0].Provider != partition.Provider ||
    brokerIdentities[0].Subject != frozenProviderSubject)
    throw new UnauthorizedAccessException("identity_partition_conflict");
```
Here `brokerIdentities` is the validated /auth/v1/user response, `partition` is BrokerPartitionOptions, `frozenProviderSubject` is the persisted binding or the sole validated subject on first login; define private records in BrokerTokenValidator.cs. Reject any mapping change before account resolution.

Google/Apple use isolated built-in Supabase providers; Yandex uses isolated custom OAuth2 with fixed authorization/token/userinfo endpoints; Telegram uses isolated custom OIDC with email_optional true, PKCE S256 and no fabricated UserInfo requirement; verified email uses its own OTP partition. Configure exact endpoints from official discovery/config in qualification evidence. For Telegram preserve verified OIDC sub and verified numeric id separately: insert a unique alias only when a verified response includes both; otherwise require explicit two-proof business linking with bot/Mini App numeric identity. Never parse OIDC sub as numeric user ID. For tests with a numeric Telegram subject, BrokerEmulator deliberately issues separate sub=`oidc-<numeric>` and signed id=<numeric>; the API verifies the alias before P4 MiniAppAsync can resolve that same account.

R027 five-partition config contains provider, issuer, audience, callbackUri, providerKey only; deployment injects real server credentials. API startup rejects duplicate issuer, >1 enabled provider per partition and emulator outside Development. JWT signed by the emulator is accepted only in the test host.
- [ ] **GREEN run:** repeat filtered suite and build; scan captured request logging with synthetic sentinel tokens and assert no token/code/cookie/signed URL appears.
- [ ] **Commit:** exact task files; `git commit -m "feat: validate provider sessions without email merging"`.

### Task 3: Two-proof links, last-login recovery and auditable merge

**Files — Create:** `src/VideoGrabber.Platform.Api/Accounts/IdentityLinkService.cs`; `IdentityEndpoints.cs` in same directory; `src/VideoGrabber.Platform.Persistence/Migrations/003_identity_links_audit.sql`; `tests/VideoGrabber.Platform.Tests/IdentityLinkTests.cs`; `docs/platform/account-recovery.md`. Modify AccountEndpoints.cs and Program.cs.

**Interfaces:**
```csharp
public sealed record LinkChallenge(Guid ChallengeId, DateTimeOffset ExpiresAt);
public sealed record LinkProof(Guid ChallengeId, string FreshProviderAssertion);
public sealed record MergeRequest(Guid SourceAccountId, Guid TargetAccountId,
    string Reason, string SourceProof, string TargetProof);
```
IdentityLinkService methods: `BeginAsync(Guid accountId,CancellationToken)`; `CompleteAsync(Guid accountId,LinkProof,CancellationToken)`; `UnlinkAsync(Guid accountId,Guid identityId,CancellationToken)`; `MergeAsync(Guid adminId,MergeRequest,CancellationToken)`, all Task-returning except Begin returns Task<LinkChallenge>. Existing authenticated session must be fresh <=5m for link/unlink; new provider proof is independently bound to challenge. Merge remains unavailable until P2 grant reconciliation is installed: it returns a concrete 409 `financial_merge_requires_reconciliation` whenever either account has business rows; P2 replaces that policy with an atomic audited transfer.

- [ ] **RED — Add this test plus last-provider unlink 409, expired/replayed challenge 409, foreign challenge 404, and paid-account collision 409:**
```csharp
[Fact]
public async Task Link_challenge_cannot_be_consumed_by_another_account()
{
    await using var f = await ApiFixture.StartAsync();
    var a = await f.AccountAsync("telegram", "1001");
    var b = await f.AccountAsync("telegram", "1002");
    var begun = await a.Client.PostAsJsonAsync("/v1/identities/link", new { });
    var challenge = await begun.Content.ReadFromJsonAsync<LinkChallenge>();
    var response = await b.Client.PostAsJsonAsync("/v1/identities/link/complete",
        new LinkProof(challenge!.ChallengeId, "synthetic-invalid-proof"));
    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
}
```
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~IdentityLinkTests`.
- [ ] **GREEN — Create account_links, broker_bindings and audit_events; claim challenge atomically.**
```sql
update licensing.account_links set consumed_at = now()
where challenge_id = @challenge and account_id = @account
 and consumed_at is null and expires_at > now()
returning challenge_id;
```
No row means 404 for foreign challenge, 409 for a known expired/consumed challenge. Lock account row before counting/removing identities so concurrent unlinks cannot remove the last sign-in. Fresh verified email recovery resolves only the already-linked email identity; email similarity never grants recovery. No-login recovery uses explicit admin MFA review, recorded proof source, reason, session revocation and a one-use five-minute link; never reveals email existence to anonymous requests. Account merge transfers identity ownership only after source and target proof plus admin MFA, preserves source as merged_into tombstone and retains audit history; block both account rows in UUID order.
- [ ] **GREEN run:** repeat tests; test two simultaneous unlinks preserve one identity and two simultaneous challenge consumes succeed once.
- [ ] **Commit:** exact task files; `git commit -m "feat: add safe identity linking and account recovery"`.

### Task 4: Verify every account boundary and qualify provider configuration

**Files — Create:** `tests/VideoGrabber.Platform.Tests/DatabaseBoundaryTests.cs`; `tests/VideoGrabber.Platform.Tests/AuthPrivacyTests.cs`; `scripts/platform/Test-ProviderQualification.ps1`. Modify migration 003 only before it is applied outside disposable tests; otherwise create 004_account_security.sql. Add security_invoker views only if needed; do not expose business schemas via Supabase.

**Interfaces:** Test-ProviderQualification.ps1 parameters `-EvidenceDirectory <absolute-directory> -Mode Emulator|Live -Provider google|apple|yandex|telegram|email`; Live requires explicit test-target configuration and already-authorized user login. It creates a JSON record with provider, issuer hash, version, callback validation, positive login, collision/replay/expiry results and PASS/FAIL/BLOCKED, never tokens.

- [ ] **RED — Add direct SQL/API boundary tests.**
```csharp
[Fact]
public async Task Missing_tenant_context_reads_no_accounts()
{
    await using var f = await ApiFixture.StartAsync();
    await f.AccountAsync("google", "one");
    await using var connection = await f.Database.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await using var role = new Npgsql.NpgsqlCommand("set local role vg_api", connection, transaction);
    await role.ExecuteNonQueryAsync();
    await using var query = new Npgsql.NpgsqlCommand(
        "select count(*) from licensing.accounts", connection, transaction);
    Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
}
```
Also assert UPDATE owner reassignment denied, PUBLIC/anon/authenticated cannot access licensing schema or functions, vg_api lacks BYPASSRLS, migrations cannot be run by API, pooled connection loses SET LOCAL after transaction, metadata role mutation cannot elevate, CSRF absent token rejects browser mutation, unsupported version yields 426.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter "FullyQualifiedName~DatabaseBoundaryTests|FullyQualifiedName~AuthPrivacyTests"`.
- [ ] **GREEN — Apply explicit grants/revokes and boundary middleware.**
```sql
revoke all on schema licensing from public;
revoke all on all functions in schema licensing from public;
alter default privileges in schema licensing revoke execute on functions from public;
```
Grant only required commands to each runtime role. Identity resolution/admin paths use dedicated narrowly scoped connections unavailable to ordinary routes. Default deny CORS except exact Mini App/admin origins; secure HttpOnly SameSite cookies + antiforgery for browser sessions, bearer token flow for desktop, per-route rate limits from program. Redact authorization/cookie/set-cookie/query values, OAuth body and provider webhook payload; log correlation IDs and stable error codes. Data API, storage, SSE and payment/job boundary tests are added with those subsystems; their current nonexistence is not marked PASS.
- [ ] **GREEN run:** full Platform.Tests plus existing Core.Tests/Infrastructure.Tests; `pwsh -File scripts/platform/Test-ProviderQualification.ps1 -Mode Emulator -Provider telegram -EvidenceDirectory D:\CODEX\Artifacts\videograbber-platform\p1-telegram`.
- [ ] **Commit:** exact task files; `git commit -m "test: enforce account and provider isolation"`.

## Acceptance and handoff

Local gate: API starts, durable same identity returns same account; no email merge; link/recovery/unlink races fail safely; real PostgreSQL RLS and API cross-account cases pass; all five emulator protocols exercised; no secrets in logs; lockfiles committed. Manual gate: use the real profile UI/HTTP with accounts A/B and verify schema migrations twice on a fresh DB.

Live gate: separately test Google, Apple, Yandex, Telegram and email with registered test apps and exact redirects; same email across partitions and distinct subjects within a partition never gain each other's account. Compare OIDC sub with numeric Telegram ID only through verified mapping. A failing/absent live gate disables that provider and keeps VG-GAP-002/027 open for production. P2 consumes AccountProfile, VerifiedIdentity, IAccountStore, ApiFixture and private-schema migration runner; no production deploy occurs here.
