create table licensing.sources (
  source_id text primary key check (length(source_id) between 16 and 128),
  account_id uuid not null references licensing.accounts(account_id),
  media_id text not null,
  source_cipher bytea not null,
  qualities jsonb not null check (jsonb_typeof(qualities)='array'),
  expires_at timestamptz not null,
  created_at timestamptz not null default now()
);
create index sources_account_expiry_idx on licensing.sources(account_id,expires_at);

create table licensing.jobs (
  job_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  intent_id uuid not null,
  reservation_id uuid references licensing.reservations(reservation_id),
  request_hash text not null,
  kind text not null,
  executor text not null check (executor in ('server_worker','desktop_worker')),
  device_id uuid,
  source_id text not null references licensing.sources(source_id),
  quality text not null,
  input_artifact_ids uuid[] not null default array[]::uuid[],
  trim_start_ms bigint,
  trim_duration_ms bigint,
  state text not null check (state in (
    'queued','waiting_for_worker','running','cancel_requested','cancelled',
    'completed','review_required','failed')),
  fence bigint not null default 0,
  artifact_id uuid,
  reason text not null,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(account_id,intent_id)
);
create index jobs_claim_idx on licensing.jobs(executor,state,created_at,job_id);
create index jobs_account_idx on licensing.jobs(account_id,created_at,job_id);

create table licensing.job_attempts (
  attempt_id uuid primary key,
  job_id uuid not null references licensing.jobs(job_id),
  worker_id uuid not null,
  fence bigint not null,
  capability_hash text not null,
  lease_until timestamptz not null,
  runtime_deadline timestamptz not null,
  state text not null check (state in ('running','completed','failed','expired','cancelled')),
  started_at timestamptz not null,
  heartbeat_at timestamptz not null,
  completed_at timestamptz,
  outcome text,
  evidence_id text,
  unique(job_id,fence)
);
create index job_attempts_active_idx on licensing.job_attempts(job_id,state,lease_until);

create table licensing.artifacts (
  artifact_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  job_id uuid not null references licensing.jobs(job_id),
  attempt_id uuid not null references licensing.job_attempts(attempt_id),
  fence bigint not null,
  sha256 text not null,
  bytes bigint not null check (bytes>0),
  media_type text not null,
  verification_evidence_id text not null,
  created_at timestamptz not null,
  unique(job_id)
);

alter table licensing.jobs
  add constraint jobs_artifact_fk foreign key (artifact_id) references licensing.artifacts(artifact_id);

create table licensing.job_outbox (
  event_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  job_id uuid not null references licensing.jobs(job_id),
  event_type text not null,
  payload jsonb not null,
  created_at timestamptz not null,
  consumed_at timestamptz
);
create index job_outbox_pending_idx on licensing.job_outbox(created_at,event_id) where consumed_at is null;

revoke all on licensing.sources,licensing.jobs,licensing.job_attempts,licensing.artifacts,licensing.job_outbox from public;
grant select,insert,update on licensing.sources,licensing.jobs,licensing.job_attempts,licensing.artifacts,licensing.job_outbox to vg_ledger;
grant select on licensing.sources,licensing.jobs,licensing.artifacts to vg_api;