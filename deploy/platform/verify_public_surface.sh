#!/usr/bin/env bash
set -Eeuo pipefail

BASE="${1:-https://videograbber.srv1902378.hstgr.cloud}"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

curl -fsS --max-time 15 "$BASE/health/live" |
  grep -F '"status":"live"' >/dev/null

curl -fsS --max-time 20 "$BASE/web/" -o "$tmp/web.html"
for needle in   "Скачивайте видео проще"   "Вход и синхронизация"   ">Тарифы</h2>"   "10 обычных видео навсегда"   "Популярный"   "Для курсов"   "Full Course"   "/assets/videograbber-hero.webp" "hero-art-shell" "id=\"hero-webgl\""   "Скачать установщик"   "Portable ZIP"   "/admin/"
do
  grep -F "$needle" "$tmp/web.html" >/dev/null
done

for obsolete in   "Тарифы без скрытых"   "Отправить на мой компьютер"   "Supabase отправит"   "Cannot set properties"
do
  if grep -F "$obsolete" "$tmp/web.html" >/dev/null; then
    echo "Obsolete public UI marker found: $obsolete" >&2
    exit 4
  fi
done

curl -fsS --max-time 20 "$BASE/web/app.js" -o "$tmp/app.js"
grep -F "setupHeroScene" "$tmp/app.js" >/dev/null
grep -F "Не удалось загрузить данные аккаунта" "$tmp/app.js" >/dev/null
if grep -F "Не удалось загрузить кабинет:" "$tmp/app.js" >/dev/null; then
  echo "Raw dashboard error text is still exposed." >&2
  exit 5
fi

curl -fsS --max-time 20 "$BASE/admin/" -o "$tmp/admin.html"
grep -F "Двухфакторная аутентификация" "$tmp/admin.html" >/dev/null
grep -F "feature-grid" "$tmp/admin.html" >/dev/null

curl -fsS --max-time 30 -H "Range: bytes=0-15"   "$BASE/download/windows" -o "$tmp/setup.bin"
test "$(od -An -tx1 -N2 "$tmp/setup.bin" | tr -d ' \n')" = "4d5a"

curl -fsS --max-time 30 -H "Range: bytes=0-3"   "$BASE/download/windows/portable" -o "$tmp/portable.bin"
test "$(od -An -tx1 -N2 "$tmp/portable.bin" | tr -d ' \n')" = "504b"

curl -fsS --max-time 20 "$BASE/miniapp/" >/dev/null

echo "PUBLIC_SURFACE_OK=$BASE"
