# VideoGrabber Platform: Authentication, Licensing, Telegram and Payments Design

**Date:** 2026-09-15`n**Status:** Approved architecture, pre-implementation`n**Branch:** `feature/platform-auth-licensing-design`

## 1. Goal

Extend VideoGrabber from a local Windows application into one account-based platform shared by:
- the Windows desktop application;
- a Telegram bot and Mini App;
- an administrator console;
- future paid access.

The platform must preserve the working local downloader while making access, gifts, purchases and download allowances consistent across all clients.

> **2026-09-21 superseding implementation note.** The current product rules are defined in `docs/platform/2026-09-21-web-account-plans-implementation.md`. In particular, a new account now receives exactly 10 lifetime Free downloads, Start is limited to 10 logical downloads per UTC day, Unlimited Video excludes full-course download, Full Course includes it, and Telegram identity alone is no longer sufficient for protected media jobs. Telegram must be linked to a main account whose origin is not Telegram (launch UX: Google or verified e-mail). Where this older design conflicts with that note, the 2026-09-21 note controls.

## 2. Core decisions

- Authentication providers: Google, Apple, Yandex, Telegram, plus verified email as recovery/fallback.
- One VideoGrabber account may have several linked identities.
- Telegram identity alone is sufficient for synchronization; email is recommended but not mandatory for the same verified Telegram identity.
- Guest accounts remain guests until a paid entitlement exists; gifts do not change the role to purchaser.
- Owner/Admin has permanent unlimited usage and does not consume download credits.
- Guest Windows device limit: 1 active computer.
- Admin Windows device limit: 3 active computers.
- Offline grace after a successful online license check: 24 hours for Guest/User, 72 hours for Admin.
- Download-credit spending requires an online reservation; offline clients may use time-based access but may not spend shared credits.
- Existing stable/local releases remain separate from the managed edition; no attempt is made to retroactively lock old binaries.

## 3. Recommended architecture

Use a hybrid architecture:

1. **Supabase Auth** handles external identity providers and account sessions.
2. **VideoGrabber Licensing API** on the user's VPS is the authority for roles, devices, gifts, purchases, entitlements and reservations.
3. **PostgreSQL** stores account/business state. Auth identity data and licensing data are linked by stable internal user IDs.
4. **Windows client**, **Telegram bot/Mini App**, and future web/admin interfaces call the same Licensing API.
5. **Media workers** execute server-side jobs where permitted; a connected Windows worker handles site-authenticated jobs that require a local browser session.

The client must never decide locally that a paid entitlement is valid. It may only cache a signed offline lease issued by the Licensing API.

## 4. Account and identity model

A VideoGrabber account has one internal `account_id` independent of provider email/username.

Linked identities are stored separately:
- Google subject ID;
- Apple subject ID;
- Yandex user ID;
- Telegram user ID;
- verified email identity.

Linking a new identity requires proof of control of both the existing account session and the new provider identity. Accounts must not auto-merge merely because provider emails match.
If a user accidentally creates two paid accounts, merging is an explicit administrative flow that reconciles entitlements and audit history.

## 5. Roles and entitlements

Roles and paid rights are independent concepts.

### Roles
- `owner_admin`: platform owner; unlimited usage; administrative privileges.
- `user`: ordinary registered user.
- `guest`: registered but unpaid user.
- `blocked`: account disabled from protected operations.

### Entitlements
An account may hold zero or more entitlement grants:
- unlimited permanent access;
- time access (`valid_from`, `valid_until`);
- download credits (`remaining_downloads`);
- hybrid grants (`N` downloads before an expiry date);
- feature flags for future plans.

A gift is represented as an entitlement grant with source `admin_gift`. A payment grant uses source `purchase`. Gifts do not alter the role. A newly registered account starts as `guest`; the first confirmed purchase may transition it to `user`, while gifted access alone leaves it `guest`.

The effective access evaluator combines active grants deterministically and produces a server response such as:
`can_download`, `can_edit`, `valid_until`, `remaining_downloads`, `offline_until`, and `reason`.
## 6. Download-credit accounting

A credit represents one logical requested media item, not transport fragments.

Do not charge additional credits for:
- HLS segments;
- retries/resume of the same logical job;
- Telegram multipart delivery of one completed file;
- re-sending an already completed result where no new download occurs.

Six independent lesson parts are six logical downloads.

Credit spending is transactional:
1. client requests a reservation for a logical job;
2. server atomically reserves one credit if available;
3. job executes;
4. success commits the reservation;
5. an eligible confirmed failure releases it.

Reservations have an idempotency key and expiry. Concurrent desktop and Telegram requests cannot both spend the last available credit.

For local desktop jobs, server-side credit accounting cannot prove every locally reported failure. Refund policy for ambiguous client-side failures must therefore be conservative and auditable.

## 7. Devices and sessions

