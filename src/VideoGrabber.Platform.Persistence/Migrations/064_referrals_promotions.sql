create table licensing.referral_codes (
  account_id uuid primary key references licensing.accounts(account_id),
  code text not null unique check (code ~ '^[A-Z0-9]{12}$'),
  created_at timestamptz not null
);
create table licensing.referrals (
  referee_id uuid primary key references licensing.accounts(account_id),
  referrer_id uuid not null references licensing.accounts(account_id),
  created_at timestamptz not null,
  qualified_payment_id uuid unique references licensing.payments(payment_id) deferrable initially deferred,
  check (referee_id <> referrer_id)
);
create index referrals_referrer_idx on licensing.referrals(referrer_id);
create table licensing.promo_codes (
  code text primary key check (code ~ '^[A-Z0-9_-]{3,32}$'),
  discount_bps integer not null check (discount_bps between 1 and 2000),
  sku text,
  currency text not null check (currency in ('RUB','XTR')),
  starts_at timestamptz not null,
  ends_at timestamptz not null,
  max_uses integer not null check (max_uses > 0),
  per_account_limit integer not null check (per_account_limit > 0),
  budget_minor bigint not null check (budget_minor > 0),
  first_purchase_only boolean not null,
  active boolean not null,
  used integer not null default 0 check (used >= 0),
  reserved integer not null default 0 check (reserved >= 0),
  spent_minor bigint not null default 0 check (spent_minor >= 0),
  reserved_minor bigint not null default 0 check (reserved_minor >= 0),
  actor_id uuid not null references licensing.accounts(account_id),
  check (ends_at > starts_at),
  check (used + reserved <= max_uses),
  check (spent_minor <= budget_minor - reserved_minor)
);
create table licensing.promotion_quotes (
  quote_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  sku text not null,
  provider text not null check (provider in ('stars','yookassa')),
  catalog_version text not null,
  product_snapshot text not null,
  currency text not null check (currency in ('RUB','XTR')),
  original_minor bigint not null check (original_minor > 0),
  discount_minor bigint not null check (discount_minor >= 0),
  bonus_minor bigint not null check (bonus_minor >= 0),
  payable_minor bigint not null check (payable_minor > 0),
  promo_code text references licensing.promo_codes(code),
  referrer_id uuid references licensing.accounts(account_id),
  referral_applied boolean not null,
  recurring boolean not null,
  use_bonuses boolean not null,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  payment_id uuid unique references licensing.payments(payment_id) deferrable initially deferred,
  check (original_minor = discount_minor + bonus_minor + payable_minor),
  check (expires_at > created_at)
);
create table licensing.promotion_payments (
  payment_id uuid primary key references licensing.payments(payment_id) deferrable initially deferred,
  account_id uuid not null references licensing.accounts(account_id),
  referrer_id uuid references licensing.accounts(account_id),
  quote_id uuid unique references licensing.promotion_quotes(quote_id),
  promo_code text references licensing.promo_codes(code),
  currency text not null check (currency in ('RUB','XTR')),
  original_minor bigint not null check (original_minor > 0),
  discount_minor bigint not null check (discount_minor >= 0),
  bonus_minor bigint not null check (bonus_minor >= 0),
  payable_minor bigint not null check (payable_minor > 0),
  referral_applied boolean not null,
  recurring boolean not null,
  qualified boolean not null,
  reward_bps integer not null default 1000 check (reward_bps between 1 and 2000),
  hold_days integer not null default 14 check (hold_days between 0 and 365),
  lifetime_days integer not null default 365 check (lifetime_days between 1 and 3650),
  state text not null check (state in ('reserved','succeeded','canceled','refunded')),
  reward_minor bigint not null default 0 check (reward_minor >= 0),
  refunded_minor bigint not null default 0 check (refunded_minor >= 0),
  bonus_restored boolean not null default false,
  created_at timestamptz not null,
  check (original_minor = discount_minor + bonus_minor + payable_minor),
  check (refunded_minor <= payable_minor)
);
create index promotion_payments_promo_account_idx on licensing.promotion_payments(promo_code,account_id,state);
create index promotion_payments_account_reserved_idx on licensing.promotion_payments(account_id) where state='reserved';
create index payments_pending_promotions_idx on licensing.payments(account_id) where state in ('pending','reconcile_required');
create table licensing.bonus_lots (
  lot_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  currency text not null check (currency in ('RUB','XTR')),
  source_payment_id uuid references licensing.payments(payment_id) deferrable initially deferred,
  kind text not null check (kind in ('reward','restore')),
  granted_minor bigint not null check (granted_minor > 0),
  remaining_minor bigint not null,
  reserved_minor bigint not null default 0 check (reserved_minor >= 0),
  valid_from timestamptz not null,
  expires_at timestamptz not null,
  created_at timestamptz not null,
  unique(source_payment_id,kind),
  check (expires_at > valid_from)
);
create index bonus_lots_account_idx on licensing.bonus_lots(account_id,currency,valid_from,expires_at);
create table licensing.bonus_events (
  event_id uuid primary key,
  lot_id uuid not null references licensing.bonus_lots(lot_id),
  account_id uuid not null references licensing.accounts(account_id),
  payment_id uuid references licensing.payments(payment_id) deferrable initially deferred,
  event_key text not null unique,
  kind text not null check (kind in ('reward','reserve','spend','release','clawback','restore','offset_debit','offset_credit')),
  amount_minor bigint not null,
  remaining_delta bigint not null,
  reserved_delta bigint not null,
  created_at timestamptz not null
);
create index bonus_events_account_idx on licensing.bonus_events(account_id,created_at,event_id);
create table licensing.bonus_allocations (
  payment_id uuid not null references licensing.payments(payment_id) deferrable initially deferred,
  lot_id uuid not null references licensing.bonus_lots(lot_id),
  amount_minor bigint not null check (amount_minor > 0),
  state text not null check (state in ('reserved','spent','released','restored')),
  primary key(payment_id,lot_id)
);
-- Runtime roles cannot modify or erase audit events. The migration owner is the
-- only principal allowed to change the schema; business code appends events.
do $$ declare t text; begin
  foreach t in array array['referral_codes','referrals','promo_codes','promotion_quotes','promotion_payments','bonus_lots','bonus_events','bonus_allocations'] loop
    execute format('alter table licensing.%I enable row level security',t);
    execute format('alter table licensing.%I force row level security',t);
    execute format('revoke all on licensing.%I from public',t);
    execute format('grant select,insert on licensing.%I to vg_ledger',t);
    execute format('create policy ledger_promotions on licensing.%I to vg_ledger using (true) with check (true)',t);
  end loop;
end $$;
grant update(qualified_payment_id) on licensing.referrals to vg_ledger;
grant update(active,used,reserved,spent_minor,reserved_minor) on licensing.promo_codes to vg_ledger;
grant update(payment_id) on licensing.promotion_quotes to vg_ledger;
grant update(state,reward_minor,refunded_minor,bonus_restored) on licensing.promotion_payments to vg_ledger;
grant update(remaining_minor,reserved_minor) on licensing.bonus_lots to vg_ledger;
grant update(state) on licensing.bonus_allocations to vg_ledger;
grant select on licensing.referral_codes,licensing.referrals,licensing.promotion_quotes,
  licensing.promotion_payments,licensing.bonus_lots,licensing.bonus_events,licensing.bonus_allocations to vg_admin;
do $$ declare t text; begin
  foreach t in array array['referral_codes','referrals','promotion_quotes','promotion_payments','bonus_lots','bonus_events','bonus_allocations'] loop
    execute format('create policy admin_promotion_metadata on licensing.%I for select to vg_admin using (true)',t);
  end loop;
end $$;
