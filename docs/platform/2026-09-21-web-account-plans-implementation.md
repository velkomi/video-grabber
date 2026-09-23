# VideoGrabber Web + shared account plans — implementation note

**Date:** 2026-09-21  
**Status:** implementation branch `feature/web-account-plans-20260921`  
**Base:** `v0.1.10-preview.31` / `3e4796e`

This note supersedes the 2026-09-15 rule that a new guest has no download grant, and it supersedes the statement that a Telegram identity alone is sufficient for protected downloads.

## Product catalog

| Plan | Price | Individual video downloads | Full course |
| --- | ---: | --- | --- |
| Free | 0 RUB | 10 total for the lifetime of the account | no |
| Start | 1,500 RUB / 30 days | maximum 10 logical downloads per UTC calendar day | no |
| Unlimited Video | 2,500 RUB / 30 days | unlimited | no |
| Full Course | 5,000 RUB / 30 days | unlimited | yes |
| owner_admin | n/a | unlimited | yes |

A logical download is one requested media item. HLS fragments, transport retries, resume, worker retries and Telegram delivery retries do not spend a second logical allowance for the same idempotent intent.

Free is issued transactionally once when the account is first created. A unique partial index prevents a second starter grant. Existing accounts receive one migration backfill. Re-login and identity linking do not issue another starter grant.

Start uses a server-side `daily_plan_usage` bucket keyed by account, UTC date and plan. Reservation is atomic and constrained to `reserved + spent <= 10`. Release returns an unstarted slot; commit converts reserved to spent.

`course_download` is an explicit server-authorized capability. It is not represented by an artificial credit balance.

## Account origin and Telegram

The launch authentication now reuses the already-qualified MOST Control Supabase Auth project `wsbsaesgoliiojjjucfq`: Google OAuth and passwordless e-mail Magic Link are enabled there. VideoGrabber is an additional allowed redirect at `https://videograbber.srv1902378.hstgr.cloud/web/`; the MOST site URL and existing MOST callbacks remain unchanged. VideoGrabber stores its own product/account/entitlement state in its own PostgreSQL and uses Supabase only as the external identity authority.

The browser receives the Supabase session and immediately exchanges the Supabase access token with VideoGrabber. The VideoGrabber API validates that token server-side via Supabase `/auth/v1/user` and then issues its own short-lived access/rotating refresh session. Google and Magic Link for the same Supabase user ID coalesce into one VideoGrabber account; equal e-mail strings with different Supabase user IDs never merge.

Managed Windows uses the same web/Supabase login through a five-minute one-time desktop handoff. The local callback contains only a one-time VideoGrabber code, never the Supabase token. The internal refresh session remains protected by Windows DPAPI.

The account records `primary_auth_provider`, set only when the internal account is originally created. The public launch account flows are Google and verified e-mail. Existing Apple/Yandex accounts remain compatible.

A Telegram-origin account may exist so the bot can explain linking, but it is not allowed to create protected media jobs. Linking Google to that Telegram-origin account does not rewrite its origin. The intended flow is:

1. create/sign in to the main VideoGrabber account through Google or e-mail;
2. send `/link` to **@VideoGra_bot**;
3. open the five-minute one-time link and confirm the normal Google/e-mail account on Web;
4. the server atomically moves only the Telegram identity to the main account, revokes/voids the untouched temporary Free-10 allowance and blocks the orphan temporary account;
5. use the same account and entitlements in Web, Managed Windows and Telegram.

Automatic Telegram linking stops instead of guessing when the temporary Telegram account contains purchases, subscriptions, non-starter grants, active Windows devices, active delivery destinations or unfinished jobs. `/buy` is disabled on a temporary Telegram account until linking succeeds.

Administrative account merge discards the source account's Free starter instead of transferring it. This prevents two pre-existing Free accounts from becoming a 20-download account after merge.

