# Referral and promotion implementation plan

> **For agentic workers:** use superpowers:subagent-driven-development for the independent storage and UI tasks; root implements policy/payment integration. Steps are tracked here and in the ledger.

**Goal:** Shared-account referrals, monetary bonus balance and promo codes with verified payment/refund handling.
**Architecture:** Deterministic Core policy, PostgreSQL PromotionStore, immutable quotes and bonus events, existing provider adapters. Feature disabled by default until separately reviewed production activation.
**Tech Stack:** .NET 10, Npgsql/PostgreSQL, existing HTML/JS/CSS and WinUI. No new dependency.
**Spec:** `docs/superpowers/specs/2026-10-08-referrals-design.md`.

## Global constraints

10% friend / 10% referrer; hold 14 days; validity 365 days; max combined discount 30%; max public coupon 20%; no cash withdrawal; RUB/XTR separate. First qualifying payment only; do not award on free registration, credit top-up or renewal. Do not apply one-time promotions to recurring Stars invoice. No production changes or real messages/payments during acceptance.

## Review focus

Replay/concurrency cannot create money. Current catalog updates cannot alter stored invoice terms. Full/partial refund and spent/expired reward must remain auditable. Linking/merging accounts cannot duplicate eligibility or lose balances. Disabling new promotions does not stop settlement of existing invoices.

## Task 1 вЂ” root: contracts and policy

Files: `Platform.Contracts/PromotionContracts.cs`, `Platform.Core/Payments/PromotionPolicy.cs`, `tests/VideoGrabber.Platform.Tests/PromotionPolicyTests.cs`.
- [x] RED: price 150000 RUB, referral first payment, no wallet -> payable 135000; referrer reward 13500.
- [x] Cover coupon best-of, combined cap, XTR integer rounding, overflow/invalid input, recurring restriction.
- [x] Implement deterministic calculation and run isolated policy tests.

## Task 2 вЂ” storage implementer

Files: new partial `PromotionStore.*.cs`, migration `064_referrals_promotions.sql`, `PromotionStoreTests.cs`.
Constructor `(CreditLedger ledger, TimeProvider clock, PromotionOptions options)`. Public: `SummaryAsync(accountId,ct)`, `ClaimAsync(accountId,code,ct)`, `QuoteAsync(accountId,request,CatalogProduct,catalogVersion,ct)`, `SavePromoAsync(actorId,PromoDefinition,ct)`, `ListPromosAsync(ct)`.
Payment hooks below take `NpgsqlConnection c, NpgsqlTransaction tx` and must not commit independently:
- `PreparePaymentAsync(c,tx,accountId,paymentId,PurchaseRequest,CatalogProduct,ct) -> Task<Money>` verifies/consumes quote, freezes attributed referrer, reserves promo/wallet; called after payer account lock and before payment insert. Quote consumption is idempotent through PaymentStore idempotency before this hook.
- `LockPaymentAccountsAsync(c,tx,accountId,paymentId,ct)` locks payer + attributed referrer in sorted order using existing hashtextextended account key 20260918.
- `SuccessAsync(c,tx,VerifiedPayment,CatalogProduct,bool firstPurchase,ct)` commits reservations and issues one pending reward only on actual first success.
- `CancelAsync(c,tx,paymentId,ct)` releases reserved promo/wallet.
- `RefundAsync(c,tx,paymentId,string refundKey,long cumulativeRefundedMinor,long originalPaidMinor,bool fullRefund,ct)` reduces reward to 10% remaining external cash, restores payer bonuses on full refund, idempotent and atomic. Caller validates provider proof and serializes payment.
- [x] RED -> GREEN storage integration: self claim, double claim, parallel last coupon use, wallet conservation, 14-day maturity, 365-day expiry, late refund debt, repeated/full/partial refund and linked account attribution.
Schema in existing licensing: referral_codes/referrals/promo_codes/promotion_quotes/promotion_payments/bonus_lots/bonus_events/bonus_allocations. Read/write only vg_ledger, metadata needed by vg_admin/identity carefully scoped; no public permissions. Money uses bigint minor units, percent basis points and checked arithmetic.
Lots keep remaining (may be negative for a clawback) and reserved counters, maturity/expiry timestamps. Active balance sums eligible unspent minus clawback debt; pending funds are unavailable. Offset negative lots from eligible positive lots under the same account lock with balanced immutable offset events, so already-settled debt cannot reappear when the offsetting lot expires. Expired positive funds cannot be spent or used to offset new debt; reserved invoices remain valid until settlement.

## Task 3 вЂ” root: payments/API/tests

Files: `PaymentStore.cs`, both adapters, webhook, `PaymentProjection.cs`, `PaymentContracts.cs`, `Program.cs`, `Api/Promotions/PromotionEndpoints.cs`, account merge guards, new integration tests.
- [x] Add optional QuoteId to PurchaseRequest; freeze expected amount and product snapshot; preserve old request hash for absent QuoteId.
- [x] Adapters issue/read invoices against stored expected amount. Verified success is not catalog-repriced. Introduce authenticated provider refund lookup and exact refund-ID dedup/cumulative refund tracking.
- [x] Endpoints: GET `/v1/referrals`; POST `/v1/referrals/claim` `{code}`; POST `/v1/promotions/quote` PromotionQuoteRequest; GET/POST `/v1/admin/promotions` owner + MFA for writes.
- [x] Feature disabled -> 503 for new public marketing calls; already-created financial events still settle.
- [x] Enable in isolated fixture; test quote tampering, stale quote, changed price, real adapter invoice amounts, no reward on precheckout/renewal, refunds, feature disabling and merge/link.

## Task 4 вЂ” UI implementer

Files: Web/Mini App JS/HTML/CSS, new common `wwwroot/assets/promotions.js`, WinUI account card, BotCommandHandler/configure_bot, frontend/bot tests. Do not modify payment/storage/Core files.
- [x] Add compact Studio account section and buttons, accessible validation/errors, quote price breakdown; retain menus and style.
- [x] Capture `?ref=CODE` and Telegram `ref_CODE` private start, claim via same account. Pending guest intent retained by Store across linking.
- [x] Public DTOs and endpoints below are authoritative; payment request includes QuoteId. Hide/disable marketing cleanly on 503; no fake balances/working codes.
- [x] Windows card queries authenticated GET `/v1/referrals`, shows invite/balances and opens site. Bot uses PromotionStore summary/claim and website/Mini App for promo checkout.
- [x] Test actual JS controller with mocked HTTP, linked/disabled/empty/pending states and safe DOM; build WinUI and visualize.

## Task 5 вЂ” combine and review

- [x] Fresh reviewers evaluate each delegated task against spec; root resolves findings with regression tests.
- [x] Full Platform, relevant Core/Infrastructure/Worker and frontend tests, builds, isolated UI/E2E, secret/diff checks.
- [x] Final independent branch review; fix all material issues. Produce verified source/package/activation proposal; do not mark production active without migration/activation evidence.

## Acceptance boundary

Implementation, independent review, Windows ZIP/installer and isolated checks completed; see artifacts/referral-research-20261008/ACCEPTANCE.md. Production publication/activation and fresh final WinUI rendering remain pending. Automatic approval blocked launching the final QA executable; no bypass. The installer payload passed a separate archive-integrity check; it was not installed.
