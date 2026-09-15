# VideoGrabber Workers and Telegram Delivery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute durable server/desktop media jobs with the same credit rules and deliver private verified results, including large files and recovery from uncertain delivery.

**Architecture:** PostgreSQL jobs/attempts/outbox provide admission and fencing without a broker dependency. A Linux server worker runs bounded media tools behind an egress boundary; a managed Windows companion pulls account/device-scoped jobs and keeps private site sessions local. Delivery retries are a separate state machine.

**Tech Stack:** .NET 10 hosted workers, PostgreSQL, existing media interfaces, official yt-dlp/FFmpeg/Whisper and local Telegram Bot API, xUnit.

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

P2+P3+P4 accepted; local media P1/security defects and process/file ownership regressions must be fixed before reusing execution code. Covers VG-GAP-011/013/015/016/017/030. No package broker or Telegram SDK is added. New projects are Worker and Worker.Tests; existing Infrastructure code is evaluated for Linux portability rather than assumed portable. Official tool hashes/licenses and local Bot API version/limits are qualified explicitly.

P2 owns ReservationRequest/Receipt, CreditLedger and device identity. P4 owns IBotApiClient, DeliveryDestination and DestinationService. P3 owns managed session/device protection. The desktop edition has a usable standalone downloader before this bridge.


### Task 1: Durable admission, attempts, heartbeat/fencing and recovery

**Files — Create:** `src/VideoGrabber.Platform.Contracts/JobContracts.cs`; `src/VideoGrabber.Platform.Core/Jobs/JobRequestHasher.cs`; `src/VideoGrabber.Platform.Persistence/JobStore.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/030_jobs_outbox.sql`; `src/VideoGrabber.Platform.Api/Jobs/JobEndpoints.cs`; `src/VideoGrabber.Platform.Api/Jobs/AttemptEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/JobRecoveryTests.cs`. Modify CreditLedger.cs to share a transaction with job admission/finalization. JobRequestHasher.Hash(CreateJob) returns lowercase SHA-256 of canonical JSON with RequestHash excluded, fixed field order as declared in CreateJob, lowercase UUID strings, invariant integer numbers and inputArtifactIds in requested order; whitespace-free UTF-8, no locale formatting. Account ID is additionally bound by the reservation's account_id unique key.

**Interfaces:**
```csharp
public sealed record CreateJob(Guid IntentId, string RequestHash, string Kind, string Executor,
    Guid? DeviceId, string SourceId, string Quality, Guid[] InputArtifactIds,
    long? TrimStartMs, long? TrimDurationMs);
public sealed record JobView(Guid JobId, Guid AccountId, Guid IntentId, string State,
    string Executor, Guid? ArtifactId, string Reason);
public sealed record AttemptLease(Guid JobId, Guid AttemptId, long Fence,
    DateTimeOffset LeaseUntil, string CapabilityToken, CreateJob Work);
public sealed record ArtifactReceipt(Guid ArtifactId, string Sha256, long Bytes,
    string MediaType, string VerificationEvidenceId);
public sealed record AttemptCompletion(Guid JobId, Guid AttemptId, long Fence,
    string Outcome, ArtifactReceipt? Artifact, string EvidenceId);
```
JobStore `CreateAsync(Guid accountId,CreateJob,CancellationToken)`→Task<JobView>; `ClaimAsync(Guid workerId,Guid? accountId,Guid? deviceId,CancellationToken)`→Task<AttemptLease?>; `HeartbeatAsync(AttemptLease,CancellationToken)`→Task<bool>; `CompleteAsync(AttemptCompletion,CancellationToken)`→Task<JobView>. Public routes POST/GET /v1/jobs, GET /v1/jobs/{id}, POST /cancel and /retry; private worker claim/heartbeat/complete require worker capability identity, never user-supplied unrestricted account filter.

