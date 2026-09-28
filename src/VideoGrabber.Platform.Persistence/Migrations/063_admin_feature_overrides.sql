create table licensing.admin_feature_overrides (
  account_id uuid not null references licensing.accounts(account_id),
  feature text not null check (feature in (
    'download','mp3','trim','join','transcribe','course_download',
    'browser_download','telegram_delivery'
  )),
  enabled boolean not null,
  valid_until timestamptz,
  reason text not null,
  admin_id uuid not null references licensing.accounts(account_id),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key(account_id,feature),
  check (valid_until is null or valid_until > created_at)
);

create index admin_feature_overrides_active_idx
  on licensing.admin_feature_overrides(account_id,valid_until);

alter table licensing.admin_feature_overrides enable row level security;
alter table licensing.admin_feature_overrides force row level security;
revoke all on licensing.admin_feature_overrides from public;
grant select on licensing.admin_feature_overrides to vg_api;
grant select,insert,update,delete on licensing.admin_feature_overrides to vg_admin;

create policy admin_feature_overrides_owner
  on licensing.admin_feature_overrides
  for select to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create policy admin_feature_overrides_admin
  on licensing.admin_feature_overrides
  to vg_admin using (true) with check (true);

create table licensing.admin_mfa (
  account_id uuid primary key references licensing.accounts(account_id),
  secret_cipher bytea not null,
  enabled boolean not null default false,
  failed_attempts integer not null default 0 check (failed_attempts >= 0),
  locked_until timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table licensing.admin_mfa enable row level security;
alter table licensing.admin_mfa force row level security;
revoke all on licensing.admin_mfa from public;
grant select,insert,update,delete on licensing.admin_mfa to vg_admin;

create policy admin_mfa_admin
  on licensing.admin_mfa
  to vg_admin using (true) with check (true);
