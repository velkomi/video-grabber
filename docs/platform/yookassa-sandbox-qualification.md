# YooKassa sandbox qualification

## Local qualification

Status: **PASS — emulator only**.

The local adapter is pinned to the YooKassa v3 HTTPS API origin and uses server-side Basic authentication. The desktop application never receives the shop secret. `POST /payments` is created from the server catalog with `capture=true`, an exact two-decimal RUB value, a UUID `Idempotence-Key`, an allowlisted HTTPS return URL, and an opaque `metadata.order_id`.

Incoming notifications are treated only as a hint containing an object ID. VideoGrabber composes `GET /v3/payments/{validated-id}` and trusts only that authenticated provider read for state, amount, currency, `test`, metadata/order ownership and saved payment-method data. A notification body claiming `succeeded` cannot grant access while the provider read is still `pending`.

Local emulator tests cover exact kopeck parsing, duplicate create idempotence, provider 429/503 behavior, unknown object IDs, amount/environment mismatch, forged browser return query, verified success replay and refund construction. Emulator evidence records only whether Basic auth existed; credentials are never written to evidence.

## Real sandbox gate

Status: **BLOCKED until an authorized YooKassa test shop credential is injected**.

A real sandbox run requires:
- an approved YooKassa test shop ID and test secret supplied as protected runtime secrets;
- an approved HTTPS return URL and webhook URL;
- explicit authorization to create/refund test payments;
- evidence that returned objects have `test=true`, the expected amount/currency and the same idempotency key/order metadata.

The repository must not contain shop credentials. Without those secrets, no real external payment or refund is attempted and this gate must not be marked PASS.

## Live gate

Status: **BLOCKED**.

Live selling additionally requires a separately versioned approved live product catalog, merchant/legal/tax/receipt configuration, support policy and explicit production authorization. The synthetic test catalog is not a live tariff.