The Telegram product bot is **@VideoGra_bot**. Its token is never committed or pasted into documentation/chat. Deployment reads the token, webhook secret, inbox encryption key and numeric bot ID from protected VPS files. The deployment helper configures webhook, command menu and the Mini App button only after the HTTPS API is live.

## Web surface

Platform.Api serves the public/app surface at `/web/`.

The page includes:
- landing/features/pricing;
- Supabase Google OAuth / passwordless e-mail Magic Link sign-in;
- account and plan state;
- Windows devices and online/offline state;
- URL, operation and quality controls;
- protected desktop queue creation;
- recent jobs;
- subscription catalog and YooKassa checkout when explicitly configured.

The visual layer is static HTML/CSS/JavaScript with progressive 3D film-strip effects. It has mobile and `prefers-reduced-motion` fallbacks and does not depend on third-party artwork.

## Web -> Desktop execution

Web does not download protected media into permanent VPS storage.

For a desktop job:
1. the API stores a short-lived encrypted source envelope scoped to the account;
2. Web creates a job assigned to a registered Windows device;
3. Managed VideoGrabber proves device ownership while polling;
4. the worker resolves only its own source;
5. the existing local downloader writes directly into the remembered local VideoGrabber folder;
6. verified local success is completed with a capability-bound local evidence value;
7. the logical reservation is committed without uploading the finished video/course to the VPS.

A full course is always `desktop_worker`. The Windows course pipeline uses the local authenticated WebView2/GetCourse session. Cookies and site passwords are not sent to Telegram or persisted in the licensing database.

`devices.last_seen_at` is updated only after a successful signed device proof. Web and Telegram use it for online/offline presentation.

## Server artifact retention

Server-worker artifacts still exist for operations that require Telegram delivery or server processing. They remain temporary and are governed by the existing retention/cleanup pipeline. Local-only Web/Desktop output is outside VPS retention and remains under the user's control.

## Payments

The shared payment catalog now carries optional `planId`, and subscriptions preserve it into renewal grants.

The committed staging example contains RUB YooKassa prices for Start, Unlimited Video and Full Course. No RUB-to-Stars exchange rate is invented. Telegram Stars products must be explicitly configured and qualified separately.

Live money movement remains disabled unless protected provider credentials/configuration are explicitly supplied and deployment gates pass.

## Compatibility

- Previously released Local Windows builds remain local/unlocked.
- Mandatory account rules apply to the Managed platform path.
- Legacy entitlement grants with no `plan_id` retain their prior behavior and do not silently gain `course_download`.
- owner_admin remains unlimited.
- Existing Apple/Yandex identities are not deleted; the launch UI prioritizes Google/e-mail.

## Acceptance tests added

The integration suite covers:
- one Free starter per account and no re-grant on re-login;
- idempotent logical Free reservation/release;
- Start 20-way concurrency with exactly 10 successful reservations;
- Start UTC-day reset;
- Unlimited Video individual-download access and course denial;
- Full Course course admission;
- owner_admin course admission without Free depletion;
- Telegram-origin protected-job denial;
- Google/e-mail protected-job admission;
- account merge discarding the duplicate source Free starter;
- one-time Windows Web-auth handoff and replay denial;
- one-time Telegram self-link, replay denial and reconciliation-required safety stop;
- deployment assets: explicit RLS login roles, protected secrets, pinned yt-dlp and pinned Squid egress boundary.

Final local acceptance for this branch:
- Platform.Tests: **322 passed, 0 failed** (split into exhaustive class groups to stay below the control-channel timeout);
- Infrastructure.Tests: **677 passed, 14 pre-existing real-media integration skips, 0 failed**;
- Core.Tests: **32 passed, 0 failed**;
- Platform.Worker.Tests: **31 passed, 0 failed**;
- Managed Windows build: **0 warnings, 0 errors**;
- Web JavaScript / Python helper / YAML / `git diff --check`: pass;
- VPS Squid validation: public HTTPS **200**, loopback target **403**.

Live YooKassa/Stars charging remains intentionally disabled until provider credentials/catalog qualification are explicitly completed.