- [ ] **RED — Add crash/replay/fence tests using real PostgreSQL.**
```csharp
[Fact]
public async Task Admission_replay_returns_one_logical_job()
{
    await using var f = await ApiFixture.StartAsync();
    var user = await f.AccountAsync("telegram", "job-owner");
    var admin = await f.AdminAsync();
    (await admin.PostAsJsonAsync("/v1/admin/grants",
        new GrantRequest(user.Id, "credits", 0, 1, null, "job", Guid.NewGuid()))).EnsureSuccessStatusCode();
    var sourceId = await f.SourceAsync(user.Id, "720p");
    var request = new CreateJob(Guid.NewGuid(), new string('a',64), "download", "server_worker",
        null, sourceId, "720p", [], null, null);
    request = request with { RequestHash = JobRequestHasher.Hash(request) };
    var responses = await Task.WhenAll(Enumerable.Range(0, 10)
        .Select(_ => user.Client.PostAsJsonAsync("/v1/jobs", request)));
    var jobs = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<JobView>()));
    Assert.Single(jobs.Select(j => j!.JobId).Distinct());
}
```
Add ApiFixture.SourceAsync(Guid accountId,string quality)→Task<string> that inserts a synthetic account-owned source handle in the real licensing.sources table using the test migrator role. Task 1 creates this table in migration 030 (opaque source_id, account_id, media_id, selected source URI encrypted, qualities JSON, expires_at); Task 2 exposes real analysis, with no fixture: URI accepted by production. Test transaction failure after reserve before insert, crash after outbox commit before claim, heartbeat loss, expired old attempt completion after new fence, duplicate complete, cancellation/success race, blocked account and delivery-after-success behavior.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~JobRecoveryTests`.
- [ ] **GREEN — Add jobs/job_attempts/outbox/artifacts in the private schema.** Job create validates/canonicalizes request and hashes on server; client hash must match or 409. SourceId refers to analyzed source record, not an arbitrary worker command/path. Transaction locks account, reserves or records time permit, creates job and outbox. Unique(account_id,intent_id) is shared with reservation. There is no gap requiring an external enqueue commit.
```sql
select job_id from licensing.jobs
where state='queued' and executor=@executor
 and (@account::uuid is null or account_id=@account)
 and (@device::uuid is null or device_id=@device)
order by created_at for update skip locked limit 1;
update licensing.jobs set fence=fence+1,state='running' where job_id=@job
returning fence;
```
Only server-authorized worker identity can use null account scope; desktop bound account/device cannot. Attempt lease 60s/heartbeat15s/runtime2h/retry max3; terminal update WHERE attempt_id AND fence AND current state. Stale completion 409; expired active attempt cannot commit until revalidated. Successful artifact registration, ledger commit and job completed share one transaction; retained immutable verification evidence refers to owned artifact SHA. Outbox event ID unique; consumers dedupe. Failed eligible pre-start proof releases; uncertain local failure review_required. Worker-offline desktop job waiting_for_worker holds no credit until claim/admission; slot/account authorization is checked when it starts.
- [ ] **GREEN run:** targeted suite with injected crash checkpoints and restart; assert ledger bucket conservation and one terminal artifact.
- [ ] **Commit:** exact files; `git commit -m "feat: persist fenced media job execution"`.

### Task 2: Real Linux media worker and nested-network containment

**Files — Create:** `src/VideoGrabber.Platform.Worker/VideoGrabber.Platform.Worker.csproj`; `Program.cs`, `MediaJobExecutor.cs`, `ArtifactVerifier.cs`, `WorkerToolLocator.cs`, `BoundedProcessRunner.cs` in that project; `src/VideoGrabber.Platform.Api/Jobs/SourceAnalysisService.cs`; `src/VideoGrabber.Platform.Api/Jobs/EgressProxy.cs`; `tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj`; `WorkerMediaTests.cs`, `WorkerNetworkTests.cs` there; `deploy/platform/worker.Dockerfile`; `deploy/platform/worker.test.yml`. Modify solution and tool manifest as exact qualified versions become available.

**Interfaces:**
```csharp
public sealed record AnalyzedMedia(string SourceId, string MediaId, string Title,
    long? DurationMs, string[] Qualities, string RequiredExecutor);
public interface IMediaJobExecutor
{
 Task<ArtifactReceipt> ExecuteAsync(AttemptLease lease, CancellationToken cancellationToken);
}
```
SourceAnalysisService `AnalyzeAsync(Guid accountId,Uri source,CancellationToken)`→Task<AnalyzedMedia[]>; stores expiring account-owned source handles and sanitized metadata. EgressProxy validates URI and DNS answers on **every** connection/redirect/manifest child; IP-pinned connect uses the validated address with original TLS host name. Tool process receives only fixed arguments and allowlisted operation types.

- [ ] **RED — Add actual synthetic 360p/720p media and blocked nested HLS fixtures.**
```csharp
[Theory]
[InlineData("http://127.0.0.1/private")]
[InlineData("http://169.254.169.254/latest/meta-data/")]
[InlineData("http://[::1]/private")]
[InlineData("http://10.0.0.1/private")]
public async Task Server_source_policy_rejects_private_targets(string url)
{
    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        EgressProxy.ValidatePublicTargetAsync(new Uri(url), CancellationToken.None));
}
```
Add redirect public→private, DNS rebinding public→private, IPv4 mapped IPv6, userinfo, alternate numeric forms, private segment/key/child-manifest and attempted direct socket bypass. Test fixture public service is explicitly allowed only in isolated test network; production private-address override cannot be set by user request.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Worker.Tests/VideoGrabber.Platform.Worker.Tests.csproj --filter "FullyQualifiedName~WorkerMediaTests|FullyQualifiedName~WorkerNetworkTests"`; fixture absence FAIL, never SKIP.
- [ ] **GREEN — Implement process execution and artifact verification.** Worker uses fixed UUID-owned working directories under /var/lib/videograbber/jobs, read-only tool/model mounts, nonroot user, no host/browser mounts. Default per worker: 2 CPU, 2GiB memory, 4GiB job disk, 128 processes, 64 sockets, one active media job, 2-hour timeout; reject oversized expected outputs before start and terminate on quota. Use an isolated Linux process group so cancel kills descendants; Windows worker retains existing verified Job Object process semantics. Create files with no overwrite and prove directory ownership before promotion.

