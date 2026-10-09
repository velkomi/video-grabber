# Support rollout and operation

Guest support is available through `/v1/support/*`, the shared website/Mini App dialog, Windows Information, and bot `/support`. The form has no attachments. Validated requests are encrypted and stored once with independent email/Telegram outbox rows.

## Configuration

Set `VG_SUPPORT_ENABLED=true`, `VG_SUPPORT_NOTIFICATIONS_ENABLED=true` only after fixed owner destination verification. `VG_SUPPORT_TELEGRAM_OWNER_ID` must come from the linked owner identity and Bot API `getChat` returning the expected private owner chat. Never accept recipient IDs from the request.

Email needs an existing service sender: `VG_SUPPORT_SMTP_HOST`, `VG_SUPPORT_SMTP_PORT=587` (STARTTLS) or465 (TLS), `VG_SUPPORT_SMTP_USERNAME`, `VG_SUPPORT_SMTP_PASSWORD`, `VG_SUPPORT_SMTP_FROM`. An administrator can specify another valid TCP port (1..65535); it still requires STARTTLS with normal certificate validation. Owner defaults to `velkoshkin@gmail.com`; `VG_SUPPORT_EMAIL_OWNER` can explicitly override it. Keep credentials only in protected deployment configuration, never Git/chat. A personal Gmail password is not an SMTP app password. If an app password is needed, the owner creates it in Google Account settings and enters it into a protected local/server file; browser credential creation requires human handoff.

No SMTP sender was found during the read-only inventory. Telegram can operate independently while email stays pending. This is not evidence of email delivery; the UI promises only acceptance and a ticket number. Connecting SMTP later releases the bounded existing outbox. Check backlog before enabling a new channel so old requests are not unexpectedly dispatched.

## Limits and failure states

Tickets: per authenticated account or guest contact+trusted IP,1/minute,3/15minutes,10/day; IP30/hour, pending queue1000. Challenge/invalid-submit limits use a signed one-day guest browser identity; additional IP ceilings are60 challenges/minute and120 submits/minute. Clearing a cookie cannot bypass database ticket quotas. Browser proof-of-work challenges expire in5minutes and are consumed atomically. Bot input has30/minute per verified sender. Dispatch reservations persist:60/hour,200/day per channel; at most3 attempts for confirmed transient failures respecting Telegram retry_after. Unknown remote acceptance or interrupted lease becomes `review_required`, with no blind resend. Manual review must check actual receipt first.

Requests contain no secrets by design; full text/contact is never written to application logs. No automatic deletion has been authorized. Stored quotas/leases survive restart. Monitor pending/failed/review_required counts with restricted owner DB access; do not publish ticket contents or create a public enumeration API.

## Proxy/auth rollout

`VG_TRUSTED_PROXY_IPS` lists exact observed trusted proxy IPs separated by semicolons, at most two forwarded hops. Do not trust arbitrary X-Forwarded-For or whole public networks. Apply current UTC `VG_TELEGRAM_ASSERTION_NOT_BEFORE_UTC` at migration of replay identity; this invalidates the previous five-minute assertion window without deleting old replay rows. Users may need to reopen Mini App once.

Browser CSP starts Report-Only. `/v1/security/csp-report` discards body after a16KiB bound, retaining only a generic counter event. Caller10/minute and global4 concurrent reports bound abuse. HSTS is one day on validated HTTPS only, without preload/includeSubDomains. These controls do not constitute exhaustive protection from every attack or provider DDoS mitigation.

## Acceptance and rollback

Back up PostgreSQL and verify archive readability before additive migrations067–069. Deploy API only; retain the previous image/configuration and preserve neighboring containers. Check health, migration revision, client-only media policy, actual public form/Telegram command menu, and a single labelled owner notification. A Bot API200 is insufficient: verify the received message.

If readiness fails, restore previous API image and protected configuration; do not drop new tables or restore over customer data automatically. Native managed build/test evidence is separate from actual installed GUI acceptance and full installer redistribution licensing gates.
