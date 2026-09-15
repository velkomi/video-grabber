# VideoGrabber Recovery, Deployment and Operational Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a reproducible isolated platform deployment, prove backup/restore consistency and record operational/release readiness from real measurements.

**Architecture:** Build API/worker artifacts from the accepted commit, pin containers and tool hashes, and deploy only into a named isolated environment. Use PostgreSQL backups plus separate protected secret/lease-key recovery and application-level reconciliation; health checks and alerts reflect user-facing failure, not just process liveness.

**Tech Stack:** .NET 10, PostgreSQL 17, Docker Compose, Caddy reverse proxy, restic encrypted backup, PowerShell 7, existing xUnit.

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

P1–P6 local acceptance is required. Covers VG-GAP-022/023/024 and final proof of all platform subsystems. No production infrastructure change, DNS/firewall modification, secret read or publication is authorized by this planning task. Every script defaults to isolated test mode and refuses unqualified targets.

Additional operational dependencies: Docker/Compose already installed or separately approved; Caddy 2.10.0 and restic 0.18.0 are qualification baselines, patched compatible versions may replace them after official release/advisory verification. PostgreSQL client tools must match the deployed major. Supabase Auth partitions may be managed services or isolated official Auth deployments, each recorded with its actual version/config; no fabricated project credentials. Every final image uses a qualified digest in dependency-lock.json before any staged deployment.


### Task 1: Reproducible manifests with startup security checks

**Files — Create:** `deploy/platform/compose.staging.yml`; `deploy/platform/Caddyfile`; `deploy/platform/api.Dockerfile`; `deploy/platform/dependency-lock.json`; `deploy/platform/platform.env.example`; `scripts/platform/Resolve-PlatformDependencies.ps1`; `scripts/platform/Start-PlatformStage.ps1`; `tests/VideoGrabber.Platform.Tests/DeploymentConfigurationTests.cs`; `docs/platform/deployment-runbook.md`. Modify worker.Dockerfile and API/worker health endpoints.

**Interfaces:** Resolve-PlatformDependencies.ps1 `-EvidenceDirectory <absolute> -SourceCommit <sha>` resolves verified image tags to registry digests, compares media tool hashes/licenses, writes lock JSON and checks NuGet lock/advisories. It never deploys. Start-PlatformStage.ps1 `-EnvironmentName vg-stage-<suffix> -DependencyLock <path> -ConfigPath <path> -EvidenceDirectory <path>` validates environment, secret reference files and exact lock before running compose; rejects name without vg-stage-, public DB/Bot API/admin listeners, production hostnames and unpinned images.

- [ ] **RED — Add manifest/config tests for insecure defaults.**
```csharp
[Theory]
[InlineData("postgres:latest")]
[InlineData("videograbber-api:dev")]
public void Stage_rejects_unpinned_images(string image)
{
    Assert.False(DeploymentConfiguration.IsPinnedImage(image));
}
```
Create `src/VideoGrabber.Platform.Core/Operations/DeploymentConfiguration.cs` for IsPinnedImage(string) and ValidateStageName(string). Add a `--validate-stage-config <path>` command branch to `src/VideoGrabber.Platform.Api/Program.cs` before building the web host: parse a non-secret config file, run these validators, print only violated field names, exit 0/1 without connecting to infrastructure. Start-PlatformStage.ps1 calls this exact command before compose, so the tested checks guard real deployment. Test external host DB port, wildcard CORS, missing Auth issuer, test adapter in stage, blank signing key reference, live selling without catalog, public local Bot API and unauthorized callback URL.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~DeploymentConfigurationTests`.
- [ ] **GREEN — Define real service manifests for built API, worker, postgres, private Bot API and reverse proxy.**
```csharp
public static bool IsPinnedImage(string image) =>
    System.Text.RegularExpressions.Regex.IsMatch(image, @"^.+@sha256:[a-f0-9]{64}$");
public static bool ValidateStageName(string name) =>
    System.Text.RegularExpressions.Regex.IsMatch(name, @"^vg-stage-[a-z0-9-]{1,32}$");