Core argv creation:
```csharp
var args = new List<string> { "--no-playlist", "--no-progress", "--proxy", proxyUri.AbsoluteUri,
    "-f", selectedFormat, "-o", Path.Combine(jobRoot, "result.%(ext)s"), "--", validatedSource.AbsoluteUri };
```
Define selectedFormat from analyzed allowlisted quality mapping, validatedSource from SourceId store, jobRoot from lease IDs; no shell string concatenation. Egress firewall is scoped to isolated worker network: outbound only via proxy and internal API/artifact endpoints; direct Internet/DNS/private services denied. Docker compose alone without a proven egress boundary is insufficient. Never modify unrelated host firewall.

ArtifactVerifier uses ffprobe plus full decode, expected streams/format/duration, exact selected resolution and SHA-256; positive result must exist in this attempt root. No recovery by filename prefix, no accepting partial readable output. MP3/trim/join/ASR use existing algorithm interfaces only after portability checks; WorkerToolLocator resolves Linux executable names and fixed paths, not .exe assumptions. Audit stream compatibility and SRT validation remain enforced.
- [ ] **GREEN run:** real Linux container fixture downloads six independent parts, ffprobe and full-decode each, no extra charges for HLS fragments. Network suite proves private/nested/direct bypass prevented. Inspect actual quota cancellation and descendant exit.
- [ ] **Commit:** exact files; `git commit -m "feat: run isolated verified server media jobs"`.

### Task 3: Managed desktop companion with scoped enrollment and uploads

**Files — Create:** `src/VideoGrabber.Infrastructure/Licensing/DesktopWorkerClient.cs`; `src/VideoGrabber.App/MainWindow.DesktopWorker.cs`; `src/VideoGrabber.Platform.Api/Jobs/ArtifactUploadEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/DesktopWorkerAuthorizationTests.cs`; `tests/VideoGrabber.Infrastructure.Tests/DesktopWorkerTests.cs`. Modify account page and P3 protected coordinator. Add migrations 031_artifact_uploads.sql.

**Interfaces:** DesktopWorkerClient `PollAsync(Guid deviceId,CancellationToken)`→Task<AttemptLease?>, `UploadAsync(AttemptLease lease,string selectedLocalPath,CancellationToken)`→Task<ArtifactReceipt>. Server `UploadTicket(Guid UploadId,Guid JobId,Guid AttemptId,long Fence,long MaximumBytes,DateTimeOffset ExpiresAt)` in JobContracts; create/upload/complete scoped to account/device/attempt with five-minute ticket, exact byte limit and digest. No remote absolute path argument accepted.

- [ ] **RED — Test foreign device/account, old attempt capability, path/shell payload and offline waiting.**
```csharp
[Fact]
public async Task A_foreign_account_cannot_read_a_job()
{
    await using var f = await ApiFixture.StartAsync();
    var foreign = await f.AccountAsync("telegram", "foreign-worker");
    var response = await foreign.Client.GetAsync("/v1/jobs/" + Guid.NewGuid());
    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
}
```
Extend with a real seeded owner job and verify foreign GET/cancel/claim/upload/SSE all 404/403 with no metadata. Tests use registered P2 device public-key proof, not request-body account IDs.
- [ ] **RED run:** Platform.Tests filter DesktopWorkerAuthorizationTests and Infrastructure.Tests filter DesktopWorkerTests.
- [ ] **GREEN — Require an explicit “Разрешить задания с Telegram на этом компьютере” enrollment setting.** Poll only while signed in and authorized; desktop displays incoming source/operation before any local-file selection. Site-authenticated sources open in the user's existing media browser and use local session only. The server job contains a page/source identity and quality; desktop refreshes it through existing discovery, never downloads a remotely supplied cookie file. Artifacts from preexisting local files require an explicit file picker.
```csharp
if (lease.Work.Executor != "desktop_worker" || lease.Work.DeviceId != currentDeviceId)
    throw new UnauthorizedAccessException("worker_scope_mismatch");
```
Define currentDeviceId from protected enrollment. Verify signature, issuer/audience/account/device/attempt/fence/expiry before dispatch; allow only download/mp3/trim/join/transcribe. Stream uploaded output from an owned successful job or explicitly chosen file into server-owned UUID root; server validates declared length/digest and verifies media before ledger commit. Never transmit browser cookies, passwords, proxy credentials, local arbitrary paths or shell command text.
- [ ] **GREEN run:** PC offline produces waiting_for_worker; reconnect enrollment claims own job, actual authorized synthetic browser source executes, upload verifies, one shared reservation commits. Revoke device stops polls/heartbeat and blocks new jobs while leaving local output intact.
- [ ] **Commit:** exact files; `git commit -m "feat: bridge scoped jobs to managed desktop workers"`.

