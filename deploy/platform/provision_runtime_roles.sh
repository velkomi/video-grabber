#!/usr/bin/env bash
set -Eeuo pipefail

export PGPASSWORD="$(cat /run/secrets/postgres_password)"
DB="videograbber"
HOST="postgres"

args=()
for role in api identity admin ledger device operations; do
  args+=("-v" "${role}_password=$(cat /run/secrets/${role}_password)")
done

psql -v ON_ERROR_STOP=1 -h "$HOST" -U postgres -d "$DB" "${args[@]}" <<'SQL'
REVOKE CONNECT ON DATABASE videograbber FROM PUBLIC;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_api_login') THEN
    CREATE ROLE vg_api_login LOGIN NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_identity_login') THEN
    CREATE ROLE vg_identity_login LOGIN NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_admin_login') THEN
    CREATE ROLE vg_admin_login LOGIN NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_ledger_login') THEN
    CREATE ROLE vg_ledger_login LOGIN NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_device_login') THEN
    CREATE ROLE vg_device_login LOGIN NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vg_operations_login') THEN
    CREATE ROLE vg_operations_login LOGIN NOINHERIT;
  END IF;
END $$;

ALTER ROLE vg_api_login PASSWORD :'api_password';
ALTER ROLE vg_identity_login PASSWORD :'identity_password';
ALTER ROLE vg_admin_login PASSWORD :'admin_password';
ALTER ROLE vg_ledger_login PASSWORD :'ledger_password';
ALTER ROLE vg_device_login PASSWORD :'device_password';
ALTER ROLE vg_operations_login PASSWORD :'operations_password';

GRANT vg_api TO vg_api_login;
GRANT vg_identity TO vg_identity_login;
GRANT vg_admin TO vg_admin_login;
GRANT vg_ledger TO vg_ledger_login;
GRANT vg_device TO vg_device_login;
GRANT vg_operations TO vg_operations_login;

GRANT CONNECT ON DATABASE videograbber TO
  vg_api_login,
  vg_identity_login,
  vg_admin_login,
  vg_ledger_login,
  vg_device_login,
  vg_operations_login;
SQL

unset PGPASSWORD
echo "VIDEOGRABBER_RUNTIME_ROLES_READY"
