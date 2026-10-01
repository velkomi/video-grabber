# VideoGrabber Public Site — Design QA

## Source visual truth

- Approved concept: `C:\Users\Oleg\Downloads\Изображение ChatGPT 30 сент. 2026 г., 09_53_49.png`
- Source pixels: **1122 × 1402**
- Theme/state: dark desktop landing page, signed out
- Approved art direction: glossy dark UI, blue/violet/pink highlights, smooth volumetric 3D hero, compact "How it works", account synchronization block, four simple pricing cards.

## Rendered implementation

- Local final implementation: `D:\CODEX\Temp\vg-obvious3d-final.png`
- Browser: Microsoft Edge / Chromium, headless with WebGL/SwiftShader enabled
- Requested browser window: **1122 × 1402**
- Captured content viewport: **1098 × 1263 CSS px**
- Implementation screenshot pixels: **1098 × 1263**
- Local route: `http://127.0.0.1:8881/web/`
- Scene runtime: self-hosted Three.js **0.186.1**, production tree-shaken bundle built through esbuild **0.28.2**.

## Primary interactions tested

- Signed-out state is visible; authenticated dashboard is hidden.
- All four pricing cards open their own dialog.
- Pricing dialog title and price match the clicked plan.
- Paid-plan action while signed out routes the user to sign-in context.
- Native e-mail validation blocks malformed e-mail before network activity.
- Application-side e-mail validation returns human-readable copy.
- Hero parallax reacts to pointer movement.
- Realtime Three.js geometry initializes successfully on desktop; renderer reports `data-engine="three.js r186"`, canvas is populated and browser console has **0 SEVERE errors**.
- Geometry is real, not a flat SVG/shader illusion: PBR sphere, physically lit torus orbits and extruded rounded feature cards are rendered by Three.js.
- Obvious-depth pass: card Z positions are intentionally staggered, focused cards move ~0.62 world units toward the camera, camera parallax is stronger, and scroll adds a subtle cinematic camera move.
- Planet/orbit motion is deliberately readable: planet ~0.14 rad/s; three orbits rotate at independent stronger rates; back-rim light, rim shell and deterministic depth stars reinforce foreground/background separation.
- The approved 3D raster remains the instant fallback while Three.js and textures load; with reduced-motion the Three.js runtime is not loaded, while normal narrow/mobile viewports keep animation at a reduced ~30 FPS and lower DPR.
- Hero feature interaction is driven by real Three.js raycasting over the six card meshes (URL, Video, MP3, Course, Windows, Telegram). Invisible HTML anchors remain only as keyboard/touch activation fallbacks; no visible rectangular hotspot overlay remains.
- WebGL animation pauses automatically when the hero leaves the viewport or the page is hidden.
- Pointer raycast changes the actual 3D card material/position/accent; keyboard focus on the invisible fallback links drives the same 3D state without drawing a fake focus rectangle around the scene.
- Scroll reveal remains progressive; the old rectangular pointer-sheen layer was removed from sections/cards because its box edges were visible on the approved dark background.
- Reduced-motion was verified in Edge: motion classes stay disabled and the content remains visible.
- Single-stage scroll storytelling was browser-verified through `hero → workflow → sync → pricing → windows`; the stage fades out before the footer and sends a visibility event so rendering can stop.
- Geometric raycast browser test found all six hero cards inside canvas bounds: URL, Video, MP3, Course, Windows and Telegram.
- `?visualTest=1` fixes the story at the hero state and yields a stable GPU visual diff (mean delta well below the acceptance threshold) without relying on byte-identical WebGL screenshots.
- Main anchors and destinations exist: How it works, Download, Pricing, Account, Windows installer, Portable ZIP, Telegram.
- Mobile width smoke test passed without horizontal document overflow; a temporary overflow caused by animated background glows was detected and fixed by clipping the root x-axis.
- Browser console check: **0 SEVERE errors**.
- Quantified obvious-3D check (latest release gate): temporal motion changed ~50% of canvas pixels, pointer parallax ~80%, feature-card focus ~78%; mobile animation remained active and overflow-free.

## Full-view comparison evidence

The approved concept and browser-rendered implementation were inspected together in the same QA pass.

### Earlier P1/P2 findings and fixes

