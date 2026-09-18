create table licensing.delivery_attempts (
  delivery_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  job_id uuid not null references licensing.jobs(job_id),
  artifact_id uuid not null references licensing.artifacts(artifact_id),
  destination_id uuid not null references licensing.delivery_destinations(destination_id),
  idempotency_key uuid not null,
  state text not null check (state in (
    'pending','sending','delivered','failed_before_send','delivery_unknown')),
  message_id bigint,
  telegram_file_id text,
  reason text not null,
  retry_count integer not null default 0,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(account_id,idempotency_key)
);
create index delivery_attempts_account_state_idx
  on licensing.delivery_attempts(account_id,state,created_at,delivery_id);
create index delivery_attempts_artifact_active_idx
  on licensing.delivery_attempts(artifact_id,state)
  where state in ('pending','sending','delivery_unknown');

alter table licensing.artifacts add column retained_until timestamptz;
alter table licensing.artifacts add column expired_at timestamptz;
alter table licensing.artifacts add column unavailable_reason text;

revoke all on licensing.delivery_attempts from public;
grant select,insert,update on licensing.delivery_attempts to vg_api;
grant select,insert,update on licensing.delivery_attempts to vg_ledger;
grant update(retained_until,expired_at,unavailable_reason) on licensing.artifacts to vg_api;
