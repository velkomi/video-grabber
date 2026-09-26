#!/usr/bin/env bash
set -Eeuo pipefail

SECRET_DIR="${VG_SECRET_DIR:-/home/assistant/.config/videograbber}"
BACKUP_DIR="$SECRET_DIR/backups"
RETENTION_ROOT="/var/lib/videograbber/jobs"
mkdir -p "$BACKUP_DIR"
chmod 700 "$SECRET_DIR" "$BACKUP_DIR"
umask 077

postgres_container="vg-stage-videograbber-postgres-1"
worker_volume="vg-stage-videograbber-workerjobs"
ts="$(date -u +%Y%m%dT%H%M%SZ)"
iso="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
tmp="$BACKUP_DIR/.videograbber-$ts.dump.tmp"
out="$BACKUP_DIR/videograbber-$ts.dump"

docker exec "$postgres_container" pg_dump -U postgres -d videograbber -Fc > "$tmp"
docker exec -i "$postgres_container" pg_restore --list < "$tmp" > /dev/null
mv "$tmp" "$out"
chmod 600 "$out"
sha256sum "$out" > "$out.sha256"
chmod 600 "$out.sha256"

read -r total with_storage expired unsafe active <<<"$(docker exec "$postgres_container" psql -U postgres -d videograbber -At -F ' ' -c "
select
  (select count(*) from licensing.artifacts),
  (select count(*) from licensing.artifacts where storage_path is not null),
  (select count(*) from licensing.artifacts where retained_until is not null and retained_until <= now() and expired_at is null),
  (select count(*) from licensing.artifacts where storage_path is not null and storage_path not like '/var/lib/videograbber/jobs/%'),
  (select count(*) from licensing.delivery_attempts where state in ('pending','sending','delivery_unknown'));
")"

if [ "$unsafe" != "0" ]; then
  echo "Unsafe artifact paths detected; retention marker not advanced." >&2
  exit 4
fi

symlink_report="/home/assistant/apps/videograbber-platform/.maintenance-symlinks.txt"
docker run --rm -v "$worker_volume:/data:ro" alpine:3.22 sh -c '
  find /data -xdev -type l -print
' > "$symlink_report"
chmod 0644 "$symlink_report"
if [ -s "$symlink_report" ]; then
  echo "Symlink detected in worker volume; retention marker not advanced. See $symlink_report" >&2
  exit 5
fi

printf '%s\n' "$iso" > "$SECRET_DIR/last_backup_utc"
printf '%s\n' "$iso" > "$SECRET_DIR/retention_dry_run_utc"
chmod 600 "$SECRET_DIR/last_backup_utc" "$SECRET_DIR/retention_dry_run_utc"

# Keep two days of 10-minute backups; prune only after a validated new dump exists.
find "$BACKUP_DIR" -maxdepth 1 -type f -name 'videograbber-*.dump' -mmin +2880 -delete
find "$BACKUP_DIR" -maxdepth 1 -type f -name 'videograbber-*.dump.sha256' -mmin +2880 -delete

echo "VIDEOGRABBER_MAINTENANCE_OK backup=$out artifacts=$total stored=$with_storage expired=$expired active=$active"