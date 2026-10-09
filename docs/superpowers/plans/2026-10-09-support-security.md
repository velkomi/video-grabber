# VideoGrabber protected support implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for inline implementation after plan approval, with a fresh whole-branch review. Steps use checkbox syntax.

**Goal:** Replace mail-handler links with an in-product support form, persist each accepted request once, and notify the fixed owner email and private bot chat with bounded spam controls.

**Architecture:** The existing .NET10 API owns tickets, atomic challenge consumption, caller quotas and two independent notification outboxes in PostgreSQL. Website, Mini App, Windows and explicit bot support mode use this same service. SMTP and Telegram delivery remain disabled until their recipient/sender configuration is verified; the UI never promises delivery for a merely queued request.

**Tech Stack:** Existing ASP.NET Core/Npgsql/PostgreSQL/WinUI/vanilla JavaScript. Proposed pinned MIT dependencies: Altcha1.1.0 (official NuGet package contains net8/net10 binaries and upstream commit c48002521c862e15cc08c2018378634203e9f925), local ALTCHA widget3.3.0, MailKit4.18.1. Audit/notice transitive BouncyCastle.Cryptography before adding dependencies. No AI or paid service.

**Spec:** `C:/Users/Oleg/.codex/state/plugins/codex-security/scans/videograbber-unified-20260924/48a2f1c03b8c93d113d679205c71401fce9697ee_20261009T201003Z_a00sbwh_/hardening/support-design-20261009.md`; user approved the short first-version design on 09.10.2026. This plan still awaits review before product implementation.

## Global constraints

- Owner recipients: velkoshkin@gmail.com; numeric, verified Telegram ID of @Velkoshkin, delivery from @VideoGra_bot. Never accept recipient fields from clients.
- Guest support must remain usable when sign-in fails. Authenticated account identity comes from server validation only.
- Topic allowlist: sign_in, download, subscription, suggestion, other. Contact maximum254 characters; message20–4000 characters; request body maximum16KiB. No attachments, URL fetching, HTML interpretation or automatic diagnostic upload.
- ALTCHA five-minute, signed support-action challenges; atomic, representation-stable replay claims in PostgreSQL. No Sentinel or remote ML classification. Pin and self-host the widget.
- Starter caller budgets: 1/minute, 3/15minutes, 10/day; wider trusted-IP ceiling30/hour for shared networks. Guest contact is not a verified identity. Limit challenge issuance and invalid submissions before expensive verification.
- Notification budgets per channel60/hour and200/day; bounded pending queue1000; explicit backpressure when full, no unlimited retry or implicit loss of accepted tickets. Three retry attempts for confirmed transient rejection; ambiguous timeout becomes review_required.
- No restart bypass: quotas, replay claims, request dedupe and delivery claims persist in PostgreSQL. One ticket and two unique channel records per idempotency key; different-body reuse rejects.
- Text displayed/sent as plain text, no link previews. Customer content never enters application logs, arbitrary mail headers, public telemetry or Git.
- Storage retention proposed90days after closure, pending owner decision; no automatic deletion in this implementation. Existing records and infrastructure remain intact.
- No full production release or real notification smoke until sender/recipient checks and owner permission are satisfied. Source updates/deployment remain within the existing authorized project.

## Review focus

1. Shared NAT and reverse-proxy IP ambiguity must not make one customer block all others.
2. Crash/timeout after SMTP/BotAPI acceptance must not create endless duplicate notifications.
3. A visitor unable to sign in must still be able to submit a legitimate request.
4. Equivalent challenge representations and concurrent submits must not bypass one-use limits.
5. Malicious HTML/CRLF/URLs and client-controlled account or recipient fields must cross no trust boundary.

## Task 1: Repair the two confirmed authentication controls

**Files:** modify `src/VideoGrabber.Platform.Api/Program.cs`, `Telegram/MiniAppAssertionValidator.cs`, `Telegram/TelegramSessionEndpoints.cs`; create `Security/ClientRateLimitPolicies.cs`; tests `tests/VideoGrabber.Platform.Tests/TelegramAuthTests.cs`, new `AuthRateLimitIsolationTests.cs`; extend fixtures narrowly.

