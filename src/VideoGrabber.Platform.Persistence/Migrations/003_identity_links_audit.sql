alter table licensing.accounts
  add column if not exists merged_into uuid references licensing.accounts(account_id);

create table licensing.account_links (
  challenge_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  created_at timestamptz not null,
  expires_at timestamptz not null,
  consumed_at timestamptz,
  purpose text not null default 'link' check (purpose in ('link','recovery')),
  created_by_admin uuid references licensing.accounts(account_id),
  proof_source text,
  reason text
);

create index account_links_account_idx
  on licensing.account_links(account_id, expires_at);

create table licensing.audit_events (
  event_id uuid primary key,
  account_id uuid references licensing.accounts(account_id),
  actor_account_id uuid references licensing.accounts(account_id),
  event_type text not null,
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);
alter table licensing.account_links enable row level security;
alter table licensing.account_links force row level security;
alter table licensing.audit_events enable row level security;
alter table licensing.audit_events force row level security;

grant select, insert, update on licensing.account_links to vg_api;
grant select, insert on licensing.audit_events to vg_api;
grant select, insert, update, delete on licensing.account_links, licensing.audit_events to vg_admin;

create policy account_link_owner on licensing.account_links
  to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy audit_owner on licensing.audit_events
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy audit_insert_owner on licensing.audit_events
  for insert to vg_api
  with check (actor_account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy admin_account_links on licensing.account_links
  to vg_admin using (true) with check (true);
create policy admin_audit_events on licensing.audit_events
  to vg_admin using (true) with check (true);

grant delete on licensing.identities to vg_identity;
create policy identity_mapping_delete on licensing.identities
  for delete to vg_identity
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);
