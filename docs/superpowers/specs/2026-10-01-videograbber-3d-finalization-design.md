# VideoGrabber 3D Finalization Design

Date: 2026-10-01

## Goal

Finish the public VideoGrabber site without replacing its current HTML/CSS/JS architecture: keep the existing single Three.js renderer and GSAP story flow, improve visual depth and real-device resilience, add deterministic performance observability, refresh release/deployment documentation, and leave explicit gates for anything that cannot be honestly verified without production secrets or physical devices.

## Constraints

- Do not migrate the public site to React.
- Keep one Three.js renderer for the story states `hero → workflow → sync → pricing → windows`.
- Keep all primary actions as normal HTML controls; 3D remains progressive enhancement.
- Keep the approved static hero as fallback and do not load realtime 3D when `prefers-reduced-motion: reduce` is active.
- Keep all WebGL/runtime dependencies self-hosted.
- Do not add a second animation system that competes with GSAP.
- Do not publish or enable a production payment/release state without the repository release-acceptance evidence.
- Do not claim physical-device or production Web Vitals verification unless it has actually been collected.

## 1. Adaptive Three.js quality

The scene should begin at a conservative quality tier derived from viewport/device capability and then use measured frame time with hysteresis to adjust rendering cost. The controller changes pixel ratio and decorative depth layers only; geometry semantics, links, copy, story states and interaction remain unchanged.

Quality tiers:
- high: desktop cap 1.5 DPR, mobile cap 1.1 DPR; full stars/particles/atmosphere;
- balanced: desktop cap 1.25 DPR, mobile cap 0.95 DPR; reduced decorative opacity;
- economy: desktop cap 1.0 DPR, mobile cap 0.8 DPR; depth stars and nonessential particles disabled.

The controller may step down only after a sustained slow window and step up only after a substantially longer fast window. Visual-test and reduced-motion modes remain deterministic/static and do not auto-tune.

The canvas exposes `data-quality`, `data-dpr`, and `data-frame-ms` for browser QA. No telemetry is sent to a server.

## 2. Richer visual environment

Keep the current planet, orbit and semantic card geometry. Add a low-cost procedural environment/reflection texture and a small set of soft nebula sprites so physical materials read with more depth without requiring a runtime CDN or a Blender dependency.

These additions are decorative and must be removable in economy quality. They must be deterministic in visual-test mode.

A future bespoke Blender GLB remains optional art direction, not a release blocker. The current public contract must not depend on a local Blender installation.

## 3. Typography

Do not fetch a web font from a third-party CDN. Improve the display stack with local system variable/display faces first, while keeping robust fallbacks. This avoids making page render dependent on a font network request.

## 4. Performance diagnostics

Add an opt-in `?perfDebug=1` diagnostics surface for development/QA. It shows:
- current 3D quality tier,
- effective DPR,
- smoothed frame time,
- LCP when available,
- CLS,
- INP when available.

Metrics stay in the browser only. Normal visitors do not see the panel and no analytics endpoint is added.

## 5. Release and documentation hygiene

Update the project checkpoint/change report to reflect preview.60 and the post-preview.60 Three.js work. Add a release-preflight document/checklist that explicitly distinguishes:
- green CI,
- release-acceptance evidence,
- GitHub release assets,
- production deployment,
- physical-device validation.

Do not create a misleading GitHub Release without the official bundled Windows artifacts and manifest required by `update_windows_downloads.sh`.

## 6. Verification

Automated regression must pin:
- adaptive-quality markers and tier values;
- no external Three.js/environment URL;
- `perfDebug` browser-only diagnostics;
- fallback/reduced-motion behavior;
- public-surface script markers;
- updated checkpoint/release documentation.

CI must remain green on the branch before integration.
