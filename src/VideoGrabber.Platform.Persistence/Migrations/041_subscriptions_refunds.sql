create table licensing.subscriptions (
  subscription_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  origin_payment_id uuid not null references licensing.payments(payment_id),
  provider text not null check (provider in ('stars','yookassa')),
  environment text not null check (environment in ('test','stage','live')),
  provider_reference text not null,
  state text not null check (state in ('active','cancel_pending','canceled','review_required')),
  auto_renew boolean not null,
  paid_through timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(provider,environment,provider_reference),
  unique(origin_payment_id)
);

create table licensing.subscription_events (
  event_id uuid primary key,
  subscription_id uuid not null references licensing.subscriptions(subscription_id),
  event_key text not null,
  event_kind text not null check (event_kind in (
    'initial','renewal','cancel_requested','cancel_confirmed','refund','chargeback','review')),
  provider text not null,
  environment text not null,
  charge_id text not null,
  period_start timestamptz,
  period_end timestamptz,
  amount_minor bigint not null,
  currency text not null,
  occurred_at timestamptz not null,
  created_at timestamptz not null,
  unique(subscription_id,event_key)
);

create unique index subscription_period_once
  on licensing.subscription_events(subscription_id,period_start)
  where event_kind='renewal' and period_start is not null;

create unique index subscription_provider_charge_once
  on licensing.subscription_events(provider,environment,charge_id,event_kind);

create unique index subscription_grant_once
  on licensing.entitlement_grants(source_reference)
  where source='purchase' and source_reference like 'subscription:%';

revoke all on licensing.subscriptions,licensing.subscription_events from public;
grant select,insert,update on licensing.subscriptions to vg_ledger;
grant select,insert on licensing.subscription_events to vg_ledger;
grant select on licensing.subscriptions to vg_api;

alter table licensing.subscriptions enable row level security;
alter table licensing.subscriptions force row level security;
alter table licensing.subscription_events enable row level security;
alter table licensing.subscription_events force row level security;

create policy ledger_subscriptions_access on licensing.subscriptions
  to vg_ledger using (true) with check (true);
create policy ledger_subscription_events_access on licensing.subscription_events
  to vg_ledger using (true) with check (true);
create policy api_subscription_owner on licensing.subscriptions
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);