alter table licensing.jobs alter column source_id drop not null;
alter table licensing.jobs add column queue_order bigint not null default 0;
alter table licensing.jobs add column queue_version bigint not null default 1;

alter table licensing.artifacts add column storage_path text;

create index jobs_account_queue_idx
  on licensing.jobs(account_id,state,queue_order,created_at,job_id);