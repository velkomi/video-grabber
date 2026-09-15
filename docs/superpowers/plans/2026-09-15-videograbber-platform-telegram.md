# VideoGrabber Telegram Bot and Mini App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a working Telegram account/admin product with verified assertions, durable webhook ingestion and private destination management.

**Architecture:** Host typed Bot API adapters and account handlers in the .NET API; serve Mini App static HTML/CSS/ES modules from an exact HTTPS origin. Persist dedupe and callback capabilities in PostgreSQL; account and admin operations call the same P1/P2 services.

**Tech Stack:** .NET 10, HttpClient/System.Text.Json, ASP.NET Core, PostgreSQL, browser ES modules; no Telegram or npm SDK.

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

P1/P2 locally accepted; use the already-defined AccountProfile, AccessSnapshot, GrantRequest and DeviceReceipt contracts. Covers VG-GAP-012/014 and Telegram portions of 006/013/021. P4 account/admin/destination functionality is fully usable without workers. P5 owns media submission/execution/parity and installs those handlers; before P5 GET /v1/capabilities returns media_available=false and the UI explains the missing worker, without pretending media buttons work.

No real bot token, webhook modification or message is needed for local acceptance; TelegramApiEmulator receives real HTTP adapter calls. Live test bot use must already be authorized. New dependencies: none.


### Task 1: Verify initData and accept webhook updates durably

**Files — Create:** `src/VideoGrabber.Platform.Api/Telegram/MiniAppAssertionValidator.cs`, `TelegramWebhookEndpoints.cs`, `TelegramUpdateInbox.cs`, `TelegramSessionEndpoints.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/020_telegram_inbox.sql`; `tests/VideoGrabber.Platform.Tests/TelegramAuthTests.cs`; `tests/VideoGrabber.Platform.Tests/TelegramWebhookTests.cs`; `tests/VideoGrabber.Platform.Tests/TelegramApiEmulator.cs`. Modify API Program.cs and test ApiFixture.cs (emulator BotApi HttpClient override).

**Interfaces:**
```csharp
public sealed record TelegramPrincipal(long UserId, DateTimeOffset AuthTime, string AssertionHash);
public sealed record TelegramUpdate(long UpdateId, JsonElement Body);
public interface IMiniAppAssertionValidator
{
 TelegramPrincipal Validate(string initData, DateTimeOffset now);
}
public interface ITelegramUpdateInbox
{
 Task<bool> AcceptAsync(TelegramUpdate update, CancellationToken cancellationToken);
}
```
Validator constructor takes a server-only bot token and TimeSpan maxAge=5m. POST /v1/telegram/session consumes raw initData, validates once, maps numeric user ID to a unique Telegram identity account and issues P1 API session; POST /v1/telegram/webhook validates a separate webhook secret and writes inbox row, then returns 200. No webhook media processing runs inline.

- [ ] **RED — Test tamper/age/duplicates and durable update replay.**
```csharp
[Fact]
public async Task Duplicate_update_is_only_inserted_once()
{
    await using var f = await ApiFixture.StartAsync();
    var inbox = new TelegramUpdateInbox(f.Database);
    var update = new TelegramUpdate(100, JsonSerializer.SerializeToElement(new { update_id = 100 }));
    var accepted = await Task.WhenAll(Enumerable.Range(0, 20)
        .Select(_ => inbox.AcceptAsync(update, CancellationToken.None)));
    Assert.Single(accepted.Where(value => value));
}
```
Define test helper `SignInitData(Dictionary<string,string>,string botToken)` inside TelegramAuthTests using documented HMAC and query escaping; test unknown hash, repeated auth_date/hash/user keys, future auth_date>30s, >5m age, null user, negative/overflow ID, extra data length>16KiB, replay and forged initDataUnsafe. Raw Bot API update user_id is trusted only after secret header verification.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter "FullyQualifiedName~TelegramAuthTests|FullyQualifiedName~TelegramWebhookTests"`.
- [ ] **GREEN — Implement exact parser and fixed-time verification.**
```csharp
var checkString = string.Join("\n", fields.Where(p => p.Key != "hash")
    .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value));
var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(checkString));
if (!CryptographicOperations.FixedTimeEquals(expected, suppliedHash))
    throw new UnauthorizedAccessException("telegram_assertion_invalid");
