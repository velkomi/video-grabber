create table support.notification_attempts (
 claim_id uuid primary key,
 ticket_id uuid not null references support.requests(ticket_id),
 channel text not null check(channel in ('email','telegram')),
 reserved_at timestamptz not null
);
create index support_dispatch_budget on support.notification_attempts(channel,reserved_at desc);
create index support_requests_open on support.requests(created_at) where closed_at is null;
alter table support.notification_attempts enable row level security;
alter table support.notification_attempts force row level security;
revoke all on support.notification_attempts from public;
grant select,insert on support.notification_attempts to vg_api;
grant select on support.notification_attempts to vg_admin;
create policy api_support_attempts on support.notification_attempts to vg_api using(true) with check(true);
create policy admin_support_attempts on support.notification_attempts for select to vg_admin using(true);
