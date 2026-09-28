create table licensing.desktop_auth_handoffs (
  flow_id uuid primary key,
  account_id uuid references licensing.accounts(account_id),
  return_uri text not null,
  state_hash text not null,
  code_hash text,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  approved_at timestamptz,
  consumed_at timestamptz,
  check (expires_at > created_at),
  check ((approved_at is null and account_id is null and code_hash is null)
      or (approved_at is not null and account_id is not null and code_hash is not null))
);

create index desktop_auth_handoffs_expiry_idx
  on licensing.desktop_auth_handoffs(expires_at)
  where consumed_at is null;

alter table licensing.desktop_auth_handoffs enable row level security;
alter table licensing.desktop_auth_handoffs force row level security;
revoke all on licensing.desktop_auth_handoffs from public;

grant select,insert,update,delete
  on licensing.desktop_auth_handoffs to vg_identity;

create policy identity_desktop_auth_handoffs
  on licensing.desktop_auth_handoffs
  to vg_identity
  using (true)
  with check (true);