**Interfaces:** `ClientRateLimitPolicies.Add(...)` registers separate public-config and auth policies. Identity uses authenticated account or IP resolved exclusively through explicitly trusted proxy hops. Replay identity is derived from the verified canonical signed fields and bot scope.

- [ ] Add failing tests `Equivalent_init_data_has_same_replay_identity` for reorder/percent-encoding and `Concurrent_equivalent_assertions_issue_one_session`.
- [ ] Add failing tests `Client_A_exhaustion_does_not_block_B`, `Public_config_does_not_consume_login_budget`, and `Untrusted_forwarded_header_is_ignored`.
- [ ] Run targeted tests; record actual failures. No live attack on VPS.
- [ ] Implement canonical replay identity, preserve HMAC/age/duplicate-fields guards, and document five-minute transition protection for previously used raw hashes. Do not delete existing assertions.
- [ ] Implement caller-separated policies and explicit trusted-proxy configuration. Confirm actual Traefik/nginx/API chain before production enablement; never clear all KnownProxies/KnownNetworks to trust any sender.
- [ ] Run targeted and existing auth/link/desktop handoff tests, then commit the bounded fix separately.

## Task 2: Support ticket and anti-abuse API

**Files:** create `src/VideoGrabber.Platform.Contracts/SupportContracts.cs`, `Api/Support/SupportEndpoints.cs`, `SupportChallengeService.cs`, `SupportStore.cs`, `SupportOptions.cs`, migration `Persistence/Migrations/067_support_requests.sql` (check next available number), and `tests/VideoGrabber.Platform.Tests/SupportRequestTests.cs`. Modify Program.cs, central package/lock files, integration fixture and migration grants.

**Interfaces:** `GET /v1/support/config` returns public enabled/topics/limits only; `GET /v1/support/challenge` returns ALTCHA protocol data; `POST /v1/support/requests` takes `{requestId,topic,contact,message,altcha,website}` and returns `{ticketId,status:"accepted"}`. No public ticket enumeration/read API. `website` is the supplemental honeypot. `SupportStore.SubmitAsync(...)` owns transactional quotas/replay/dedupe/outbox creation.

- [ ] Test guest/auth submit, caller identity injection rejection, malformed/oversized/streamed body, input ranges, concurrent idempotency, changed-body same-key409, spoofed XFF, challenge expiry/tamper/reorder/reuse and quota persistence.
- [ ] Run tests red; check published Altcha1.1.0 README/API rather than assuming GitHub main APIs match the package.
- [ ] Add pinned Altcha dependency and notices, implement signed challenge creation/verification, and atomically consume challenge identity in the ticket transaction. Rate-limit challenge creation independently.
- [ ] Implement dedicated limited-privilege schema/grants and store. Use existing encryption patterns with separated key purpose; do not place request text/contact in logs. Do not reuse the webhook secret for customer support.
- [ ] Enforce budgets, bounded queue and explicit503/429 states. Backend disabled/unconfigured must fail closed, not produce a false success.
- [ ] Run API/DB race/isolation regressions and commit.

## Task 3: Independent bounded notifications

**Files:** create `Api/Support/SupportNotificationWorker.cs`, `SupportMailTransport.cs`, `SupportTelegramTransport.cs`, `SupportNotificationText.cs`; modify Program.cs and pinned package/locks; add `SupportDeliveryTests.cs`, SMTP test adapter and BotAPI emulator cases. Add setup instructions without credential values.

**Interfaces:** `ISupportNotificationTransport.SendAsync(notification,ct)` returns delivered/confirmed_failure/unknown with safe codes. Each channel claims its unique ticket row atomically; counters persist and worker uses leases. Transport recipient comes only from verified server options.

