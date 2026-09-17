do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'vg_device') then
    create role vg_device nologin nobypassrls nocreatedb nocreaterole;
  end if;
end $$;

grant usage on schema licensing to vg_device;
grant select on licensing.accounts to vg_device;
create policy device_account_access on licensing.accounts
  for select to vg_device
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create table licensing.devices (
  device_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  name text not null check (char_length(name) between 1 and 80),
  platform text not null check (platform = 'windows'),
  public_key text not null,
  idempotency_key uuid not null,
  payload_hash text not null,
  lease_version integer not null default 1 check (lease_version > 0),
  created_at timestamptz not null default now(),
  revoked_at timestamptz,
  unique(account_id,idempotency_key)
);
create index devices_account_active_idx
  on licensing.devices(account_id,revoked_at,created_at);
alter table licensing.devices enable row level security;
alter table licensing.devices force row level security;
revoke all on licensing.devices from public;
grant select,insert,update on licensing.devices to vg_device;
create policy device_owner_access on licensing.devices
  to vg_device
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create table licensing.device_nonces (
  nonce_hash text primary key,
  account_id uuid not null references licensing.accounts(account_id),
  device_id uuid not null references licensing.devices(device_id),
  expires_at timestamptz not null,
  consumed_at timestamptz,
  created_at timestamptz not null default now()
);
alter table licensing.device_nonces enable row level security;
alter table licensing.device_nonces force row level security;
revoke all on licensing.device_nonces from public;
grant select,insert,update,delete on licensing.device_nonces to vg_device;
create policy nonce_owner_access on licensing.device_nonces
  to vg_device
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);
create table licensing.offline_leases (
  lease_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  device_id uuid not null references licensing.devices(device_id),
  key_id text not null,
  lease_version integer not null check (lease_version > 0),
  token_hash text not null,
  features_hash text not null,
  issued_at timestamptz not null,
  expires_at timestamptz not null
);
create index offline_leases_device_idx
  on licensing.offline_leases(device_id,expires_at);
alter table licensing.offline_leases enable row level security;
alter table licensing.offline_leases force row level security;
revoke all on licensing.offline_leases from public;
grant select,insert on licensing.offline_leases to vg_device;
create policy lease_owner_access on licensing.offline_leases
  to vg_device
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

revoke update,delete on licensing.offline_leases from vg_device;