A device registration contains a server-generated ID, account ID, friendly name, platform, first/last seen timestamps and revocation state. Raw hardware identifiers are not used as account identity.
Guest/User accounts may have one active Windows device. Owner/Admin may have three. A new device beyond the limit is rejected until an existing registration is revoked by the user (where allowed) or Admin.

Telegram is an account identity/channel and does not consume a Windows device slot.

Access tokens are short-lived. Refresh/session secrets are stored using OS-protected local storage on Windows and server-side secure storage for bots/workers. They are never logged.

## 8. Offline lease

The Licensing API may issue a signed offline lease containing only the minimum claims needed by the client:
- account ID (opaque identifier);
- role class;
- permitted features;
- issue and expiry times;
- device ID;
- lease ID/version.

Guest/User lease maximum: 24 hours. Admin lease maximum: 72 hours.

Offline leases never include reusable download-credit balances. Shared credit spending requires an online reservation to prevent double spending across Telegram and desktop.

Revocation takes effect immediately for online clients and no later than the cached lease expiry for an offline client.

## 9. Desktop application behavior

The managed edition starts with an account gate. The application shell may open while signed out, but protected actions are disabled until authorization succeeds.

Protected actions include direct download, browser-discovered download, queue execution, server-bound processing and any future paid-only feature. Existing local media files are never deleted because access expires.
Sign-in uses the system browser or provider-approved authorization surface. The media-discovery WebView2 remains a separate site-session browser and must not be treated as the VideoGrabber identity provider browser.

Account page shows linked providers, effective access, expiry, remaining credits, active device(s), and Telegram link state.

## 10. Telegram product

The Telegram surface has three components:
- Bot chat for commands, notifications, queue summaries and completed-file delivery;
- Mini App for richer lists, quality selection, queue editing, account/balance and admin workflows;
- optional private channels as delivery/archive destinations.

The bot and Mini App use the same account and entitlement service as desktop. Telegram `user_id` is linked to exactly one VideoGrabber account unless an explicit merge operation is performed.

Mini App initialization data and Telegram login assertions are validated server-side. Client-supplied Telegram profile fields are never trusted as authorization by themselves.

User-facing bot functions should include:
- submit URL / analyze;
- choose video and quality;
- queue management;
- download status and retry;
- MP3 extraction;
- trim/join/transcription jobs where worker support exists;
- account, balance and linked identities;
- delivery destination and help.

Admin-only bot functions include account lookup, grant/revoke days or credits, block/unblock, device reset and audit summary.
## 11. Execution model

Jobs have an explicit executor type:
- `server_worker`: public/direct sources that can be processed on VPS without the user's private site session;
- `desktop_worker`: tasks requiring the user's authenticated local browser/site session;
- future specialized worker types may be added without changing entitlement semantics.

A desktop worker registers under the user's account and pulls only jobs assigned to that account/device. Course cookies and site passwords are not sent through Telegram messages or persisted in the licensing database.

If a desktop-only job is requested while the authorized computer is offline, the job stays `waiting_for_worker` and the user is told why.

## 12. Telegram file delivery

For normal bot delivery use the Telegram Bot API. For large generated files deploy the official local Telegram Bot API server on the VPS and verify its actual supported upload path/limits during acceptance.

Delivery rules:
- result files are private to the owning account unless explicitly sent to an authorized destination;
- a user may link a destination only after proving permission to use it;
- bot publication rights in a channel are checked before saving it as a target;
- splitting one logical result for Telegram transport does not spend additional download credits;
- do not use Telegram as the authoritative database or permanent server-side object store.

Server job artifacts have explicit retention and cleanup rules. Desktop output files are outside server retention and remain under user control.

## 13. Payments

Payment support is implemented only after gifts and entitlement accounting pass end-to-end acceptance.

Inside Telegram, digital-service purchases use Telegram Stars where required by Telegram policy. Payment handlers must be idempotent and support reconciliation/refunds.
External desktop/web payments may use a separate provider. Payment confirmation is accepted only from verified provider callbacks or reconciliation, never from a client-side "paid" flag.

Purchases create immutable payment records and entitlement grants. Repeated callbacks cannot duplicate a grant.

## 14. Data model

Minimum server tables/entities:
- `accounts` — internal user/account record and role;
- `identities` — provider, provider subject, verified metadata, link timestamps;
- `devices` — device registrations and revocation;
- `entitlement_grants` — gifts, purchases, permanent admin grant;
- `credit_ledger` — immutable additions, reservations, commits, releases and adjustments;
- `offline_leases` — issuance/revocation metadata, not raw client secrets;
- `jobs` — logical media operations and executor assignment;
- `job_attempts` — retry/worker history;
- `delivery_destinations` — Telegram chat/channel destinations with ownership verification;
- `payments` — provider transaction state and idempotency key;
- `account_links` / link challenges — short-lived identity-link operations;
- `audit_events` — administrative and security-sensitive actions.

Business history is append-oriented where practical. Current balances are derived transactionally or maintained with database constraints, not trusted from client counters.

## 15. Licensing API boundaries

