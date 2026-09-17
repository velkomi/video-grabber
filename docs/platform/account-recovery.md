# Account recovery and identity linking

Account identity is keyed only by `(issuer, provider, provider_subject)`. Matching email text never merges accounts and never grants recovery.

## Linking

- Linking requires a currently authenticated account session no older than five minutes.
- The server creates an owner-scoped, one-use, five-minute `challenge_id`.
- The new provider proof is signature/issuer/audience/nonce validated and bound to that challenge.
- A challenge owned by another account is returned as not found.
- An identity already owned by another account is a conflict; it is never auto-moved.
- The final identity cannot be unlinked. Concurrent unlink attempts are serialized so at least one sign-in identity remains.

## No-login recovery

- Recovery begins only after explicit owner-admin review with fresh MFA in the admin layer.
- The review records the proof source and reason, revokes existing API sessions, and creates a one-use five-minute recovery challenge.
- Anonymous clients receive no endpoint for looking up an account by email. They may only complete a previously issued opaque challenge.
- Verified-email recovery succeeds only when that exact email-provider identity is already linked to the reviewed account. Similar or matching email text on another provider is insufficient.
- Non-email recovery may add a freshly proven provider identity only after the admin-reviewed challenge.
- Review and completion are both audit events.

## Merge

Account merge is an internal admin operation until the admin API has server-verified MFA. It requires proof of both source and target identities. Accounts with purchase history are blocked pending financial reconciliation. Successful merge moves identities atomically, tombstones the source with `merged_into`, revokes source sessions, and writes an audit event.