```
Parse form-urlencoded once, reject duplicate fields before dictionary creation, hex hash length 64, documented HMAC branch includes all fields other than hash (do not confuse with third-party Ed25519 rules). Validate auth_date and JSON user.id after signature. Persist assertion SHA-256 and five-minute replay TTL; repeated assertion may return the same session only with an already-bound secure session cookie, otherwise reject. Do not log raw initData/hash.
```sql
insert into licensing.telegram_updates(update_id,payload,received_at,state)
values(@id,@body,now(),'pending') on conflict(update_id) do nothing;
```
Encrypt sensitive inbox payload at rest, store minimum fields, strip access/cookie fields, retention 24h after handled. Ingestion compare webhook secret fixed-time, body <=1MiB, return 401 for invalid secret, 503 on DB failure so Telegram retries. Worker can claim pending update using FOR UPDATE SKIP LOCKED. update_id is durable dedupe, never a per-process dictionary.
- [ ] **GREEN run:** suites plus process restart between duplicate updates; row count stays one, HTTP acceptance <1 second under a 100-update synthetic burst.
- [ ] **Commit:** exact task files; `git commit -m "feat: authenticate Telegram sessions and durable updates"`.

### Task 2: Actual bot commands, callback authorization and Mini App account UI

**Files — Create:** `src/VideoGrabber.Platform.Api/Telegram/BotApiClient.cs`, `BotCommandHandler.cs`, `BotCallbackStore.cs`, `TelegramInboxWorker.cs`; `src/VideoGrabber.Platform.Api/wwwroot/miniapp/index.html`, `app.js`, `styles.css`; `src/VideoGrabber.Platform.Persistence/Migrations/021_bot_callbacks.sql`; `tests/VideoGrabber.Platform.Tests/BotAccountWorkflowTests.cs`; `tests/VideoGrabber.Platform.Tests/MiniAppUiTests.cs`. Modify TelegramApiEmulator.cs and Program.cs.

**Interfaces:**
```csharp
public sealed record BotMessage(long ChatId, string Text, JsonElement? ReplyMarkup = null);
public sealed record BotSentMessage(long ChatId, long MessageId);
public interface IBotApiClient
{
 Task<BotSentMessage> SendMessageAsync(BotMessage message, CancellationToken cancellationToken);
 Task AnswerCallbackAsync(string callbackQueryId, string text, CancellationToken cancellationToken);
}
```
BotCommandHandler `HandleAsync(TelegramUpdate,CancellationToken)`→Task; CallbackStore `CreateAsync(Guid accountId,string action,Guid? resourceId,CancellationToken)`→Task<string> random 128-bit opaque token, consume single-use <=5m, bound to actor/action/resource. Store raw 64-bit Telegram IDs as long/bigint, never int.

- [ ] **RED — Build a real HTTP emulator account/help/admin workflow.**
```csharp
[Fact]
public async Task Telegram_and_desktop_see_the_same_account_balance()
{
    await using var f = await ApiFixture.StartAsync();
    var desktop = await f.AccountAsync("telegram", "5001");
    var admin = await f.AdminAsync();
    (await admin.PostAsJsonAsync("/v1/admin/grants",
        new GrantRequest(desktop.Id, "credits", 0, 3, null, "shared gift", Guid.NewGuid()))).EnsureSuccessStatusCode();
    var profile = await desktop.Client.GetFromJsonAsync<AccountProfile>("/v1/me");
    var access = await desktop.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
    var miniApp = await f.MiniAppAsync(5001);
    var telegramProfile = await miniApp.GetFromJsonAsync<AccountProfile>("/v1/me");
    var telegramAccess = await miniApp.GetFromJsonAsync<AccessSnapshot>("/v1/access");
    Assert.Equal(profile!.AccountId, telegramProfile!.AccountId);
    Assert.Equal(access, telegramAccess);
    Assert.Contains("telegram", profile!.LinkedProviders);
    Assert.Equal(3, access!.RemainingDownloads);
}
```
ApiFixture exposes `Task<HttpClient> MiniAppAsync(long userId)` implemented with the signing helper and real /v1/telegram/session route, no direct account seeding shortcut. Test callback stolen by account B, stale callback, duplicate callback, admin keyboard forged by ordinary user, job/resource ID ownership via callback resolver.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter "FullyQualifiedName~BotAccountWorkflowTests|FullyQualifiedName~MiniAppUiTests"`.
- [ ] **GREEN — Implement /start, /account, /balance, /link, /devices, /destinations, /help and authorized /admin.** All commands look up account from verified update sender, not message arguments; group chats get a private-account link instead of private profile data. /admin exposes lookup/gift/revoke/block/unblock/reset/audit; for sensitive action lacking fresh MFA send a one-use link to P2 console's MFA flow, never infer MFA from Telegram ownership.

Mini App uses window.Telegram.WebApp.initData only to exchange once with backend; app state uses API session/CSRF protections. No role from initDataUnsafe. Account, balance, linked providers, device list, admin actions and destination forms perform actual API operations. Render text with textContent, no innerHTML of user/title/error. Example:
```javascript
const response = await fetch("/v1/access", { credentials: "same-origin" });
if (!response.ok) throw new Error("Не удалось получить доступ");
const access = await response.json();
document.querySelector("#balance").textContent = String(access.remainingDownloads);
document.querySelector("#access-reason").textContent = access.reason;
```
Responsive CSS for 320px–desktop, focus visible, labels, aria-live status, loading/empty/error states. Native browser node:test can test pure exported render/mapping functions, but actual browser interaction verifies controls. Media capability cards show unavailable until P5 registers an executor; help describes desktop worker requirement.
- [ ] **GREEN run:** suites and real Mini App in browser with emulator account, inspect account A/B isolation, gift from admin, refresh exact credits, keyboard navigation and 320px viewport. Store synthetic screenshots only.
- [ ] **Commit:** exact files; `git commit -m "feat: add Telegram account and admin workflows"`.

