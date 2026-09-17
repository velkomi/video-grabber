do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'vg_ledger') then
    create role vg_ledger nologin nobypassrls nocreatedb nocreaterole;
  end if;
end $$;

grant usage on schema licensing to vg_ledger;
grant select on licensing.accounts to vg_ledger;
grant select on licensing.entitlement_grants to vg_ledger;
grant update(available,reserved) on licensing.entitlement_grants to vg_ledger;

create policy ledger_account_access on licensing.accounts
  for select to vg_ledger using (true);
create policy ledger_grant_access on licensing.entitlement_grants
  to vg_ledger using (true) with check (true);

create table licensing.reservations (
  reservation_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  intent_id uuid not null,
  request_hash text not null,
  operation text not null,
  executor text not null,
  device_id uuid,
  grant_id uuid references licensing.entitlement_grants(grant_id),
  state text not null check (state in ('reserved','completed','released','review_required')),
  uses_credit boolean not null,
  expires_at timestamptz not null,
  attempt_id uuid,
  fence bigint,
  evidence_id text,
  created_at timestamptz not null default now(),
  finalized_at timestamptz,
  unique(account_id,intent_id)
);
create index reservations_account_state_idx
  on licensing.reservations(account_id,state,expires_at);

alter table licensing.reservations enable row level security;
alter table licensing.reservations force row level security;
revoke all on licensing.reservations from public;
grant select,insert,update on licensing.reservations to vg_ledger;
create policy ledger_reservation_access on licensing.reservations
  to vg_ledger using (true) with check (true);

create table licensing.credit_ledger (
  ledger_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  grant_id uuid references licensing.entitlement_grants(grant_id),
  reservation_id uuid references licensing.reservations(reservation_id),
  event_kind text not null check (event_kind in (
    'issue','reserve','commit','release','release_void','adjustment')),
  available_delta bigint not null,
  reserved_delta bigint not null,
  spent_delta bigint not null,
  void_delta bigint not null,
  actor_account_id uuid,
  evidence_id text,
  reason text not null,
  created_at timestamptz not null default now()
);

create unique index credit_ledger_reservation_event_uq
  on licensing.credit_ledger(reservation_id,event_kind)
  where reservation_id is not null;
create index credit_ledger_account_idx
  on licensing.credit_ledger(account_id,created_at,ledger_id);
alter table licensing.credit_ledger enable row level security;
alter table licensing.credit_ledger force row level security;
revoke all on licensing.credit_ledger from public;
grant select,insert on licensing.credit_ledger to vg_ledger;
grant select on licensing.credit_ledger to vg_admin;
create policy ledger_event_access on licensing.credit_ledger
  to vg_ledger using (true) with check (true);
create policy admin_ledger_read on licensing.credit_ledger
  for select to vg_admin using (true);

grant insert on licensing.credit_ledger to vg_admin;
create policy admin_ledger_insert on licensing.credit_ledger
  for insert to vg_admin with check (true);

revoke update,delete on licensing.credit_ledger from vg_api,vg_admin,vg_ledger;
