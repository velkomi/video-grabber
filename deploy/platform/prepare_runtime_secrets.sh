#!/usr/bin/env bash
set -Eeuo pipefail

SECRET_DIR="${VG_SECRET_DIR:-/home/assistant/.config/videograbber}"
mkdir -p "$SECRET_DIR"
chmod 700 "$SECRET_DIR"
umask 077

rand_url() { python3 - <<'PY'
import secrets
print(secrets.token_urlsafe(32))
PY
}
rand_b64_32() { python3 - <<'PY'
import base64,secrets
print(base64.b64encode(secrets.token_bytes(32)).decode())
PY
}
write_once() {
  local name="$1"; shift
  local path="$SECRET_DIR/$name"
  if [ ! -s "$path" ]; then
    "$@" > "$path"
    chmod 600 "$path"
  fi
}
write_text_once() {
  local name="$1" value="$2" path="$SECRET_DIR/$1"
  if [ ! -s "$path" ]; then
    printf '%s' "$value" > "$path"
    chmod 600 "$path"
  fi
}

write_once postgres_password rand_url
for role in api identity admin ledger device operations; do
  write_once "${role}_password" rand_url
done
write_once worker_token rand_url
write_once session_key rand_url
write_once operations_token rand_url
write_once source_encryption_key rand_b64_32

if [ ! -s "$SECRET_DIR/lease_signing_key_pkcs8" ]; then
  openssl ecparam -name prime256v1 -genkey -noout 2>/dev/null \
    | openssl pkcs8 -topk8 -nocrypt -outform DER 2>/dev/null \
    | base64 -w0 > "$SECRET_DIR/lease_signing_key_pkcs8"
  chmod 600 "$SECRET_DIR/lease_signing_key_pkcs8"
fi

for required in telegram_bot_token telegram_webhook_secret telegram_inbox_key telegram_bot_user_id; do
  if [ ! -s "$SECRET_DIR/$required" ]; then
    echo "Missing Telegram secret: $SECRET_DIR/$required" >&2
    exit 3
  fi
done

pgpw="$(cat "$SECRET_DIR/postgres_password")"
write_text_once migration_dsn "Host=postgres;Port=5432;Database=videograbber;Username=postgres;Password=$pgpw;SSL Mode=Disable"

for role in api identity admin ledger device operations; do
  pw="$(cat "$SECRET_DIR/${role}_password")"
  write_text_once "${role}_dsn" "Host=postgres;Port=5432;Database=videograbber;Username=vg_${role}_login;Password=$pw;Options=-c role=vg_${role};SSL Mode=Disable"
done

for f in "$SECRET_DIR"/*; do chmod 600 "$f"; done
chmod 700 "$SECRET_DIR"
echo "VIDEOGRABBER_RUNTIME_SECRETS_READY"