### Task 3: Verify destination ownership at linking and sending

**Files — Create:** `src/VideoGrabber.Platform.Contracts/DeliveryContracts.cs`; `src/VideoGrabber.Platform.Api/Telegram/DestinationService.cs`; `DestinationEndpoints.cs` in same directory; `src/VideoGrabber.Platform.Persistence/Migrations/022_destinations.sql`; `tests/VideoGrabber.Platform.Tests/DestinationAuthorizationTests.cs`. Modify BotApiClient and Mini App destination screen.

**Interfaces:**
```csharp
public sealed record DestinationRequest(long ChatId, string Title, string Proof);
public sealed record DestinationChallenge(string Proof, DateTimeOffset ExpiresAt);
public sealed record DeliveryDestination(Guid DestinationId, Guid AccountId, long ChatId,
    string Kind, bool Revoked);
public sealed record TelegramChatRights(bool UserCanPublish, bool BotCanPublish, string Kind);
```
IBotApiClient adds `GetRightsAsync(long chatId,long userId,CancellationToken)`→Task<TelegramChatRights>. DestinationService `BeginAsync(Guid accountId,long chatId,CancellationToken)`→Task<DestinationChallenge>, `LinkAsync(Guid accountId,DestinationRequest,CancellationToken)`→Task<DeliveryDestination>, `AuthorizeSendAsync(Guid accountId,Guid destinationId,CancellationToken)`→Task<DeliveryDestination>, `RevokeAsync(Guid accountId,Guid destinationId,CancellationToken)`→Task. POST /v1/destinations/challenges starts the single-use permission check and returns its opaque proof; a verified bot interaction or fresh user/bot membership read completes it before link consumption.

- [ ] **RED — Deny arbitrary chat_id, wrong owner, revoked bot rights and 32-bit truncation.**
```csharp
[Fact]
public async Task An_account_cannot_revoke_another_accounts_destination()
{
    await using var f = await ApiFixture.StartAsync();
    var a = await f.AccountAsync("telegram", "7001");
    var b = await f.AccountAsync("telegram", "7002");
    var challengeResponse = await a.Client.PostAsJsonAsync("/v1/destinations/challenges",
        new { chatId = -1001234567890L });
    challengeResponse.EnsureSuccessStatusCode();
    var challenge = await challengeResponse.Content.ReadFromJsonAsync<DestinationChallenge>();
    var linked = await a.Client.PostAsJsonAsync("/v1/destinations",
        new DestinationRequest(-1001234567890L, "Synthetic private channel", challenge!.Proof));
    linked.EnsureSuccessStatusCode();
    var destination = await linked.Content.ReadFromJsonAsync<DeliveryDestination>();
    var response = await b.Client.PostAsJsonAsync($"/v1/destinations/{destination!.DestinationId}/revoke", new { });
    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
}
```
Set emulator membership explicitly for this synthetic channel and user 7001; the proof above comes from the actual service route and is consumed only after fresh rights checks. No magic proof string is accepted.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~DestinationAuthorizationTests`.
- [ ] **GREEN — Persist account-bound challenge and check user/bot chat membership.** Private direct destination requires the verified user to start the bot and reply to the challenge; channel/group requires user creator/admin publish capability and bot publish capability, freshly fetched before save. Recheck both on every send. Private account mapping and RLS constrain destination ID. Never accept a channel merely because the bot belongs to it.
```csharp
var rights = await bot.GetRightsAsync(request.ChatId, telegramUserId, cancellationToken);
if (!rights.UserCanPublish || !rights.BotCanPublish)
    throw new UnauthorizedAccessException("destination_permission_required");
```
Define local `bot` via IBotApiClient injection and telegramUserId by linked verified identity in DestinationService. GET /v1/destinations lists only own rows; POST revoke is idempotent and does not delete historical delivery rows.
- [ ] **GREEN run:** targeted suite; rights lost after link causes denied send and actionable message; live private/channel target test stays a separate authorized gate.
- [ ] **Commit:** exact files; `git commit -m "feat: verify private Telegram delivery destinations"`.

## Acceptance

Actual webhook adapter→durable inbox→handler→Bot API emulator flow passes; replay/forgery and cross-account callbacks fail. A real browser Mini App signs in using synthetic signed initData and sees the same account/grants/devices as desktop; admin actions require server role and MFA. Destination linking/revalidation passes with 64-bit IDs.

Live test bot, HTTPS origin, login return, Telegram permissions and real messaging are recorded only after authorized tests. P5 owns analyze, quality selection, queue/results/MP3/trim/join/TXT/SRT delivery; a capabilities message in P4 does not close VG-GAP-013.
