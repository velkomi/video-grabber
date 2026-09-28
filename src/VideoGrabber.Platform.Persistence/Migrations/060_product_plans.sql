alter table licensing.accounts
  add column primary_auth_provider text;

alter table licensing.accounts
  add constraint accounts_primary_auth_provider_ck
  check (primary_auth_provider is null
    or primary_auth_provider in ('google','email','telegram','apple','yandex'));

update licensing.accounts a
set primary_auth_provider = first_identity.provider
from (
  select distinct on (account_id)
    account_id,provider
  from licensing.identities
  order by account_id,linked_at,identity_id
) first_identity
where first_identity.account_id=a.account_id
  and a.primary_auth_provider is null;

alter table licensing.entitlement_grants
  add column plan_id text;

alter table licensing.entitlement_grants
  add constraint entitlement_grants_plan_id_ck
  check (plan_id is null or plan_id in ('free','start','unlimited_video','full_course'));

alter table licensing.entitlement_grants
  drop constraint if exists entitlement_grants_source_check;

alter table licensing.entitlement_grants
  add constraint entitlement_grants_source_check
  check (source in ('admin_gift','purchase','adjustment','system_starter'));

create index entitlement_grants_account_plan_idx
  on licensing.entitlement_grants(account_id,plan_id,valid_until,created_at)
  where plan_id is not null;

create unique index entitlement_grants_free_starter_uq
  on licensing.entitlement_grants(account_id)
  where source='system_starter' and plan_id='free';

create policy migrator_free_starter_grant_insert on licensing.entitlement_grants
  for insert to vg_migrator
  with check (
    kind='credits'
    and source='system_starter'
    and plan_id='free'
    and valid_until is null
    and available=10
    and reserved=0
    and original_amount=10
    and reason='free starter allowance'
  );

create policy migrator_free_starter_ledger_insert on licensing.credit_ledger
  for insert to vg_migrator
  with check (
    event_kind='issue'
    and available_delta=10
    and reserved_delta=0
    and spent_delta=0
    and void_delta=0
    and reservation_id is null
    and reason='free starter allowance issued'
  );

with created as (
  insert into licensing.entitlement_grants(
    grant_id,account_id,kind,source,plan_id,
    valid_from,valid_until,available,reserved,original_amount,
    reason,created_at)
  select
    md5('videograbber-free-grant:' || a.account_id::text)::uuid,
    a.account_id,'credits','system_starter','free',
    now(),null,10,0,10,'free starter allowance',now()
  from licensing.accounts a
  where not exists (
    select 1 from licensing.entitlement_grants g
    where g.account_id=a.account_id
      and g.source='system_starter'
      and g.plan_id='free')
  returning grant_id,account_id
)
insert into licensing.credit_ledger(
  ledger_id,account_id,grant_id,event_kind,
  available_delta,reserved_delta,spent_delta,void_delta,
  reason,created_at)
select
  md5('videograbber-free-ledger:' || account_id::text)::uuid,
  account_id,grant_id,'issue',10,0,0,0,
  'free starter allowance issued',now()
from created;

drop policy migrator_free_starter_grant_insert on licensing.entitlement_grants;
drop policy migrator_free_starter_ledger_insert on licensing.credit_ledger;

alter table licensing.subscriptions
  add column plan_id text;

alter table licensing.subscriptions
  add constraint subscriptions_plan_id_ck
  check (plan_id is null or plan_id in ('start','unlimited_video','full_course'));

create index subscriptions_account_plan_idx
  on licensing.subscriptions(account_id,plan_id,paid_through)
  where plan_id is not null;

alter table licensing.devices
  add column last_seen_at timestamptz;

grant update(last_seen_at) on licensing.devices to vg_device;

alter table licensing.reservations
  add column quota_plan_id text,
  add column quota_date date;

alter table licensing.reservations
  add constraint reservations_quota_ck
  check (
    (quota_plan_id is null and quota_date is null)
    or (quota_plan_id='start' and quota_date is not null)
  );

create table licensing.daily_plan_usage (
  account_id uuid not null references licensing.accounts(account_id),
  usage_date date not null,
  plan_id text not null check (plan_id = 'start'),
  reserved bigint not null default 0 check (reserved >= 0),
  spent bigint not null default 0 check (spent >= 0),
  updated_at timestamptz not null default now(),
  primary key(account_id,usage_date,plan_id),
  check (reserved + spent <= 10)
);

alter table licensing.daily_plan_usage enable row level security;
alter table licensing.daily_plan_usage force row level security;
revoke all on licensing.daily_plan_usage from public;

grant select on licensing.daily_plan_usage to vg_api;
grant select,insert,update on licensing.daily_plan_usage to vg_ledger;
grant select on licensing.daily_plan_usage to vg_admin;

create policy daily_plan_usage_owner on licensing.daily_plan_usage
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy ledger_daily_plan_usage on licensing.daily_plan_usage
  to vg_ledger using (true) with check (true);

create policy admin_daily_plan_usage_read on licensing.daily_plan_usage
  for select to vg_admin using (true);

grant insert on licensing.entitlement_grants to vg_identity;
grant insert on licensing.credit_ledger to vg_identity;

create policy identity_free_starter_grant_insert on licensing.entitlement_grants
  for insert to vg_identity
  with check (
    kind='credits'
    and source='system_starter'
    and plan_id='free'
    and valid_until is null
    and available=10
    and reserved=0
    and original_amount=10
    and revoked_at is null
    and admin_id is null
    and idempotency_key is null
    and payload_hash is null
    and source_reference is null
    and reason='free starter allowance'
  );

create policy identity_free_starter_ledger_insert on licensing.credit_ledger
  for insert to vg_identity
  with check (
    event_kind='issue'
    and available_delta=10
    and reserved_delta=0
    and spent_delta=0
    and void_delta=0
    and reservation_id is null
    and actor_account_id is null
    and evidence_id is null
    and reason='free starter allowance issued'
  );
