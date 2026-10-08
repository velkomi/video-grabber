# VideoGrabber Studio — selected visual direction

The owner selected displayed concept 2 on 2026-10-07. Reference: `C:/Users/Oleg/Documents/Codex/2026-10-07/vps-hostinger-github-video-grabber/outputs/concept-02.png`.

Preserve the existing HTML/CSS/JS, Three.js 0.186.1 and GSAP 3.15.0 architecture. Use a navy studio composition, optical-glass/metal play sculpture, a flowing ring, four front-facing readable plan slabs, original plan emblems and studio pedestals. Keep all existing prices and backend behavior. Web, account and Windows sections stay functional production surfaces; this change does not configure authentication or billing.

## Tokens and structure

- Canvas: `#060b17` / `#07101f`; foreground `#f7f9ff`; body text `#a9b7d3`; accent `#61a8ff`.
- Primary CTA: white text over `#1675cf` → `#1b57c8`; minimum calculated endpoint contrast 4.6879:1.
- Type: existing local Segoe UI Variable Display/Text and system fallbacks, with full Cyrillic coverage. No font CDN or global font installation.
- Desktop content width 1200 px; hero copy and scene occupy separate columns. At ≤1050 px the page becomes a single hero column and pricing becomes two columns; at ≤720 px pricing becomes one column.
- The scene occupies explicit reserved slots. Document coordinates prevent a fixed canvas from overlapping unrelated content while scrolling.
- Motion owns camera/objects inside one renderer; GSAP owns the shared DOM stage transition. Native scroll remains available.

## Implementation sequence

1. Restore pricing structure; add a regression for mobile-to-desktop lifecycle and release old triggers.
2. Replace the planet with a bevelled three-dimensional play sculpture and studio PMREM environment.
3. Preserve raycast navigation only for visible meshes; expose three sharp HTML labels for MP4/MP3/Course.
4. Add native 3D device geometry for account synchronization; preserve normal HTML login controls.
5. Apply plan emblems/pedestals and restrained pointer tilt; preserve the complete entitlement descriptions.
6. Cover changing reduced motion, initial idle-import races, context restore and persisted page navigation.
7. Rebuild the pinned bundle; verify syntax, focused regressions, API build and actual browser states.

## Asset provenance

Studio hero, four plan emblems and pedestal were generated with built-in Image Gen, using the selected concept as the visual reference. PNG originals are retained. WebP delivery copies were encoded with the existing FFmpeg executable without changing the composition. Hero delivery is ~217 KB; the complete six-asset WebP set is ~1.1 MB. The hero raster is a fallback; it is not substituted for the live Three.js model.

The existing owner-supplied VideoGrabber logo is retained. Small UI icons are Bootstrap Icons, exact upstream revision and MIT license recorded in `THIRD_PARTY_NOTICES.md` and `wwwroot/assets/icons/LICENSE.txt`.

## Acceptance boundary

Required local checks: desktop/mobile layout, all five story states, repeated breakpoint transitions, four plan dialogs, Escape/focus return, visible-only scene navigation, reduced-motion on/off, source/bundle freshness, focused tests and API Release build. Screenshots and comparison evidence are saved in this chat's outputs directory.

Physical phone/GPU testing, field Web Vitals, the user's failing Windows sign-in, sandbox payments and production acceptance are separate gates. The local static preview has no account/payment backend and does not claim that login or checkout succeeds. Publication, push and production deployment require explicit owner authorization.