The Licensing API exposes versioned endpoints for:
- current account/profile;
- identity linking/unlinking;
- device registration/revocation;
- effective entitlement evaluation;
- offline lease issuance;
- credit reservation/commit/release;
- job creation/status/cancel;
- Telegram destination linking;
- admin grants, blocks and device resets;
- payment callback/reconciliation endpoints.
All protected mutations require authenticated account context plus server-side authorization. Admin authorization is checked by role on the server for every action.

## 16. Security requirements

- No social-provider passwords are handled by VideoGrabber.
- No Supabase service-role key or payment secret is embedded in the desktop application, bot Mini App or public repository.
- Access/refresh tokens, OAuth codes, Telegram login signatures, cookies and signed media URLs are redacted from logs.
- OAuth flows use provider-supported browser redirects and PKCE where applicable.
- Redirect URIs are exact allow-listed values.
- Identity-link challenges are single-use and expire quickly.
- Database row-level/security boundaries prevent users reading other users' grants, devices, jobs or payments.
- Worker job payloads contain only information necessary for that worker.
- Server-side URL ingestion applies SSRF protection and rejects internal/private network targets unless explicitly required by trusted infrastructure.
- Administrative changes and entitlement adjustments are auditable.
- Rate limits apply to login/link attempts, job creation and payment-sensitive operations.

## 17. Failure behavior

Authentication failure never grants a cached paid role. Licensing API/network failure may use only an unexpired signed offline lease.

Expired time access disables new protected operations but does not delete completed files.

If credit reservation fails, the job is not started. If a worker fails before a reservation is committed, release follows the explicit failure/refund policy.

If Telegram delivery fails after successful media creation, the completed logical download is not automatically downloaded again; delivery is retried separately.
## 18. Admin experience

Admin can:
- find an account by internal ID or linked identity;
- view role, linked providers, active device(s), effective grants and recent jobs;
- gift days, credits, or a combined expiring package;
- revoke an unused/revocable grant according to policy;
- block/unblock an account;
- revoke/reset devices and sessions;
- inspect payment and entitlement history;
- view security/audit events relevant to support.

Admin unlimited usage is represented explicitly as a permanent grant/role rule, not as an artificially huge credit number.

## 19. Implementation decomposition

This architecture is delivered as separate implementation phases:
1. shared account/auth foundation and Licensing API skeleton;
2. entitlement engine, gifts, credits, devices and admin operations;
3. desktop managed-edition account gate and profile;
4. Telegram bot + Mini App identity/account integration;
5. server media worker and large Telegram delivery;
6. desktop-worker bridge for authenticated local-site jobs;
7. payments and reconciliation;
8. production hardening, monitoring, backup/recovery and release.

Each phase must leave the prior working VideoGrabber media functionality regression-tested.

## 20. Acceptance criteria

The design is accepted only when automated and live tests demonstrate all of the following:
- the same verified Telegram/Google/etc. identity returns the same VideoGrabber account;
- unverified email equality cannot merge accounts;
- Guest with no grant cannot start a protected download;
- Admin can use protected operations without credit depletion;
- Guest device #2 is rejected until the first device is revoked; Admin supports three devices;
- 24h/72h offline leases expire and are rejected correctly;
- one remaining credit cannot be spent concurrently by desktop and Telegram;
- gifted days/credits appear identically in desktop and Telegram;
- a Telegram purchase is visible to desktop after the same account is used;
- linking and unlinking providers preserves account ownership and prevents takeover;
- Telegram jobs cannot read another user's files or status;
- a server-worker job and a desktop-worker job both follow the same entitlement accounting;
- retries and Telegram multipart delivery do not double-charge;
- payment callback replay cannot duplicate access;
- blocked/revoked accounts cannot obtain new protected operations;
- secrets and provider tokens do not appear in application logs or Git artifacts;
- existing VideoGrabber downloader regression suites remain green.

## 21. Non-goals for the first implementation phases

- Do not attempt DRM, CAPTCHA, paywall or account-control bypass.
- Do not upload arbitrary course cookies to Telegram chat.
- Do not promise autonomous server downloads for sites that require an interactive private user session until a separate secure browser-session design is approved.
- Do not treat client-side code as an unbreakable licensing boundary; valuable server services and shared balances are enforced server-side.
- Do not delete existing user media when access expires.
- Do not make payment integration a prerequisite for testing accounts and gifts.

## 22. Compatibility and release policy

The existing open/local releases remain usable as previously published. The managed account edition is introduced as a new version/edition with explicit release notes.

The server API is versioned so desktop and bot can roll forward independently within a documented compatibility window. Unsupported clients receive a clear upgrade response rather than silent failure.

Production secrets, Supabase project credentials, Telegram bot tokens, payment secrets and VPS private configuration are never committed to the public repository.

## 23. Open implementation choices (not architectural ambiguity)

The implementation plan may choose concrete libraries, project names, migration tooling, hosting layout and UI composition as long as the boundaries and behavioral rules in this document are preserved.

No unresolved product-policy decisions remain for Phase 1 planning.