- [ ] Test SMTP down independently of Telegram,429/403/TLS rejection, global hourly/daily limits, crash/lease expiry, duplicate claims and ambiguous timeout without blind re-send.
- [ ] Test plain-text escaping/truncation: full ticket remains in the store, Telegram summary fits4096, mail header CRLF never accepted; no attachment/URL retrieval.
- [ ] Add MailKit4.18.1 and dependency notices; implement verified TLS sender and fixed owner recipient. No personal password or credential in source/console.
- [ ] Implement owner-only Telegram destination verification from the existing trusted account link and started bot dialog; no hardcoded guessed numeric IDs and no username-based authorization.
- [ ] Provide three-attempt confirmed-error retry with backoff and review_required for unknown send; bound enqueue/dispatch globally, add redacted counters/health/backlog.
- [ ] Test via local emulators only until real sender and recipient are configured; commit.

## Task 4: Website, Mini App, bot and Windows support UI

**Files:** modify `Api/wwwroot/info/index.html`, `info/app.js`, `info/styles.css`; create locally hosted support JS/CSS/vendor widget/notice; modify `miniapp/index.html`, `miniapp/app.js`, `miniapp/styles.css`, `Telegram/BotCommandHandler.cs`; create `App/MainWindow.Support.cs`, modify `MainWindow.ProductInformation.cs`, and `Infrastructure/Licensing/SupportApiClient.cs`; add web tests and native/integration UI wiring tests.

**Interfaces:** Every UI calls Task2 routes. Web modal owns accessible focus/Escape, preserves draft on retry, and uses one in-flight idempotency key. Authenticated bot mode skips browser-only widget only after trusted webhook identity and the same store quotas; it is a separate trusted submit path, never a client parameter that bypasses challenge. Windows uses published .NET ALTCHA solver with bounded cancellation, or an authenticated support path after server session verification; never a forgeable source flag.

- [ ] Test short form, guest access, successful ticket number, invalid challenge,429, network retry preserving draft/key, double click, focus return and responsive layout.
- [ ] Replace «Задать вопрос» mailto with form; retain explicit email/Telegram fallbacks under Contacts. Use existing Studio fonts/buttons and concise customer text.
- [ ] Add Mini App support view and bot «Поддержка»/`/support` with explicit support mode/cancel. Owner notification messages must not become new support tickets.
- [ ] Add native ContentDialog/embedded page with logical input order, progress and cancellation; keep auth and tokens out of request URLs. No embedded general-purpose browser or automatic raw-log attachments.
- [ ] Update privacy/help/contact descriptions to actual support dataflow with versioning. Do not claim external legal filings are complete.
- [ ] Capture fresh desktop/mobile screenshots, verify all four entry points; native GUI acceptance may require user interaction if automation remains unavailable. Run regression suites and commit.

## Task 5: Security headers, review and gated production acceptance

**Files:** selected proxy/static serving configuration, release/change reports and support runbook; endpoint-header and UI regression tests.

- [ ] Define distinct CSP policies for website and Telegram embedding, start Report-Only with a bounded redacted collector; validate Google/email sign-in, local fonts/3D/ALTCHA and Telegram iframe. Avoid blanket X-Frame-Options DENY for Mini App.
- [ ] Stage HSTS without preload/includeSubDomains until all owned domains are verified; Permissions-Policy disables unused capabilities only.
- [ ] Run affected unit/integration/web builds, test DB permissions/migrations against disposable database, and obtain a fresh independent whole-branch review. Fix Important/Critical regressions before release.
- [ ] Configure sender and verified owner Telegram numeric destination securely. With authorization send one labelled test notification to each owner channel and inspect both received messages; do not treat SMTP enqueue or BotAPI HTTP200 alone as full acceptance.
- [ ] Deploy only the agreed project services with rollback, compare neighboring containers, verify readiness and public UI/disabled-channel handling. No automatic WAF/firewall installation or old-data deletion.
- [ ] Record exact source/image/migration/tests/screenshots/channel status. Leave delivery gates and full installer/publication gaps explicit if credentials or GUI acceptance are still pending.

## Separate follow-up, not part of this rollout

CrowdSec+bouncer or OWASP CRS, provider-level DDoS purchase, host firewall/SSH redesign, wholesale authentication migration and exhaustive audit of every repository file are separate evaluated stages. Current work must not present these as installed/proven protections.
