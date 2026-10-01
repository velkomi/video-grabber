# VideoGrabber 3D Finalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finalize VideoGrabber's existing Three.js public site with adaptive GPU quality, richer self-hosted depth, opt-in performance diagnostics, and current release documentation without changing the product architecture.

**Architecture:** Keep the existing plain HTML/CSS/JS public surface, one Three.js renderer and one GSAP ScrollTrigger story stage. Add a small in-scene quality controller and browser-only diagnostics; update repository documentation and release checks separately.

**Tech Stack:** ASP.NET Core static assets, JavaScript, Three.js 0.186.1, GSAP 3.15.0, xUnit, Bash/PowerShell release scripts.

**Spec:** `docs/superpowers/specs/2026-10-01-videograbber-3d-finalization-design.md`

## Global Constraints

- No React migration.
- One Three.js renderer.
- Normal HTML remains authoritative for actions/accessibility.
- No third-party runtime CDN.
- Reduced-motion keeps static fallback.
- No release/public-payment claims without release-acceptance evidence.
- No fabricated physical-device or field-Web-Vitals PASS.

## Review Focus

1. Slow mobile GPU: scene should degrade DPR/decorations, not lose controls.
2. Fast/slow oscillation: hysteresis must prevent constant quality flapping.
3. Reduced motion: realtime runtime remains disabled in normal reduced-motion use.
4. WebGL context/lifecycle: adaptive controller must not continue after page hide/context loss.
5. Diagnostics privacy: `perfDebug` must not transmit performance data.

---

### Task 1: Pin finalization behavior in regression tests

**Files:**
- Modify: `tests/VideoGrabber.Platform.Tests/WebPlanUxTests.cs`
- Modify: `deploy/platform/verify_public_surface.sh`

**Interfaces:**
- Consumes: current web asset filenames.
- Produces: regression requirements for adaptive quality, richer environment and perf diagnostics.

- [ ] Add assertions for `data-quality`, quality tier constants, frame-time sampling, `perfDebug`, browser vitals observers and procedural environment markers.
- [ ] Run the targeted Platform test and verify RED before production code changes.
- [ ] Commit test-only RED state.

### Task 2: Adaptive quality and richer environment

**Files:**
- Modify: `src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js`
- Modify: `src/VideoGrabber.Platform.Api/wwwroot/web/styles.css`
- Regenerate: `src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.bundle.js`

**Interfaces:**
- Consumes: existing `visualTestMode`, `narrowViewport`, renderer lifecycle and story artifacts.
- Produces: canvas datasets `quality`, `dpr`, `frameMs`; deterministic `high|balanced|economy` tiers.

- [ ] Implement quality tiers and sustained frame-time hysteresis.
- [ ] Apply tier to DPR and decorative layers; preserve semantic geometry.
- [ ] Add procedural environment reflection texture and soft nebula sprites.
- [ ] Improve local display font stack in CSS.
- [ ] Rebuild the Three.js production bundle and verify source hash.
- [ ] Run targeted tests and verify GREEN.
- [ ] Commit.

### Task 3: Browser-only performance diagnostics

**Files:**
- Modify: `src/VideoGrabber.Platform.Api/wwwroot/web/index.html`
- Modify: `src/VideoGrabber.Platform.Api/wwwroot/web/app.js`
- Modify: `src/VideoGrabber.Platform.Api/wwwroot/web/styles.css`

**Interfaces:**
- Consumes: canvas quality datasets and PerformanceObserver APIs.
- Produces: opt-in `?perfDebug=1` panel and `window.__VG_WEB_VITALS` local state.

- [ ] Add hidden diagnostics markup.
- [ ] Add LCP/CLS/INP collectors with feature detection and no network send.
- [ ] Update panel from local metrics and 3D datasets only in `perfDebug=1`.
- [ ] Run targeted tests and verify GREEN.
- [ ] Commit.

### Task 4: Current release/documentation checkpoint

**Files:**
- Modify: `docs/UPGRADE_CHECKPOINT.md`
- Modify: `docs/CHANGE_REPORT.md`
- Create: `docs/platform/2026-10-01-preview60-release-preflight.md`
- Modify: `RELEASE_NOTES.md`

**Interfaces:**
- Consumes: repository VERSION, CI state and release-acceptance contract.
- Produces: current handoff documentation that distinguishes implemented, CI-verified and still-live-gated work.

- [ ] Record preview.60/post-preview.60 state and current 3D architecture.
- [ ] Document why GitHub latest release must not be advanced without official Setup/Managed ZIP/manifest.
- [ ] Record physical-device and production Web Vitals as explicit outstanding evidence, not fake PASS.
- [ ] Run documentation/static regression.
- [ ] Commit.

### Task 5: Full branch verification

**Files:**
- No production changes unless verification exposes a regression.

**Interfaces:**
- Consumes: all prior tasks.
- Produces: green CI evidence and review findings.

- [ ] Open PR against `main`.
- [ ] Run branch/PR GitHub Actions.
- [ ] Review CI job output and fix any Critical/Important failures.
- [ ] Confirm public-source checks do not introduce external runtime dependencies.
- [ ] Leave production deployment/release publication unperformed unless official release assets and manual evidence exist.