```
Deploy images from built source commit and digest lock. Only proxy HTTPS port publishes; postgres/Bot API/worker/admin diagnostics remain private. Health: /health/live process, /health/ready schema version/DB/keyring/capability dependencies, worker heartbeat; health response contains no secrets/DSN. API waits for successful compatible migration, worker stays not-ready until required tools/hash/egress quota probes pass.

Config example uses safe literal `Payments__LiveEnabled=false`, `Auth__DevelopmentIssuerEnabled=false`, `Retention__Enabled=false` and names of required secret file variables, never real values. Runtime requires explicit production/stage config with mounted secret files. Limit request/response body sizes per route; uploads are ticket-scoped streamed limits, not unlimited reverse proxy buffering. Exact CORS, CSP script-src self plus required Telegram script origin only, frame-ancestors qualified Telegram origins, HSTS after HTTPS qualification, secure cookies/CSRF. Rate-limit config from master. TLS issuance/DNS only for an already authorized stage hostname.

PostgreSQL migration role injected only into a one-shot migrator; runtime roles cannot alter schema. Admin owner bootstrap points to explicit account ID and MFA enrollment; no automatic owner. Auth partitions register only exact callbacks and disallow cross-provider automatic linking as in R027. Preserve existing workstation Docker/PostgreSQL workloads; no global shutdown/network reset.
- [ ] **GREEN run:** tests; `docker compose -f deploy/platform/compose.staging.yml config --quiet` under synthetic test env; build API/worker; start named isolated stage only after its target/dependencies are qualified. Probe HTTPS/API readiness, DB/Bot API inaccessibility externally and CORS/CSRF errors.
- [ ] **Commit:** exact files and dependency lock; `git commit -m "ops: define qualified isolated platform deployment"`.

### Task 2: Consistent encrypted backup and isolated restore drill

**Files — Create:** `scripts/platform/Backup-Platform.ps1`; `scripts/platform/Restore-PlatformDrill.ps1`; `scripts/platform/Test-PlatformConsistency.ps1`; `src/VideoGrabber.Platform.Persistence/ConsistencyReport.cs`; `tests/VideoGrabber.Platform.Tests/RestoreConsistencyTests.cs`; `docs/platform/backup-recovery-runbook.md`. Add narrowly scoped backup DB role migration 050_backup_role.sql.

**Interfaces:** Backup-Platform.ps1 `-EnvironmentName <qualified> -BackupRoot <absolute> -EvidenceDirectory <absolute>`; Restore-PlatformDrill.ps1 `-SnapshotId <id> -TargetDatabase vg_test_restore_<suffix> -EvidenceDirectory <absolute>`; both read credential references from protected environment without printing values. ConsistencyReport `RunAsync(NpgsqlDataSource,CancellationToken)`→Task<Dictionary<string,long>> with zero-valued violation counts. Restore script accepts only disposable database/target allowlist, never app production DSN as destination.

- [ ] **RED — Add consistency tests including deliberate broken grant/ledger projection in a fresh DB.**
```csharp
[Fact]
public async Task New_platform_database_has_no_accounting_violations()
{
    await using var f = await ApiFixture.StartAsync();
    var report = await ConsistencyReport.RunAsync(f.Database, CancellationToken.None);
    Assert.All(report, pair => Assert.Equal(0, pair.Value));
}
```
Write complete integration fixture: two accounts, linked identities, owner, guest gifts, one spent/one held/one expired credit, device/lease metadata, completed job+artifact/delivery_unknown, confirmed/refunded payment and canceled subscription. Back up while a concurrent reservation transaction executes; restore and assert all-or-none effects, no orphaned rows, duplicate grant or negative bucket.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~RestoreConsistencyTests`.
- [ ] **GREEN — Take consistent pg_dump custom-format snapshot with matching client; store encrypted restic snapshot.** Separate backup scopes: business PostgreSQL; private retained server artifacts; broker configuration/session recovery procedure; protected signing/refresh-envelope keys. Broker passwords/service secrets are never written into evidence/Git. Backup files use private permissions, no user media directories. Include backup timestamp, migration SHA, source commit, artifact inventory hashes and encryption key reference; verify restic check and pg_restore --list before declaring valid.

