do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'vg_migrator') then
    create role vg_migrator nologin nobypassrls;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'vg_api') then
    create role vg_api nologin nobypassrls nocreatedb nocreaterole;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'vg_identity') then
    create role vg_identity nologin nobypassrls nocreatedb nocreaterole;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'vg_admin') then
    create role vg_admin nologin nobypassrls nocreatedb nocreaterole;
  end if;
end $$;

create schema if not exists licensing authorization vg_migrator;
revoke all on schema licensing from public;
grant usage on schema licensing to vg_api, vg_identity, vg_admin;

create table licensing.accounts (
  account_id uuid primary key,
  base_role text not null check (base_role in ('guest','user','owner_admin')),
  blocked_at timestamptz,
  first_purchase_at timestamptz,
  created_at timestamptz not null default now()
);

create table licensing.identities (
  identity_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  issuer text not null,
  provider text not null,
  provider_subject text not null,
  verified_email text,
  linked_at timestamptz not null default now(),
  unique(issuer, provider, provider_subject)
);

alter table licensing.accounts enable row level security;
alter table licensing.accounts force row level security;
alter table licensing.identities enable row level security;
alter table licensing.identities force row level security;

grant select on licensing.accounts, licensing.identities to vg_api;
grant select, insert on licensing.accounts, licensing.identities to vg_identity;
grant select, insert, update, delete on licensing.accounts, licensing.identities to vg_admin;

create policy account_read on licensing.accounts
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy identity_owner on licensing.identities
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy identity_account_select on licensing.accounts
  for select to vg_identity using (true);
create policy identity_account_insert on licensing.accounts
  for insert to vg_identity with check (true);
create policy identity_mapping_select on licensing.identities
  for select to vg_identity using (true);
create policy identity_mapping_insert on licensing.identities
  for insert to vg_identity with check (true);

create policy admin_accounts on licensing.accounts
  to vg_admin using (true) with check (true);
create policy admin_identities on licensing.identities
  to vg_admin using (true) with check (true);

revoke create on schema licensing from vg_api, vg_identity, vg_admin;
revoke update, delete on licensing.identities from vg_identity;
revoke update, delete on licensing.accounts from vg_identity;
