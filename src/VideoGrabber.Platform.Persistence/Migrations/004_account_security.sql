revoke all on schema licensing from public;
revoke all on all tables in schema licensing from public;
revoke all on all functions in schema licensing from public;

alter default privileges in schema licensing
  revoke execute on functions from public;

revoke create on schema licensing from vg_api, vg_identity, vg_admin;

-- Runtime roles remain NOLOGIN and non-BYPASSRLS. No runtime role is
-- granted membership in another runtime role; privileged paths use
-- dedicated connection strings and explicit role selection.