do $$
begin
  if not exists (select 1 from pg_roles where rolname='vg_backup') then
    create role vg_backup nologin nobypassrls nocreatedb nocreaterole;
  end if;
end $$;

grant usage on schema licensing,vg_migrations to vg_backup;
grant select on all tables in schema licensing,vg_migrations to vg_backup;
alter default privileges in schema licensing grant select on tables to vg_backup;
revoke insert,update,delete,truncate,references,trigger
  on all tables in schema licensing from vg_backup;
