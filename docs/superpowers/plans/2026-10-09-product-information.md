# Product information implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for independent tasks and final review.

**Goal:** Complete useful information and usage safeguards across VideoGrabber surfaces without hosting/payment/AI expansion.
**Architecture:** Public static information center + server-authoritative version catalog/ledger, linked from current Studio web, native Windows and Telegram. Existing storage/account/payment behavior remains intact.
**Tech stack:** Existing .NET10/PostgreSQL/WinUI/vanilla HTML/CSS/JS; no new product dependency.
**Spec:** docs/superpowers/specs/2026-10-09-product-information.md

## Tasks
- [ ] 1. Backend document catalog and isolated acceptance ledger/migration066, tests for stale/unknown/auth/dedup/revocation and minimal data. Owner readiness checklist and optional annual revenue audit guidance without inventing NPD totals.
- [ ] 2. Shared information pages and Web/Mini App links/course-rights UI. Accessible responsive pages in existing design. Tests verify paths, safe text/config, errors and unchecked rights before submission.
- [ ] 3. Runtime license inventory/notice package and provenance findings; verify exact binaries and upstream terms, avoid unsupported compliance claims.
- [ ] 4. Native Windows links/rights confirmation, Telegram contacts/doc buttons/commands; avoid long technical customer prose. Retrieve actual owner website from allowed profile/history.
- [ ] 5. Merge/review, meaningful builds/tests/browser evidence; publish/deploy safely where ready, preserve account/old files and report remaining external gates.

## Review focus
- Document hash/version cannot be supplied or substituted by client.
- Unknown operator/hosting facts cannot turn into compliance claims.
- Browser and local/native course confirmation cannot persist approval across distinct operations automatically.
- Legal links still work after signed service-directory migration.
- License inventory cannot call incomplete source provenance verified redistribution.
