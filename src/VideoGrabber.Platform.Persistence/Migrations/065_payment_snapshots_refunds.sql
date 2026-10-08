alter table licensing.payments add column product_snapshot jsonb;

create table licensing.verified_refunds (
  provider text not null check(provider in ('stars','yookassa')),
  environment text not null check(environment in ('test','stage','live')),
  refund_id text not null,
  payment_id uuid not null references licensing.payments(payment_id),
  account_id uuid not null references licensing.accounts(account_id),
  provider_payment_id text not null,
  amount_minor bigint not null check(amount_minor > 0),
  currency text not null check(currency in ('RUB','XTR')),
  occurred_at timestamptz not null,
  created_at timestamptz not null default now(),
  primary key(provider,environment,refund_id)
);
create index verified_refunds_payment on licensing.verified_refunds(payment_id);
alter table licensing.verified_refunds enable row level security;
alter table licensing.verified_refunds force row level security;
revoke all on licensing.verified_refunds from public;
grant select,insert on licensing.verified_refunds to vg_ledger;
grant select on licensing.verified_refunds to vg_admin;
create policy ledger_verified_refunds on licensing.verified_refunds to vg_ledger using(true) with check(true);
create policy admin_verified_refunds on licensing.verified_refunds for select to vg_admin using(true);