Consistency SQL examples:
```sql
select count(*) from licensing.entitlement_grants
where available<0 or reserved<0;
select count(*) from licensing.jobs j
left join licensing.artifacts a on a.artifact_id=j.artifact_id
where j.state='completed' and a.artifact_id is null;
select source_reference,count(*) from licensing.entitlement_grants
where source='purchase' group by source_reference having count(*)>1;
```
Add exact grant bucket ledger sum reconciliation, reservation terminal state versus attempt/fence, payment event projection, account identity uniqueness and artifact existence/hash. Missing expired artifact is valid only with explicit retention tombstone; completed live artifact missing is failure.

On restore, pause consumers, invalidate old worker attempts by fence increment, expire or revalidate sessions, keep signing keys needed for outstanding offline leases, load fresh current keys for new leases, rebuild payment/job outbox projections idempotently and reconcile pending provider states only after authorized external connectivity. Artifact restore cannot manufacture missing successful outputs. Never regenerate purchases simply from a “paid” client history.

Measure RPO target <=15 minutes and RTO target <=60 minutes from actual timestamps; these are targets, not guarantees. Exercise lost DB and missing key scenarios; missing protected key material leaves affected services fail-closed with a documented recovery action.
- [ ] **GREEN run:** perform real isolated restore, compare fixture counts/invariants/hashes, record actual RPO/RTO; rerun all Platform tests on restored DB. No production destination accepted.
- [ ] **Commit:** exact files; `git commit -m "ops: verify platform backup and isolated recovery"`.

### Task 3: Capacity, metrics, alert delivery and retention safety

**Files — Create:** `src/VideoGrabber.Platform.Api/Operations/PlatformMetrics.cs`; `src/VideoGrabber.Platform.Worker/WorkerMetrics.cs`; `scripts/platform/Measure-PlatformCapacity.ps1`; `scripts/platform/Test-PlatformAlerts.ps1`; `tests/VideoGrabber.Platform.Tests/OperationalStateTests.cs`; `docs/platform/operations-dashboard.md`; `deploy/platform/alerts.json`. Modify ready probes and retention wiring.

**Interfaces:** PlatformMetrics exports internal authenticated JSON counters and OpenMetrics text for active/oldest jobs, reservations review_required, payment reconciliation lag, delivery_unknown, backup age, worker heartbeat, blocked admissions and dependency failures. No labels include account ID/email/URL/token. Capacity script `-EnvironmentName <qualified> -EvidenceDirectory <absolute> -DurationSeconds 120` reads real CPU/RAM/disk/inode/neighbor workload observations and performs bounded synthetic load within configured stage quotas; it never tunes host limits automatically.

