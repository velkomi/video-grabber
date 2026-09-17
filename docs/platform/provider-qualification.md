# Provider qualification

Task P1/T2 implements the local provider protocol and deterministic broker emulator for Google, Apple, Yandex, Telegram and verified email. Local emulator evidence is not a substitute for live provider qualification.

## Local acceptance

- RS256 signature, fixed issuer, audience, expiry and nonce are validated.
- Broker user-info must contain exactly one identity for the configured provider partition.
- `provider + broker_subject -> provider_subject` is frozen in PostgreSQL; a changed mapping fails closed.
- Verified-email equality never merges accounts across provider partitions.
- OAuth provider redirect uses the configured server callback, not a client-supplied callback.
- Auth flow is one-use with five-minute lifetime, fixed-time state comparison and PKCE S256.
- API access token lifetime is five minutes; refresh secrets rotate and replay revokes the family.
- OAuth codes, bearer tokens, cookies and signed client return URLs are not written to captured application logs.

## Live qualification

Each provider is independently BLOCKED until an explicitly authorized test application/environment is supplied. For each provider record: exact issuer/discovery URL, audience/client ID hash, registered callback, positive login, bad issuer/audience/expiry/nonce/state/PKCE, replay, same-email collision, distinct-subject collision and provider-user-info identity count.

A failing live collision test disables that provider. Do not infer safety from emulator PASS, and do not commit client secrets, Supabase service-role keys, OAuth codes, refresh tokens or raw Telegram assertions.
