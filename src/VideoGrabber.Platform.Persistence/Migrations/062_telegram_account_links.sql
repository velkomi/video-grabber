create table licensing.telegram_account_links (
  link_id uuid primary key,
  token_hash text not null unique,
  source_account_id uuid not null references licensing.accounts(account_id),
  telegram_user_id bigint not null,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  consumed_at timestamptz,
  target_account_id uuid references licensing.accounts(account_id),
  check (telegram_user_id > 0),
  check (expires_at > created_at),
  check ((consumed_at is null and target_account_id is null)
      or (consumed_at is not null and target_account_id is not null))
);

create index telegram_account_links_source_pending_idx
  on licensing.telegram_account_links(source_account_id,expires_at)
  where consumed_at is null;

alter table licensing.telegram_account_links enable row level security;
alter table licensing.telegram_account_links force row level security;
revoke all on licensing.telegram_account_links from public;

grant select,insert,update,delete
  on licensing.telegram_account_links to vg_admin;

create policy admin_telegram_account_links
  on licensing.telegram_account_links
  to vg_admin
  using (true)
  with check (true);

-- Self-service Telegram account linking is executed only by the narrowly
-- scoped admin connection so it can atomically inspect/move cross-account
-- state. Runtime API users still cannot enumerate these tables.
grant select on licensing.payments to vg_admin;
create policy admin_payments_read
  on licensing.payments
  for select to vg_admin
  using (true);

grant select on licensing.subscriptions to vg_admin;
create policy admin_subscriptions_read
  on licensing.subscriptions
  for select to vg_admin
  using (true);

grant select on licensing.delivery_destinations to vg_admin;
create policy admin_delivery_destinations_read
  on licensing.delivery_destinations
  for select to vg_admin
  using (true);

grant select on licensing.jobs to vg_admin;
create policy admin_jobs_read
  on licensing.jobs
  for select to vg_admin
  using (true);