### Task 4: Complete Telegram media parity and account-scoped job events

**Files — Create:** `src/VideoGrabber.Platform.Api/Telegram/BotMediaHandler.cs`; `src/VideoGrabber.Platform.Api/Jobs/JobEventEndpoints.cs`; `src/VideoGrabber.Platform.Api/wwwroot/miniapp/media.js`; `tests/VideoGrabber.Platform.Tests/TelegramMediaParityTests.cs`; `docs/platform/telegram-parity.csv`. Modify BotCommandHandler.cs, Mini App markup/CSS and source/job endpoints.

**Interfaces:** GET /v1/capabilities returns supported operation/executor pairs; POST /v1/sources/analyze accepts URL, returns AnalyzedMedia[]; POST /v1/jobs uses CreateJob; POST /v1/queue/order uses `QueueOrder(Guid[] JobIds,long Version)`; GET /v1/jobs/events streams account-filtered events with opaque event ID. SSE rechecks account revocation before emitting, never trusts a supplied account query.

- [ ] **RED — Write a data-driven parity test that executes every operation.**
```csharp
[Theory]
[InlineData("download")][InlineData("mp3")][InlineData("trim")]
[InlineData("join")][InlineData("transcribe")]
public async Task Supported_media_operation_requires_owned_inputs(string operation)
{
    await using var f = await ApiFixture.StartAsync();
    var a = await f.AccountAsync("telegram", "parity-a");
    var request = new CreateJob(Guid.NewGuid(), new string('b',64), operation, "server_worker",
        null, "unknown-source", "720p", [Guid.NewGuid()], 0, 1000);
    var response = await a.Client.PostAsJsonAsync("/v1/jobs", request);
    Assert.False(response.IsSuccessStatusCode);
}
```
Add positive fixture execution/result assertion per operation with a time grant; test accurate part durations, selected quality, reorder/remove pending only, Cancel/Continue intent reuse, result list, TXT/SRT readback, help, account/balance/linking/gifts/destinations. Payment parity routes are filled by P6, with a capability message before P6.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~TelegramMediaParityTests`.
- [ ] **GREEN — Implement each handler using owned resources and P5 worker capabilities.** Database source/job/artifact lookup always includes authenticated account_id. Queue reorder is optimistic versioned, rejects duplicates/foreign/running jobs with 409. Quality change before admission recomputes payload; after admission it is a new explicitly priced intent. Trim bounds >=0 and <=probe duration, join requires >=2 compatible owned inputs or explicit reencode operation with visible settings; ASR fixed server-selected model/language allowlist and valid nonempty TXT/SRT.
```javascript
const request = { intentId: crypto.randomUUID(), requestHash: canonicalHash,
  kind: "download", executor: selected.requiredExecutor, deviceId: selectedDeviceId,
  sourceId: selected.sourceId, quality: selectedQuality, inputArtifactIds: [],
  trimStartMs: null, trimDurationMs: null };
