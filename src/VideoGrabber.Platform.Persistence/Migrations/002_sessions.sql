create table licensing.auth_flows (
  flow_id uuid primary key,
  provider text not null,
  return_uri text not null,
  state_hash text not null,
  nonce_value text not null,
  client_challenge text not null,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null,
  consumed_at timestamptz
);

create index auth_flows_expiry_idx
  on licensing.auth_flows(expires_at)
  where consumed_at is null;

create table licensing.api_sessions (
  session_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  family_id uuid not null,
  refresh_hash text not null unique,
  created_at timestamptz not null,
  refresh_expires_at timestamptz not null,
  revoked_at timestamptz
);

grant select, insert, update, delete on licensing.auth_flows to vg_api;
grant select, insert, update on licensing.api_sessions to vg_api;
revoke all on licensing.auth_flows, licensing.api_sessions from public;

alter table licensing.api_sessions enable row level security;
alter table licensing.api_sessions force row level security;
create policy api_session_owner on licensing.api_sessions
  to vg_api
  using (account_id = nullif(current_setting('vg.account_id', true), '')::uuid)
  with check (account_id = nullif(current_setting('vg.account_id', true), '')::uuid);

create table licensing.broker_bindings (
  provider text not null,
  broker_subject text not null,
  provider_subject text not null,
  created_at timestamptz not null default now(),
  primary key(provider, broker_subject),
  unique(provider, provider_subject)
);
grant select, insert on licensing.broker_bindings to vg_api;
revoke all on licensing.broker_bindings from public;
