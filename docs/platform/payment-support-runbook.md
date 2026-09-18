# VideoGrabber payment and subscription support runbook

## Source of truth

Client-side invoice status, browser return parameters and webhook bodies do not grant access. A grant or subscription period is projected only from a verified Telegram Stars payment/update or an authenticated YooKassa provider read. Payment and subscription events are append-only; the current projection can be rebuilt from verified history.

## Telegram Stars

Telegram digital-service purchase UI offers Stars only. Pre-checkout validates the opaque invoice handle, payer, XTR amount and catalog but creates no grant. The first recurring payment creates the subscription agreement and preserves the provider first-charge reference used by `editUserStarSubscription`. Each later recurring charge becomes its own renewal event and time grant. Subscription period is 30 days / 2592000 seconds; this must be re-checked against the current Bot API before a live launch.

Cancellation first persists `auto_renew=false` / `cancel_pending`, then calls the provider. The already paid period is never shortened. A genuine renewal that raced with cancellation is still honored as paid value, while auto-renew remains off.

## YooKassa recurring

The first authorized recurring checkout requests `save_payment_method=true`. The saved `payment_method_id` is stored only server-side as the subscription provider reference. A future renewal uses the same payment method and a deterministic idempotency UUID derived from `(subscription_id, period_start)`; an unknown provider outcome is retried with that same key, never a new one.

A renewal notification is not trusted directly. The server fetches the provider payment, validates `test`/environment, subscription metadata, exact RUB amount and period, then appends a unique renewal event and time grant. Canceling a YooKassa subscription disables the VideoGrabber scheduler; the already paid period remains valid.

## Refunds and chargebacks

Refund intent is written before the provider call. A lost provider ACK remains pending until reconciliation. A verified refund revokes only the unexpired availability represented by its purchase/renewal grant; historical spend remains in the ledger. Replaying an earlier success cannot recreate a revoked renewal grant because the renewal event/period keys are immutable.

A machine-verifiable chargeback can revoke the affected renewal and move the subscription to `review_required` with auto-renew off. If a provider has no supported machine-verifiable chargeback event/read, create an admin review record instead of inventing a webhook schema.

## Support triage

1. Identify the account and payment/subscription ID from the authenticated account view or private `/paysupport`.
2. Read the payment/provider state and immutable events; never ask the user to paste payment secrets or bot tokens.
3. For pending/unknown provider outcomes, reconcile using the original provider object/charge and original idempotency key.
4. For `refund_pending`, do not issue a second refund with a different key.
5. For canceled subscriptions, verify `auto_renew=false` and `paid_through`; access until that timestamp is expected.
6. Never manually alter spend history to “undo” a payment. Use a verified adjustment/refund event.

## External gates

Local emulator qualification is PASS only when the test suites are green. Real Stars paid invoices, real YooKassa sandbox charges/refunds and all live money movement remain **BLOCKED** until explicit authorization and protected credentials are supplied. Synthetic test catalog prices are not live tariffs.
