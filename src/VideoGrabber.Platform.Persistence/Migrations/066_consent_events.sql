create table licensing.document_versions (
 document_id text not null,
 document_version text not null,
 document_hash text not null check(document_hash ~ '^[a-f0-9]{64}$'),
 content_text text not null check(length(content_text)<=65536),
 primary key(document_id,document_version),
 unique(document_id,document_version,document_hash)
);
alter table licensing.document_versions enable row level security;
alter table licensing.document_versions force row level security;
revoke all on licensing.document_versions from public;
grant select,insert on licensing.document_versions to vg_ledger;
grant select on licensing.document_versions to vg_admin;
create policy ledger_document_versions on licensing.document_versions to vg_ledger using(true) with check(true);
create policy admin_document_versions_read on licensing.document_versions for select to vg_admin using(true);

create table licensing.consent_events (
 sequence bigint generated always as identity primary key,
 account_id uuid not null references licensing.accounts(account_id),
 document_id text not null check(length(document_id) between 1 and 32),
 document_version text not null check(length(document_version) between 1 and 64),
 document_hash text not null check(document_hash ~ '^[a-f0-9]{64}$'),
 decision text not null check(decision in ('accepted','revoked')),
 purpose text not null check(purpose in ('terms','course_rights','marketing')),
 intent_id uuid not null,
 operation_id uuid,
 recorded_at timestamptz not null,
 unique(account_id,intent_id),
 foreign key(document_id,document_version,document_hash) references licensing.document_versions(document_id,document_version,document_hash),
 check((purpose='course_rights')=(operation_id is not null))
);
create index consent_course_lookup on licensing.consent_events(account_id,operation_id,sequence desc) where purpose='course_rights';
alter table licensing.consent_events enable row level security;
alter table licensing.consent_events force row level security;
revoke all on licensing.consent_events from public;
grant select,insert on licensing.consent_events to vg_ledger;
grant usage,select on sequence licensing.consent_events_sequence_seq to vg_ledger;
grant select on licensing.consent_events to vg_admin;
create policy ledger_consents on licensing.consent_events to vg_ledger using(true) with check(true);
create policy admin_consents_read on licensing.consent_events for select to vg_admin using(true);
