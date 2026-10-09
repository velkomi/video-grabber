create table support.bot_modes (
 user_id bigint primary key check(user_id>0),
 chat_id bigint not null check(chat_id>0),
 expires_at timestamptz not null
);
create table support.bot_input_windows (
 user_id bigint primary key check(user_id>0),
 started_at timestamptz not null,
 request_count integer not null check(request_count between 1 and 31)
);
alter table support.bot_modes enable row level security;
alter table support.bot_modes force row level security;
alter table support.bot_input_windows enable row level security;
alter table support.bot_input_windows force row level security;
revoke all on support.bot_modes,support.bot_input_windows from public;
grant select,insert,update on support.bot_modes,support.bot_input_windows to vg_api;
create policy api_bot_modes on support.bot_modes to vg_api using(true) with check(true);
create policy api_bot_input_windows on support.bot_input_windows to vg_api using(true) with check(true);
