create table licensing.artifact_uploads (
  upload_id uuid primary key,
  account_id uuid not null references licensing.accounts(account_id),
  device_id uuid not null references licensing.devices(device_id),
  job_id uuid not null references licensing.jobs(job_id),
  attempt_id uuid not null references licensing.job_attempts(attempt_id),
  fence bigint not null,
  declared_length bigint not null check (declared_length > 0),
  declared_sha256 text not null,
  media_type text not null,
  expires_at timestamptz not null,
  state text not null check (state in ('pending','receiving','uploaded','failed')),
  server_path text,
  created_at timestamptz not null,
  uploaded_at timestamptz
);
create index artifact_uploads_scope_idx
  on licensing.artifact_uploads(account_id,device_id,attempt_id,state,expires_at);

alter table licensing.artifact_uploads enable row level security;
alter table licensing.artifact_uploads force row level security;
revoke all on licensing.artifact_uploads from public;
grant select,insert,update on licensing.artifact_uploads to vg_ledger;
create policy ledger_artifact_upload_access on licensing.artifact_uploads
  to vg_ledger using (true) with check (true);