1. **[P1] Residual source-copy artifact inside the hero crop**
   - Earlier implementation showed a faint fragment of the source headline behind the 3D planet.
   - Fix: recropped the approved hero artwork farther right and regenerated `videograbber-hero.webp` with a feathered edge.
   - Post-fix evidence: the final local screenshot no longer shows the stray source headline fragment.

2. **[P1] Sync illustration contained stale mockup UI**
   - A generated synchronization raster showed embedded Google/e-mail controls that looked like real interface buttons.
   - Fix: removed the raster from the production layout entirely. The real Google/e-mail controls remain HTML, while the shared Three.js story scene supplies the sync Web/Windows/Telegram visual state.
   - Post-fix evidence: no `/assets/videograbber-sync.webp` element is present in the public page.

3. **[P2] Vertical rhythm was much looser than the approved concept**
   - Earlier implementation pushed pricing far below the first long desktop view.
   - Fix: reduced hero height/padding, section padding, step icon sizing, synchronization spacing, pricing-card height and feature-list spacing.
   - Post-fix evidence: the final layout is materially denser and follows the approved composition more closely while retaining responsive web spacing.

## Focused region comparison

Focused comparison was necessary for:

- **Hero imagery:** uses the approved raster as fallback/visual continuity, then crossfades to a true standalone Three.js scene with physical materials and real geometry.
- **Synchronization:** no screenshot/mockup is used. The single Three.js renderer transitions into a dedicated Web/Windows/Telegram synchronization state beside the real HTML sign-in controls.
- **Pricing:** retains the approved four-card hierarchy, with Unlimited Video marked `Популярный` and Full Course marked `Для курсов`.
- **Account block:** rewritten to “Вход и синхронизация” and no longer exposes Supabase/Magic Link implementation wording or raw JavaScript errors.

## Required fidelity surfaces

### Fonts and typography

- Large, heavy display hierarchy follows the reference.
- Main headline uses the same compact, high-contrast visual hierarchy.
- Small descriptive text remains readable and less dense than the original generated image.
- Residual difference: exact font family from the generated source is not identifiable; system/Segoe-style fallback is used. This is **P3**.

### Spacing and layout rhythm

- Header, hero, three-step flow, synchronization block and four-column pricing hierarchy match the reference structure.
- Density was tightened in the final pass.
- Residual difference: the responsive production page remains somewhat taller than the single generated reference sheet. This is intentional for readability and responsive behavior and is classified **P3**.

### Colors and visual tokens

- Dark navy base, electric blue/violet accents, pink/gold gradient highlights and bright CTA treatment match the approved direction.
- Featured Unlimited card and course badge preserve the intended hierarchy.

### Image quality and asset fidelity

- The approved hero WebP remains only as an instant fallback/visual continuity asset. Sync, workflow, pricing and Windows visuals are rendered as semantic states of the same Three.js scene rather than separate screenshots.
- Hero uses self-hosted procedural planet diffuse/bump/emissive maps, MeshPhysicalMaterial, three actual torus meshes, extruded rounded cards and a central VG badge.
- Three.js is non-blocking: it is dynamically imported after the approved hero image and idle scheduling; unsupported browsers retain the approved image without losing content or controls.
- Runtime bundle is tree-shaken and source-hash protected so stale 3D bundles are caught by regression tests.
- No visible source-text contamination remains after final recrop.

### Copy and product content

- “Тарифы без скрытых кредитов” removed; heading is simply “Тарифы”.
- “Отправить на мой компьютер” removed.
- Sign-in copy is human-facing and no longer mentions Supabase/Magic Link.
- Raw `Cannot set properties of null` / `TypeError` is not exposed.
- Pricing is organized by scenario and uses positive/negative inclusion markers.

## Remaining P3 polish

- Exact source display font could be matched more closely in a later branding pass.
- Current hero objects are high-quality procedural Three.js geometry. A later art pass can replace selected cards/planet details with bespoke Blender-authored GLB meshes without changing the public UI contract.
- Background cosmic decoration can be made richer with an additional dedicated texture/HDRI layer if desired, without changing layout or UX.
- Final page is intentionally taller than the generated concept sheet on some desktop viewports to preserve readable copy and responsive behavior.

## Final result

**passed**