- [ ] **RED — Add missing worker/old backup/DB saturation cases.**
```csharp
[Theory]
[InlineData(14, false)]
[InlineData(16, true)]
public void Backup_age_above_target_alerts(int minutes, bool expected)
{
    Assert.Equal(expected, PlatformMetrics.BackupTooOld(TimeSpan.FromMinutes(minutes)));
}
```
Also test oldest queued job>10m, heartbeat>60s, free disk<20%, restore drill absent>7days, payment verification lag>5m and delivery_unknown>0 with deduplicated alert. Define metric methods in named files; values come from DB/host, not hard-coded “healthy”.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~OperationalStateTests`.
- [ ] **GREEN — Implement measured readiness and alert transitions.**
```csharp
public static bool BackupTooOld(TimeSpan age) => age > TimeSpan.FromMinutes(15);
```
Expose degraded cause and correlation, not secrets. Alerts fire on state change and remind after a configured 60-minute unresolved interval; local tests send to an HTTP emulator. A real alert destination requires explicit messaging authorization. Show capacity measured under concurrent downloader and ASR job, outbox backlog, disk/inode forecast and neighboring VPS processes read-only. Refuse concurrency increase when free capacity uncertain.

Retention remains disabled until the configured owned paths, expiry policy and backup behavior pass a dry-run report; stage activation explicitly applies only the synthetic roots. Production enablement names exact root and expiry policy with approval, never broad cleanup. Check failed cleanup alerts and recovery from partial retention without corrupting the ledger.
- [ ] **GREEN run:** capacity report plus injected disk-full/DB-unavailable/worker-stale/backup-old alerts; verify actual receiving emulator logs and recovery/resolution notifications. Restore healthy stage state without touching neighbors.
- [ ] **Commit:** exact files; `git commit -m "ops: measure platform health capacity and recovery signals"`.

### Task 4: Compatibility, final regression and release evidence

**Files — Create:** `scripts/platform/Test-PlatformRelease.ps1`; `docs/platform/release-acceptance.md`; `docs/platform/api-compatibility.md`; `tests/VideoGrabber.Platform.Tests/ApiCompatibilityTests.cs`. Modify build release packaging for local/managed/API/worker named manifests and exact source SHAs; keep existing notices and add qualified tool redistribution materials.

**Interfaces:** Test-PlatformRelease.ps1 `-SourceCommit <sha> -EvidenceDirectory <absolute> -Mode Local|Stage`; validates clean scoped source snapshot, lockfiles, migration compatibility and all required artifacts. Returns a JSON readiness map with PASS/FAIL/BLOCKED per check and overall ready only if every required target gate passes. Missing tests/fixtures never SKIP or N/A by default.

- [ ] **RED — Current and previous supported client contract, unsupported version 426.**
```csharp
[Fact]
public async Task Unsupported_client_gets_upgrade_response()
{
    await using var f = await ApiFixture.StartAsync();
    var account = await f.AccountAsync("google", "old-client");
    account.Client.DefaultRequestHeaders.Add("X-VideoGrabber-Protocol", "0");
    var response = await account.Client.GetAsync("/v1/access");
    Assert.Equal((System.Net.HttpStatusCode)426, response.StatusCode);
}
```
Add previous compatible minor accepts v1, unknown enum fields handled predictably, old worker cannot claim unsupported operation, additive migration works with previous client and N-1 API read paths.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~ApiCompatibilityTests`.
- [ ] **GREEN — Enforce program compatibility policy and aggregate actual commands/results.**
```powershell
dotnet test tests/VideoGrabber.Core.Tests/VideoGrabber.Core.Tests.csproj -c Release
dotnet test tests/VideoGrabber.Infrastructure.Tests/VideoGrabber.Infrastructure.Tests.csproj -c Release
dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj -c Release
dotnet test tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj -c Release
dotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release -p:VideoGrabberEdition=Local
dotnet build src/VideoGrabber.App/VideoGrabber.App.csproj -c Release -p:VideoGrabberEdition=Managed
dotnet build src/VideoGrabber.Platform.Api/VideoGrabber.Platform.Api.csproj -c Release
dotnet build src/VideoGrabber.Platform.Worker/VideoGrabber.Platform.Worker.csproj -c Release
```
Each command checks LASTEXITCODE immediately and records exact args/TRX/source SHA; do not let a later success conceal earlier failure. Syntax-check PowerShell, JSON/XML and Mini App JavaScript; check lockfile and runtime advisory/provenance reports. Backup/restore previous-schema snapshot to new version; no destructive rollback migration. Code rollback only if schema compatible, otherwise roll forward with approved restore drill and measured data impact.

Manually verify real managed/local EXEs on a clean Windows without SDK, 100%/150% scaling, keyboard, queue/cancel/preflight/close/account state, verified authorized course lesson, no existing outputs deleted, both auth browser/media browser independent. Verify Mini App on Telegram mobile/desktop, actual worker results and approved large-file delivery, live five-provider collision/link flows, sandbox payment/refund/recurring support. Hash unpacked package files and compare manifest; actual launched EXE is from that package.
- [ ] **GREEN run:** release script aggregates every automated/live gate. Status remains NOT_READY if any mandatory check is FAIL/BLOCKED, even if ZIP/build exists.
- [ ] **Commit:** exact files; `git commit -m "test: gate platform compatibility and release readiness"`.

## Acceptance and final authorization boundary

An isolated stage actually starts with qualified images, every service reports useful health, DB/Bot API/admin internals are private, workload/network limits are exercised, backup restores with reconciled account/grant/ledger/job/payment state, RPO/RTO are measured, and alert delivery is verified. Every spec acceptance item is linked to test or live evidence. Previous local releases remain unaffected.

Deployment source/manifests do not prove real operation. Public release/push/merge/live payment enablement and production migration are distinct final actions that require their already-established authorization. The executor presents the exact reviewed artifact/source SHA, environment, migration/rollback proof and remaining gates before any such action.
