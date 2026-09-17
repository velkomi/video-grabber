grant select,update on licensing.devices to vg_admin;
create policy admin_devices on licensing.devices
  to vg_admin using (true) with check (true);

grant select,update on licensing.reservations to vg_admin;
create policy admin_reservations on licensing.reservations
  to vg_admin using (true) with check (true);

grant select,update on licensing.api_sessions to vg_admin;
create policy admin_api_sessions on licensing.api_sessions
  to vg_admin using (true) with check (true);

create table licensing.account_merge_relations (
  source_account_id uuid primary key references licensing.accounts(account_id),
  target_account_id uuid not null references licensing.accounts(account_id),
  actor_account_id uuid not null references licensing.accounts(account_id),
  reason text not null,
  created_at timestamptz not null default now(),
  check (source_account_id <> target_account_id)
);
alter table licensing.account_merge_relations enable row level security;
alter table licensing.account_merge_relations force row level security;
revoke all on licensing.account_merge_relations from public;
grant select,insert on licensing.account_merge_relations to vg_admin;
create policy admin_merge_relations on licensing.account_merge_relations
  to vg_admin using (true) with check (true);

revoke update,delete on licensing.account_merge_relations from vg_admin;
revoke delete on licensing.devices,licensing.reservations,licensing.api_sessions from vg_admin;
