create table licensing.telegram_updates (
  update_id bigint primary key,
  payload_cipher bytea not null,
  payload_nonce bytea not null,
  payload_tag bytea not null,
  received_at timestamptz not null,
  state text not null check (state in ('pending','processing','handled')),
  handled_at timestamptz
);

create index telegram_updates_pending_idx
  on licensing.telegram_updates(received_at, update_id)
  where state='pending';

create index telegram_updates_handled_idx
  on licensing.telegram_updates(handled_at)
  where state='handled';

create table licensing.telegram_assertions (
  assertion_hash text primary key,
  user_id bigint not null,
  account_id uuid not null references licensing.accounts(account_id),
  used_at timestamptz not null,
  expires_at timestamptz not null
);

create index telegram_assertions_expiry_idx
  on licensing.telegram_assertions(expires_at);

grant select, insert, update, delete on licensing.telegram_updates to vg_api;
grant select, insert, delete on licensing.telegram_assertions to vg_api;
revoke all on licensing.telegram_updates, licensing.telegram_assertions from public;