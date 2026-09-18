alter table licensing.entitlement_grants
  add column if not exists source_reference text;

create unique index if not exists purchase_grant_once
  on licensing.entitlement_grants(source_reference)
  where source='purchase' and source_reference is not null;

grant insert on licensing.entitlement_grants to vg_ledger;
grant update(available,reserved,revoked_at) on licensing.entitlement_grants to vg_ledger;
grant update(first_purchase_at,base_role) on licensing.accounts to vg_ledger;
create policy ledger_account_update on licensing.accounts
  for update to vg_ledger using (true) with check (true);

create table licensing.payments (
  payment_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  provider text not null check (provider in ('stars','yookassa')),
  environment text not null check (environment in ('test','stage','live')),
  sku text not null,
  catalog_version text not null,
  expected_minor bigint not null check (expected_minor > 0),
  expected_currency text not null,
  recurring boolean not null,
  idempotency_key uuid not null,
  request_hash text not null,
  invoice_payload text,
  provider_payment_id text,
  state text not null check (state in (
    'pending','succeeded','refund_pending','refunded','canceled','reconcile_required')),
  subscription_id text,
  paid_through timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(account_id,provider,idempotency_key)
);

create unique index payment_provider_charge
  on licensing.payments(provider,environment,provider_payment_id)
  where provider_payment_id is not null;
create unique index payment_invoice_payload_uq
  on licensing.payments(invoice_payload)
  where invoice_payload is not null;
create index payments_account_idx
  on licensing.payments(account_id,created_at,payment_id);

create table licensing.payment_events (
  event_id uuid primary key,
  payment_id uuid not null references licensing.payments(payment_id),
  account_id uuid not null references licensing.accounts(account_id),
  event_key text not null,
  provider text not null,
  environment text not null,
  provider_payment_id text not null,
  status text not null,
  amount_minor bigint not null,
  currency text not null,
  occurred_at timestamptz not null,
  created_at timestamptz not null,
  unique(payment_id,event_key)
);
create index payment_events_payment_idx
  on licensing.payment_events(payment_id,occurred_at,event_id);

alter table licensing.payments enable row level security;
alter table licensing.payments force row level security;
alter table licensing.payment_events enable row level security;
alter table licensing.payment_events force row level security;
revoke all on licensing.payments,licensing.payment_events from public;
grant select,insert,update on licensing.payments to vg_ledger;
grant select,insert on licensing.payment_events to vg_ledger;
grant select on licensing.payments to vg_api;
create policy ledger_payments_access on licensing.payments
  to vg_ledger using (true) with check (true);
create policy ledger_payment_events_access on licensing.payment_events
  to vg_ledger using (true) with check (true);
create policy api_payment_owner on licensing.payments
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);