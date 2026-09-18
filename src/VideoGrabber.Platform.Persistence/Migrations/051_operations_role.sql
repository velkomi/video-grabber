do $$
begin
  if not exists (select 1 from pg_roles where rolname='vg_operations') then
    create role vg_operations nologin nobypassrls nocreatedb nocreaterole;
  end if;
end $$;

grant usage on schema licensing,vg_migrations to vg_operations;
grant select on licensing.jobs,
                licensing.job_attempts,
                licensing.reservations,
                licensing.payments,
                licensing.delivery_attempts,
                licensing.job_outbox
  to vg_operations;
grant select on vg_migrations.applied_migrations to vg_operations;

create policy operations_jobs_read on licensing.jobs
  for select to vg_operations using (true);
create policy operations_job_attempts_read on licensing.job_attempts
  for select to vg_operations using (true);
create policy operations_reservations_read on licensing.reservations
  for select to vg_operations using (true);
create policy operations_payments_read on licensing.payments
  for select to vg_operations using (true);
create policy operations_delivery_attempts_read on licensing.delivery_attempts
  for select to vg_operations using (true);

revoke insert,update,delete,truncate,references,trigger
  on licensing.jobs,
     licensing.job_attempts,
     licensing.reservations,
     licensing.payments,
     licensing.delivery_attempts,
     licensing.job_outbox
  from vg_operations;
