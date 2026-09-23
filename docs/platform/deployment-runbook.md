# Isolated platform deployment runbook

The committed manifests are safe-by-default staging material. Postgres, worker, admin diagnostics and optional local Telegram Bot API do not publish public host ports. The API may bind only to a loopback host port (the production VPS uses `127.0.0.1:19230`) so the already-qualified host Traefik edge can terminate public TLS without joining the application containers to the host network. The bundled Caddy edge is now an opt-in `internal-caddy` profile for isolated staging.

All base images are digest pinned. API and worker images are not considered deployable until they are built from the reviewed source commit and addressed immutably by repo digest. An external registry is not required: a loopback-only registry on the VPS is acceptable when the resulting `name@sha256:...` digests are recorded and the registry is not exposed publicly. `Start-PlatformStage.ps1` rejects missing application digests, a source-commit mismatch, wildcard CORS, development issuer, public DB/Bot API/admin binds, bad callback hosts, missing key references and live selling without a catalog.

Secrets are file mounts. The env example contains paths only. `prepare_runtime_secrets.sh` creates protected one-time runtime credentials without overwriting an existing Telegram bot token. The migrator is the only component that receives the PostgreSQL owner DSN. After migrations, the one-shot role provisioner creates six NOINHERIT login roles (API, identity, admin, ledger, device and operations); each can SET ROLE only to its corresponding RLS role. Runtime services never receive the PostgreSQL owner password and cannot alter schema.

Source probing and server-worker media traffic use the digest-pinned Squid egress proxy. The proxy permits normal HTTP/HTTPS but explicitly denies loopback, RFC1918, carrier-grade NAT, link-local, benchmark and IPv6 local ranges. Deployment acceptance includes a real proxy check: public HTTPS succeeds and loopback is denied.

The Web account surface is served by Platform.Api at `/web/`. Set `VG_PLATFORM_PUBLIC_URL` and the exact `VG_WEB_AUTH_RETURN_URI`; arbitrary HTTPS OAuth return URLs are rejected. Desktop sign-in continues to use only the exact ephemeral IPv4 loopback callback. Web/Desktop local-only jobs do not require finished media to be persisted on the VPS; server artifacts that do exist remain subject to retention cleanup.

The optional local Bot API profile is disabled by default. The currently qualified container is third-party; for production prefer building the official TDLib Telegram Bot API source at a reviewed commit, then replace the lock with that image digest. Do not use `latest`.

`/health/live` proves the API process responds. `/health/ready` additionally checks migration history, DB access, worker capability secret presence and auth issuer configuration. Health responses expose no DSN or secret.

Stage launch remains **BLOCKED** unless `VG_STAGE_START_AUTHORIZED=YES` is explicitly provided after configuration and registry digests are reviewed. This mechanism never changes DNS, firewall rules, production databases or neighboring Docker workloads.
