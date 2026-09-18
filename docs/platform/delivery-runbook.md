# VideoGrabber delivery runbook

## State model

`pending -> sending -> delivered` is the successful path. A failure proven to happen before the Telegram upload starts becomes `failed_before_send` and may be retried automatically. Any transport exception after `sendDocument` starts becomes `delivery_unknown`; the platform must not send again until the account explicitly acknowledges that a duplicate Telegram message may be created.

Media accounting is attached to the media job/reservation, not delivery attempts. Replaying an idempotent delivery request or manually retrying an unknown ACK never creates another download-credit debit.

## Authorization

Every request is scoped by authenticated `account_id`. The referenced job must be completed, the artifact must be that job's owned artifact and not retention-expired, and the destination must belong to the same account. Immediately before send, current Telegram user/bot permissions are re-read through `DestinationService`. A blocked account or lost destination permission fails before send.

## File transport

The API opens only the server-owned `artifacts.storage_path`. User paths, Telegram cookies, browser cookies, proxy credentials and shell text are never accepted by the delivery endpoint. The configured `VG_TELEGRAM_DOCUMENT_MAX_BYTES` is authoritative for this deployment. Files above it return `transport_limit_exceeded_choose_split_or_desktop`; there is no silent transcode or split. If lossless split is implemented/selected later, each part must have recorded order/SHA and reconstruction must match the original SHA.

## Uncertain ACK recovery

`delivery_unknown` means Telegram may already have the document. The UI must show this warning. Manual resend requires the exact acknowledgement `I understand this may send a duplicate`. The same media artifact/job is reused and no extra media credit is consumed.

## Retention

Retention is disabled unless `VG_RETENTION_ENABLED=true` and `VG_RETENTION_ROOT` names the exact server artifact root. The API first tombstones an expired artifact (`expired_at`, `unavailable_reason`) only when there is no active `pending`, `sending` or `delivery_unknown` attempt. The worker then deletes only that fully-qualified owned path, refuses reparse/symlink/path escape, and ACKs cleanup. Desktop/user output directories are never enumerated.

## Transport qualification

Use `scripts/platform/Test-TelegramTransport.ps1` in `Emulator` mode for local configuration evidence. `Live` mode is blocked unless `VG_TELEGRAM_LIVE_TRANSPORT_AUTHORIZED=YES` plus an approved test chat/token are supplied. Missing budget/token/authorization remains BLOCKED, never PASS.