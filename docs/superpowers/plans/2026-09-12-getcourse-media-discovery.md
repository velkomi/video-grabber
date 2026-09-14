# GetCourse Media Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make VideoGrabber reliably play and discover downloadable media on authenticated GetCourse-like lessons such as iglyrazuma.ru while keeping the user's VPN enabled globally.

**Architecture:** Keep the existing loopback SOCKS proxy, but add a session-scoped provider policy for known GetCourse media families so only the active lesson browser uses the selected adapter. Add a DevTools Network observer alongside WebResourceResponseReceived to discover HLS/DASH/MP4/Media requests from nested frames and dynamic players. Persist only explicit root-site rules; dynamic provider-family decisions stay in memory for the active session.

**Tech Stack:** .NET 10, WinUI 3, WebView2, Chrome DevTools Protocol Network domain, yt-dlp, FFmpeg/FFprobe.

**Spec:** User-approved design in chat on 2026-09-12; existing routing plan `docs/plans/2026-09-12-site-routing.md`.

## Global Constraints
- Never disable or reconfigure VPN globally.
- Never log cookies, Authorization headers, signed query values, or full private URLs.
- Do not bypass DRM or authentication.
- Preserve the stable v0.1.9 and all previous preview folders.
- Rules persisted in `site-routes.json` remain exact-host rules only.
- Dynamic media families are session-only and only activated by an explicit routed root lesson.
- No silent fallback to another adapter for an explicitly routed host.
## Task 1: Session-scoped provider routing

**Files:**
- Modify: `src/VideoGrabber.Infrastructure/Networking/SiteRoutePolicy.cs`
- Modify: `src/VideoGrabber.Infrastructure/Networking/SiteRouteProfiles.cs`
- Modify: `src/VideoGrabber.Infrastructure/Networking/RouteConnector.cs`
- Test: `tests/VideoGrabber.Infrastructure.Tests/SiteRouteProfileTests.cs`
- Test: `tests/VideoGrabber.Infrastructure.Tests/SiteRoutePolicyTests.cs`

- [ ] Add failing tests for GetCourse media-family routing and direct-IP requests inside an active routed session.
- [ ] Verify RED.
- [ ] Implement an in-memory session profile for `.getcourse.ru`, `.gcvh.ru`, `.kinescopecdn.net`, `.vhcdn.com` and narrowly justified media CDN families.
- [ ] Route public literal-IP requests via the selected session adapter instead of bypassing the policy.
- [ ] Keep unrelated domains on the system route.
- [ ] Run focused routing tests and full Infrastructure tests.

## Task 2: DevTools media discovery

**Files:**
- Create: `src/VideoGrabber.Infrastructure/Browser/DevToolsMediaEventParser.cs`
- Modify: `src/VideoGrabber.Infrastructure/Browser/MediaCandidate.cs`
- Modify: `src/VideoGrabber.App/MainWindow.Browser.cs`
- Test: `tests/VideoGrabber.Infrastructure.Tests/DevToolsMediaEventParserTests.cs`

- [ ] Add failing tests for `Network.responseReceived` JSON with HLS, DASH, MP4, Media resource type and signed URLs.
- [ ] Verify RED.
- [ ] Enable the CDP `Network` domain and subscribe through `GetDevToolsProtocolEventReceiver`.
- [ ] Parse only URL, MIME, resource type and safe referer context; never persist tokens or headers.
- [ ] Feed candidates through the existing dedupe/list UI.
- [ ] Keep `WebResourceResponseReceived` as a fallback.
## Task 3: Real lesson verification and package

**Files:**
- Modify: `VERSION`
- Modify: `docs/MEDIA_WORKFLOWS.md`
- Evidence: `artifacts/getcourse-media-20260912/*`

- [ ] Run all Core and Infrastructure tests with actual FFmpeg/Whisper integration tools.
- [ ] Build a new preview package without overwriting prior previews.
- [ ] Start the packaged EXE and reopen the authenticated lesson.
- [ ] Verify three observed video cases: playable video, previously-stuck player, previously-cloud-error player.
- [ ] Verify at least one actual media candidate appears in “Найденные видео и потоки”.
- [ ] Start a selected-stream download and confirm progress/output with FFprobe; cancel a redundant full download after proof when appropriate.
- [ ] Record domains used, but redact signed query strings and cookies.
- [ ] Run `git diff --check`, secret scan, final tests, and independent read-only code review.
- [ ] Commit locally only after evidence is complete; do not publish to GitHub unless separately requested.
