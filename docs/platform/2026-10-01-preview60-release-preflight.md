# Preview.60 / post-preview.60 release preflight

Date: 2026-10-01

This document separates code readiness from release/deployment evidence. A green CI run is necessary but is not sufficient to publish or deploy VideoGrabber.

## 1. Source and CI

Required:
- repository VERSION equals the intended release version;
- source commit is recorded exactly;
- GitHub Actions Build is green on that exact commit;
- `scripts/Build-Web3D.ps1` reproduces the committed Three.js bundle from `hero-three.js`;
- Web static regression confirms no external runtime CDN and no stale/misleading public UI.

## 2. Release-acceptance evidence

Run `scripts/platform/Test-PlatformRelease.ps1` with the exact source commit and a persistent evidence directory.

Local mode requires explicit live/manual evidence for:
- clean Windows Local/Managed;
- authorized course download;
- Telegram Mini App;
- provider collision flows;
- sandbox payments including recurring/refunds.

Stage mode additionally requires:
- stage deployment;
- recovery drill;
- alert delivery;
- capacity baseline.

Missing evidence is BLOCKED, never an implicit PASS.

## 3. Official Windows assets

Do not advance GitHub `release/latest` for preview.60 until all of these exist for the same version/source:
- `VideoGrabber-Setup.exe`;
- `VideoGrabber.Managed-win-x64-<version>.zip`;
- `VideoGrabber-<version>.sha256.txt`.

The manifest must validate both files. `deploy/platform/update_windows_downloads.sh` is intentionally fail-closed and must remain the mechanism that atomically refreshes production download files after verification.

A source-only or empty GitHub Release is not acceptable because it would make the public release surface look newer than the downloadable product.

## 4. Public web/3D evidence

Automated:
- Three.js source/bundle freshness;
- story states and semantic HTML controls;
- reduced-motion fallback;
- mobile overflow guard;
- adaptive quality markers;
- deterministic visual mode;
- public Brotli/Gzip static delivery.

Separate physical-device evidence still required:
- iPhone Safari;
- Android Chromium;
- high-DPR desktop/Retina;
- low/mid-tier mobile GPU.

Use `?perfDebug=1` to read local quality tier, DPR, frame time, LCP, CLS and INP without sending those values to a server.

## 5. Production Web Vitals

Do not label LCP/INP/CLS as production-passing from a local/headless run.

Target references for field data remain:
- LCP <= 2.5 s;
- INP <= 200 ms;
- CLS <= 0.1;
at the 75th percentile, mobile and desktop considered separately.

## 6. Deployment boundary

Code merge, GitHub Release, Windows artifact refresh, stage deployment, production deployment and payment enablement are distinct actions.

No script or CI job in this finalization pass should silently perform production deployment or enable live payments.
