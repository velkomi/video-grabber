create schema support;
revoke all on schema support from public;
grant usage on schema support to vg_api,vg_admin;

create table support.requests (
 request_id uuid primary key,
 ticket_id uuid not null unique,
 account_id uuid references licensing.accounts(account_id),
 caller_key text not null check(length(caller_key)<=100),
 rate_key text not null check(length(rate_key)<=100),
 ip_key text not null check(length(ip_key)=64),
 request_hash text not null check(length(request_hash)=64),
 topic text not null check(topic in ('sign_in','download','subscription','suggestion','other')),
 source text not null check(source in ('form','telegram','windows','miniapp')),
 encrypted_payload bytea not null check(octet_length(encrypted_payload)<=32768),
 created_at timestamptz not null,
 closed_at timestamptz
);
create index support_caller_recent on support.requests(rate_key,created_at desc);
create index support_ip_recent on support.requests(ip_key,created_at desc);

create table support.challenge_claims (
 challenge_key text primary key check(length(challenge_key)=32),
 ticket_id uuid not null references support.requests(ticket_id),
 expires_at timestamptz not null
);

create table support.notifications (
 ticket_id uuid not null references support.requests(ticket_id),
 channel text not null check(channel in ('email','telegram')),
 state text not null check(state in ('pending','sending','delivered','failed','review_required')),
 attempts integer not null default 0 check(attempts between 0 and 3),
 next_attempt_at timestamptz not null,
 claim_id uuid,
 claim_expires_at timestamptz,
 sent_at timestamptz,
 error_code text check(length(error_code)<=64),
 primary key(ticket_id,channel)
);
create index support_notification_due on support.notifications(channel,state,next_attempt_at);
create index support_notification_budget on support.notifications(channel,sent_at) where sent_at is not null;

alter table support.requests enable row level security;
alter table support.requests force row level security;
alter table support.challenge_claims enable row level security;
alter table support.challenge_claims force row level security;
alter table support.notifications enable row level security;
alter table support.notifications force row level security;
revoke all on support.requests,support.challenge_claims,support.notifications from public;
grant select,insert,update on support.requests,support.challenge_claims,support.notifications to vg_api;
grant select on support.requests,support.notifications to vg_admin;
create policy api_support_requests on support.requests to vg_api using(true) with check(true);
create policy api_support_challenges on support.challenge_claims to vg_api using(true) with check(true);
create policy api_support_notifications on support.notifications to vg_api using(true) with check(true);
create policy admin_support_requests on support.requests for select to vg_admin using(true);
create policy admin_support_notifications on support.notifications for select to vg_admin using(true);