```
In media.js define canonicalHash using stable field serialization + Web Crypto SHA-256 of exact contract, but server recomputes as authority. Persist intent before retry within session safe state. Render stage/reason/waiting_for_worker/review_required in Russian. parity.csv columns feature, desktop_entry, bot_handler, miniapp_control, worker_capability, test, actual_result; all original parity table rows get a real result, not merely button existence.
- [ ] **GREEN run:** positive/negative parity suite and actual Mini App browser flow selecting part/quality→queue→result→MP3/trim/join/TXT/SRT; A/B event/result leakage tests.
- [ ] **Commit:** exact files; `git commit -m "feat: complete Telegram media workflows"`.

### Task 5: Large result delivery, uncertain ACK and owned retention

**Files — Create:** `src/VideoGrabber.Platform.Api/Telegram/ArtifactDeliveryService.cs`, `DeliveryWorker.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/032_delivery_attempts.sql`; `src/VideoGrabber.Platform.Worker/ArtifactRetentionWorker.cs`; `tests/VideoGrabber.Platform.Tests/DeliveryRecoveryTests.cs`; `tests/VideoGrabber.Platform.Worker.Tests/ArtifactRetentionTests.cs`; `scripts/platform/Test-TelegramTransport.ps1`; `docs/platform/delivery-runbook.md`. Modify BotApiClient and P4 delivery contracts.

**Interfaces:** `DeliveryRequest(Guid JobId,Guid ArtifactId,Guid DestinationId,Guid IdempotencyKey)`; `DeliveryView(Guid DeliveryId,string State,long? MessageId,string? TelegramFileId,string Reason)`. ArtifactDeliveryService `RequestAsync(Guid accountId,DeliveryRequest,CancellationToken)`→Task<DeliveryView>; `RetryUnknownAsync(Guid accountId,Guid deliveryId,string acknowledgedWarning,CancellationToken)`→Task<DeliveryView>. IBotApiClient adds `SendDocumentAsync(long chatId,Stream content,string fileName,CancellationToken)`→Task<BotSentMessage>. Local-path upload is available only for an owned path in a shared private Bot API artifact mount.

- [ ] **RED — Lose the ACK after emulator accepted the upload; assert one debit and delivery_unknown.**
```csharp
[Fact]
public void Unknown_delivery_requires_explicit_retry()
{
    Assert.False(ArtifactDeliveryService.MayAutomaticallyRetry("delivery_unknown"));
    Assert.True(ArtifactDeliveryService.MayAutomaticallyRetry("failed_before_send"));
    Assert.False(ArtifactDeliveryService.MayAutomaticallyRetry("delivered"));
}
```
Add HTTP integration where send accepted→network disconnected→restart; no second send until explicit action, repeated request key returns original delivery, retry uses same artifact/job. Test permissions lost between link/send, multipart one debit, retention rejects foreign/symlink/path traversal roots.
- [ ] **RED run:** Platform.Tests filter DeliveryRecoveryTests, Worker.Tests filter ArtifactRetentionTests.
- [ ] **GREEN — Persist send intent and separate delivery from media state.**
```csharp
public static bool MayAutomaticallyRetry(string state) => state == "failed_before_send";
```
Persist delivery attempts before I/O. Known Telegram message/file ID can reconcile once; uncertain upload has no API-wide idempotency promise and follows R030. On manual resend show possible duplicate explicitly. Local Bot API only private interface; authorized send obtains owning artifact handle, verifies current destination rights and block status. Do not transcode silently: if actual method limit is exceeded, user chooses lossless transport split or desktop/local download. Split records part SHA/order/reassembly manifest, one logical media job/credit.

Retention enumerates only server artifacts with expired retention, no active delivery/reader lease, verified account/job/attempt root and ownership marker; reject reparse/symlink path escape. First mark expired/unavailable transactionally, then cleanup owned files with audit; never traverse user output. Permission to enable deletion in production comes through the deployment retention configuration review. Local tests use synthetic roots.

Test-TelegramTransport.ps1 parameters `-Mode Emulator|Live -EvidenceDirectory <absolute> -BotApiBaseUri <uri>`; records actual version/method/upload path, small file, above hosted limit, near 2000MB and above current local limit. Live requires approved test chat and disk/network budget; no real send in planning. Test verifies byte size/hash and no hidden transcoding; if split, exact reconstruction hash must match.
- [ ] **GREEN run:** local emulator tests, synthetic full-file upload/download/retention and real local Bot API authorized transport cases. Missing budget/token leaves live gate BLOCKED, not PASS.
- [ ] **Commit:** exact files; `git commit -m "feat: deliver and retain private media artifacts safely"`.

## Acceptance

All parity.csv rows have real authorized handlers and meaningful result assertions. Both executors pass actual synthetic-media decode and account/attempt isolation. Last-credit concurrency, crashes, retries, cancelled/expired/blocked work and lost delivery ACK preserve accounting. No cookie/site credentials cross the desktop bridge. Linux containment, quotas and process-tree cancellation are proven in the deployed test runtime.

Run all three existing/Platform suites plus Worker.Tests, both desktop build editions, API/worker builds and Mini App GUI. Live local Bot API size/rights tests and a user-authorized private site lesson remain explicit external gates; completing this plan locally does not close those live checks.
