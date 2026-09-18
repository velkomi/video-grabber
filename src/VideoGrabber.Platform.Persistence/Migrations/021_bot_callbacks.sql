create table licensing.bot_callbacks (
  token_hash text primary key,
  account_id uuid not null references licensing.accounts(account_id),
  action text not null,
  resource_id uuid,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  consumed_at timestamptz
);

create index bot_callbacks_account_idx
  on licensing.bot_callbacks(account_id, expires_at)
  where consumed_at is null;

grant select, insert, update, delete on licensing.bot_callbacks to vg_api;
revoke all on licensing.bot_callbacks from public;