# Client updates and service migration Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for independent protocol/publisher tasks; root implements Infrastructure and WinUI integration. Execute within existing isolated worktree, preserve all other changes.

**Goal:** Signed update notifications, verified manual installer download, and safe website/API migration retaining account data.

**Architecture:** Shared contracts/Core verification, offline CLI publisher + API file routes, anonymous client fetch/cache/downloader, existing WinUI notices and Information card.

**Tech Stack:** .NET10 built-in RSA/HttpClient, existing WinUI/NSIS. No new dependency, DB schema or actual domain switch.

**Spec:** docs/superpowers/specs/2026-10-09-client-updates-design.md

## Global constraints

Use exact contract/schema and bounds from spec. First implementation downloads but does not automatically execute installers. Root owns source/version/release; delegates never commit, push, generate production secrets or deploy. Keep tokens/keys out of logs/Git. Existing session/lease/queue storage preserved. Tests use only ephemeral signing keys, fake HTTP, isolated folders.

## Review focus

No secret-bearing client follows a network-supplied origin without verified same-realm metadata. Expired active config blocks managed online requests without deleting tokens; old host disappearance has mirror discovery. Same sequence cannot substitute payload. Current downloads survive update checks/migration notice. Failed/truncated/oversize downloads never expose executable. A cached installer is rechecked before reuse. Clock/URI/semver malformed input fails closed, silence automatic network failures.

### Task1 — protocol implementer

Files: Contracts/ClientUpdates/ClientReleaseContracts.cs; Core/ClientUpdates/{ClientReleaseVersion,ClientReleaseVerifier,ClientReleaseSigner,ClientReleaseTrust}.cs; tests/Core.Tests/ClientReleaseProtocolTests.cs. Public APIs: `ClientReleaseVersion.Parse(string)`, IComparable; `ClientReleaseVerifier(keys,realm,channel,clock).Verify(SignedClientRelease,long minimumSequence=0) -> ClientReleaseManifest`; `ClientReleaseSigner.Sign(manifest,RSA,keyId) -> SignedClientRelease`. Exception `ClientReleaseRejectedException(string Code)`. Serializer shared Web JSON options and raw payload helpers. Trust initially no fake production key; root installs real public key before package. RED→GREEN pure crypto/policy tests.

### Task2 — publisher/API implementer

Files: tools/VideoGrabber.ClientReleasePublisher/*; Api/ClientUpdates/ClientReleaseEndpoints.cs; tests/Platform.Tests/ClientReleaseEndpointTests.cs + publisher checks; root registers routes/solution. Consume Task1 fixed contract. Publisher commands `generate-key --private-key PATH --public-key PATH --key-id ID`, `sign --input DRAFT.json --private-key PATH --key-id ID --output envelope.json`; never output private bytes. Private file owner-only on Unix; production generation root-owned. Signed draft validated before output, atomic creation. API serves `/v1/client-release/preview` file `VG_CLIENT_RELEASE_MANIFEST_PATH` default downloads/client-release.json, and immutable setup in downloads/releases/VERSION/VideoGrabber-Setup.exe. Tests fake files on isolated fixture; no production DB/payment/message. No own Program.cs mutation except communicate exact route registration.

### Task3 — root Infrastructure

Files: Infrastructure/ClientUpdates/{ClientReleaseClient,ClientReleaseCache,VerifiedInstallerDownloader}.cs; Infrastructure.Tests/ClientUpdate*Tests.cs. Fix anonymous transport bounds/redirects, cache sequence + active/pending config, no raw logging. Downloader .part atomic verification, no execution. Read caches before managed client; fail closed expired endpoints but preserve account. Meaningful mocked HTTP and cancellation RED→GREEN.

### Task4 — root WinUI

Files: App/MainWindow.ClientUpdates.cs, Managed/Information/FeatureUx/xaml.cs. Global Studio notice and Information check/status/download/migration actions. Timer24h + startup; QA skips network. Separate immutable active API/website on startup; no live client retarget. UI fixtures isolate state. Build and render public copy, no active session leakage.

### Task5 — integration/review/release

- [ ] Fresh independent task reviews and whole feature review, fix all material findings.
- [ ] Tests and Local/Managed builds, signed live manifest proof after owner-authorized publication.
- [ ] Protected signing key + public pin, release metadata/schema, immutable current/rollback artifacts, mirror.
- [ ] Package from committed source, GitHub/VPS/Windows verification within authorized scope; UAC human handoff where needed; no silent install or new domain.
