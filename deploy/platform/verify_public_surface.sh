#!/usr/bin/env bash
set -Eeuo pipefail

BASE="${1:-https://videograbber.srv1902378.hstgr.cloud}"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

curl -fsS --max-time 15 "$BASE/health/live" | grep -F '"status":"live"' >/dev/null

curl -fsS --max-time 20 "$BASE/web/" -o "$tmp/web.html"
for needle in   "YouTube"   "Instagram Reels"   "TikTok"   "Pinterest"   "Редактирование видео"   "Скачать установщик (.exe)"   "Portable ZIP"   "/admin/"
do
  grep -F "$needle" "$tmp/web.html" >/dev/null
done

curl -fsS --max-time 20 "$BASE/admin/" -o "$tmp/admin.html"
grep -F "Двухфакторная аутентификация" "$tmp/admin.html" >/dev/null
grep -F "feature-grid" "$tmp/admin.html" >/dev/null

curl -fsS --max-time 30 -H "Range: bytes=0-15"   "$BASE/download/windows" -o "$tmp/setup.bin"
test "$(od -An -tx1 -N2 "$tmp/setup.bin" | tr -d ' \n')" = "4d5a"

curl -fsS --max-time 30 -H "Range: bytes=0-3"   "$BASE/download/windows/portable" -o "$tmp/portable.bin"
test "$(od -An -tx1 -N2 "$tmp/portable.bin" | tr -d ' \n')" = "504b"

echo "PUBLIC_SURFACE_OK=$BASE"
