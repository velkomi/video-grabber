#!/usr/bin/env bash
set -Eeuo pipefail

BASE="${1:-https://videograbber.srv1902378.hstgr.cloud}"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

curl -fsS --max-time 15 "$BASE/health/live" |
  grep -F '"status":"live"' >/dev/null

curl -fsS --max-time 20 "$BASE/web/" -o "$tmp/web.html"
for needle in   "Скачивайте видео проще"   "Вход и синхронизация"   ">Тарифы</h2>"   "10 обычных видео навсегда"   "Популярный"   "Для курсов"   "Full Course"   "/assets/videograbber-hero.webp" "hero-art-shell" "id=\"hero-three\""   "Скачать установщик"   "Portable ZIP"   "/admin/"
do
  grep -F "$needle" "$tmp/web.html" >/dev/null
done

for obsolete in   "Тарифы без скрытых"   "Отправить на мой компьютер"   "Supabase отправит"   "Cannot set properties"   "Сменить тариф"   "/assets/videograbber-sync.webp"   "windows-orb"   "Интерактивная 3D-сцена"
do
  if grep -F "$obsolete" "$tmp/web.html" >/dev/null; then
    echo "Obsolete public UI marker found: $obsolete" >&2
    exit 4
  fi
done

curl -fsS --max-time 20 "$BASE/web/app.js" -o "$tmp/app.js"
curl -fsS --max-time 20 "$BASE/web/hero-three.js" -o "$tmp/hero-three.js"
curl -fsS --max-time 20 "$BASE/web/hero-three.bundle.js" -o "$tmp/hero-three.bundle.js"
curl -fsS --max-time 20 "$BASE/web/vendor/three.module.js" -o "$tmp/three.module.js"
curl -fsS --max-time 20 "$BASE/web/vendor/three.core.js" -o "$tmp/three.core.js"
curl -fsS --max-time 20 "$BASE/assets/videograbber-planet-map.webp" -o "$tmp/planet-map.webp"
curl -fsS --max-time 20 "$BASE/assets/videograbber-planet-bump.png" -o "$tmp/planet-bump.png"
curl -fsS --max-time 20 "$BASE/assets/videograbber-planet-emissive.webp" -o "$tmp/planet-emissive.webp"
test "$(wc -c < "$tmp/planet-map.webp")" -gt 50000
test "$(wc -c < "$tmp/planet-bump.png")" -gt 50000
test "$(wc -c < "$tmp/planet-emissive.webp")" -gt 5000
grep -F "setupHeroScene" "$tmp/app.js" >/dev/null
grep -F 'import("/web/hero-three.bundle.js")' "$tmp/app.js" >/dev/null
grep -F "requestIdleCallback" "$tmp/app.js" >/dev/null
grep -F "setupStoryStage" "$tmp/app.js" >/dev/null
grep -F "scrollTriggerApi.create" "$tmp/app.js" >/dev/null
grep -F "videograbber:story-state" "$tmp/app.js" >/dev/null
grep -F "visualTestMode" "$tmp/app.js" >/dev/null
if grep -F "setupPointerShine" "$tmp/app.js" >/dev/null; then
  echo "Obsolete pointer sheen code is still exposed." >&2
  exit 6
fi
test "$(wc -c < "$tmp/hero-three.bundle.js")" -gt 300000
grep -F "three.js r" "$tmp/hero-three.bundle.js" >/dev/null
grep -F "videograbber-planet-map.webp" "$tmp/hero-three.bundle.js" >/dev/null
grep -F "new THREE.SphereGeometry" "$tmp/hero-three.js" >/dev/null
grep -F "new THREE.TorusGeometry" "$tmp/hero-three.js" >/dev/null
grep -F "new THREE.ExtrudeGeometry" "$tmp/hero-three.js" >/dev/null
grep -F "REVISION" "$tmp/three.module.js" >/dev/null
curl -fsS --max-time 20   -H "Accept-Encoding: br, gzip"   -D "$tmp/three.headers"   -o /dev/null   "$BASE/web/hero-three.bundle.js"
grep -Eiq '^content-encoding: (br|gzip)' "$tmp/three.headers"
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
