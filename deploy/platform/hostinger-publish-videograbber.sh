#!/usr/bin/env bash
set -Eeuo pipefail

HOST="videograbber.srv1902378.hstgr.cloud"
NAME="videograbber-public-proxy"
NETWORK="valery-kanev-web"
BACKEND_NETWORK="vg-stage-videograbber_edge"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CFG="$SCRIPT_DIR/videograbber-public-nginx.conf"
IMAGE="nginx@sha256:dc5069ad14f19660b141b21236140b91656bf89bbc3e2417c70ae650cd66104c"
ROUTER_RULE="Host(\`$HOST\`)"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0"
  exit 2
fi

getent hosts "$HOST" >/dev/null
curl -fsS --max-time 5 http://127.0.0.1:19230/health/live >/dev/null

docker network inspect "$NETWORK" >/dev/null
docker network inspect "$BACKEND_NETWORK" >/dev/null
docker image inspect "$IMAGE" >/dev/null 2>&1 || docker pull "$IMAGE"

docker rm -f "$NAME" >/dev/null 2>&1 || true

docker run -d \
  --name "$NAME" \
  --restart unless-stopped \
  --network "$NETWORK" \
  --add-host host.docker.internal:host-gateway \
  --read-only \
  --user 101:101 \
  --entrypoint nginx \
  --cap-drop ALL \
  --security-opt no-new-privileges:true \
  --tmpfs /var/cache/nginx:rw,noexec,nosuid,size=8m,uid=101,gid=101,mode=0755 \
  --tmpfs /var/run:rw,noexec,nosuid,size=2m,uid=101,gid=101,mode=0755 \
  --tmpfs /tmp:rw,noexec,nosuid,size=4m,uid=101,gid=101,mode=1777 \
  --memory 64m \
  --cpus 0.25 \
  --pids-limit 32 \
  -v "$CFG:/etc/nginx/conf.d/default.conf:ro" \
  --label traefik.enable=true \
  --label traefik.docker.network="$NETWORK" \
  --label "traefik.http.routers.videograbber.rule=$ROUTER_RULE" \
  --label traefik.http.routers.videograbber.entrypoints=websecure \
  --label traefik.http.routers.videograbber.tls.certresolver=letsencrypt \
  --label traefik.http.routers.videograbber.service=videograbber \
  --label traefik.http.services.videograbber.loadbalancer.server.port=8080 \
  "$IMAGE" -g 'daemon off;'

docker network connect "$BACKEND_NETWORK" "$NAME"

for _ in $(seq 1 15); do
  if [ "$(docker inspect -f '{{.State.Status}}' "$NAME" 2>/dev/null || true)" = "running" ]; then
    docker exec "$NAME" nginx -t >/dev/null 2>&1 && break
  fi
  sleep 1
done

docker exec "$NAME" nginx -t

for _ in $(seq 1 45); do
  if curl -fsS --max-time 8 "https://$HOST/health/live" >/dev/null 2>&1; then
    echo "VIDEOGRABBER_PUBLIC_HTTPS_OK https://$HOST"
    exit 0
  fi
  sleep 2
done

echo "VideoGrabber proxy started, but HTTPS verification failed."
docker logs --tail 80 "$NAME" || true
exit 4
