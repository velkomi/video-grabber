create table licensing.telegram_destination_challenges (
  proof_hash text primary key,
  account_id uuid not null references licensing.accounts(account_id),
  chat_id bigint not null,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  consumed_at timestamptz
);

create index telegram_destination_challenges_account_idx
  on licensing.telegram_destination_challenges(account_id, expires_at)
  where consumed_at is null;

create table licensing.delivery_destinations (
  destination_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  chat_id bigint not null,
  title text not null,
  kind text not null,
  telegram_user_id bigint not null,
  linked_at timestamptz not null,
  revoked_at timestamptz
);

create unique index delivery_destinations_active_chat_uq
  on licensing.delivery_destinations(account_id, chat_id)
  where revoked_at is null;

alter table licensing.telegram_destination_challenges enable row level security;
alter table licensing.telegram_destination_challenges force row level security;
alter table licensing.delivery_destinations enable row level security;
alter table licensing.delivery_destinations force row level security;

grant select, insert, update, delete on licensing.telegram_destination_challenges to vg_api;
grant select, insert, update on licensing.delivery_destinations to vg_api;
revoke all on licensing.telegram_destination_challenges, licensing.delivery_destinations from public;

create policy telegram_destination_challenge_owner on licensing.telegram_destination_challenges
  to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy delivery_destination_owner on licensing.delivery_destinations
  to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);