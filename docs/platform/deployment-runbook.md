# Isolated platform deployment runbook

The committed manifests are safe-by-default staging material, not production authorization. An environment name must match `vg-stage-*`; production-like names are rejected. Runtime Postgres, API, worker, admin diagnostics and optional local Telegram Bot API do not publish host ports. Only Caddy maps an HTTPS listener and the example maps it to loopback port 8443.

All base images are digest pinned. API and worker images are not considered deployable until they are built from the reviewed source commit, published to an authorized registry and recorded as repo digests in a generated dependency lock. `Start-PlatformStage.ps1` rejects missing application digests, a source-commit mismatch, wildcard CORS, development issuer, public DB/Bot API/admin binds, bad callback hosts, missing key references and live selling without a catalog.

Secrets are file mounts. The env example contains paths only. The API and migrator read DSNs/tokens into process environment inside their own containers; values are not written to evidence. The migrator is a one-shot command using a migration-only DSN. Runtime roles cannot alter schema.

The optional local Bot API profile is disabled by default. The currently qualified container is third-party; for production prefer building the official TDLib Telegram Bot API source at a reviewed commit, then replace the lock with that image digest. Do not use `latest`.

`/health/live` proves the API process responds. `/health/ready` additionally checks migration history, DB access, worker capability secret presence and auth issuer configuration. Health responses expose no DSN or secret.

Stage launch remains **BLOCKED** unless `VG_STAGE_START_AUTHORIZED=YES` is explicitly provided after configuration and registry digests are reviewed. This mechanism never changes DNS, firewall rules, production databases or neighboring Docker workloads.
