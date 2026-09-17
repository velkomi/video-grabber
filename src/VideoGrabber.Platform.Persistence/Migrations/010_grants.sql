create table licensing.entitlement_grants (
  grant_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  kind text not null check (kind in ('permanent','time','credits','hybrid')),
  source text not null check (source in ('admin_gift','purchase','adjustment')),
  valid_from timestamptz not null,
  valid_until timestamptz,
  available bigint not null check (available >= 0),
  reserved bigint not null default 0 check (reserved >= 0),
  original_amount bigint not null check (original_amount >= 0),
  revoked_at timestamptz,
  admin_id uuid references licensing.accounts(account_id),
  idempotency_key uuid,
  payload_hash text,
  reason text not null,
  created_at timestamptz not null default now(),
  check (valid_until is null or valid_until > valid_from),
  unique(admin_id,idempotency_key)
);

create index entitlement_grants_account_idx
  on licensing.entitlement_grants(account_id,valid_until,created_at);

alter table licensing.entitlement_grants enable row level security;
alter table licensing.entitlement_grants force row level security;
revoke all on licensing.entitlement_grants from public;
grant select on licensing.entitlement_grants to vg_api;
grant select,insert,update on licensing.entitlement_grants to vg_admin;

create policy entitlement_grant_owner on licensing.entitlement_grants
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy admin_entitlement_grants on licensing.entitlement_grants
  to vg_admin using (true) with check (true);
