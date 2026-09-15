# VideoGrabber Payment Adapters, Refunds and Recurring Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement fully exercised Stars and YooKassa sandbox adapters with immutable payment accounting, refunds, reconciliation and cancellable recurring access.

**Architecture:** Keep provider HTTP/verification in adapters and one shared transactional payment projection that issues grants. Treat webhooks as inputs to verified server state; use provider transaction IDs plus environment as durable idempotency boundaries. Payment features remain disabled for live accounts until their external qualification gates pass.

**Tech Stack:** .NET 10 typed HttpClient, System.Text.Json, PostgreSQL integer accounting, existing Telegram adapter and xUnit; no new SDK package.

**Spec:** `docs/superpowers/specs/2026-09-15-videograbber-platform-auth-licensing-design.md`; program/rulings: `docs/superpowers/plans/2026-09-15-videograbber-platform-program.md`.

## Global Constraints

- Authentication providers: Google, Apple, Yandex, Telegram, plus verified email as recovery/fallback.
- One VideoGrabber account may have several linked identities.
- Guest accounts remain guests until a paid entitlement exists; gifts do not change the role to purchaser.
- Owner/Admin has permanent unlimited usage and does not consume download credits.
- Guest Windows device limit: 1 active computer.
- Admin Windows device limit: 3 active computers.
- Offline grace after a successful online license check: 24 hours for Guest/User, 72 hours for Admin.
- Download-credit spending requires an online reservation; offline clients may use time-based access but may not spend shared credits.
- Existing stable/local releases remain separate from the managed edition; no attempt is made to retroactively lock old binaries.
- No Supabase service-role key or payment secret is embedded in the desktop application, bot Mini App or public repository.
- Access/refresh tokens, OAuth codes, Telegram login signatures, cookies and signed media URLs are redacted from logs.
- Linking a new identity requires proof of control of both the existing account session and the new provider identity. Accounts must not auto-merge merely because provider emails match.
- Do not attempt DRM, CAPTCHA, paywall or account-control bypass.
- Do not delete existing user media when access expires.
- Existing repository baseline: SDK `10.0.400`, C# `14.0`, `net10.0`; WinUI target `net10.0-windows10.0.19041.0`, `win-x64`; nullable, warnings-as-errors and package locks remain enabled.
- Apply all Rulings in `2026-09-15-videograbber-platform-program.md`. Implement only the files assigned to the current task; audit fixes in the shared checkout may be in flight.
- Real credentials, provisioning, external messages, production migration, publication and payments require their separately authorized live gate. Local emulators and disposable test data require no production access.

---

## Increment and prerequisites

P2/P4/P5 gift→shared spend→private result acceptance must pass before enabling payments. Covers VG-GAP-018/019/020. External adapter choice is **YooKassa**, implemented against its documented v3 sandbox; this is a concrete code choice, not merchant enrollment or live sales approval. Telegram digital-service purchase UI uses only Stars; no external payment escape button. No real payment, refund, subscription or credential is used during local implementation.

Local product catalog is explicitly synthetic: `test.credits3` = 3 credits, 30 XTR or RUB 30.00; `test.time30` = 30 days, 100 XTR or RUB 100.00. These prices are test fixtures, not final product content. Live catalog is absent and startup rejects live selling without a separately versioned approved catalog and merchant/legal setup; no invented live tariff.


### Task 1: Catalog, payment journal and one atomic grant projection

**Files — Create:** `src/VideoGrabber.Platform.Contracts/PaymentContracts.cs`; `src/VideoGrabber.Platform.Core/Payments/PaymentProjection.cs`; `src/VideoGrabber.Platform.Persistence/PaymentStore.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/040_payments.sql`; `src/VideoGrabber.Platform.Api/Payments/PaymentEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/PaymentProjectionTests.cs`; `tests/VideoGrabber.Platform.Tests/PaymentRaceTests.cs`; `tests/VideoGrabber.Platform.Tests/Fixtures/payment-catalog.test.json`. Modify Program.cs.

**Interfaces:**
```csharp
public sealed record Money(long MinorUnits, string Currency);
public sealed record PurchaseRequest(string Sku, string Provider, Guid IdempotencyKey, bool Recurring);
public sealed record PaymentCheckout(Guid PaymentId, string State, Uri? RedirectUri, string? InvoicePayload);
public sealed record VerifiedPayment(string Provider, string Environment, string ProviderPaymentId,
    Guid PaymentId, Guid AccountId, Money Amount, string Status, DateTimeOffset OccurredAt,
    string? SubscriptionId, DateTimeOffset? PaidThrough);
public sealed record RefundRequest(Guid PaymentId, Guid IdempotencyKey, string Reason);
public sealed record PaymentView(Guid PaymentId, string Status, Money Amount, string Sku);
public interface IPaymentAdapter
{
 Task<PaymentCheckout> CreateAsync(Guid accountId, PurchaseRequest request, CancellationToken cancellationToken);
 Task<VerifiedPayment> ReadVerifiedAsync(string providerPaymentId, CancellationToken cancellationToken);
 Task<string> RefundAsync(RefundRequest request, CancellationToken cancellationToken);
}
```
PaymentStore `ApplyAsync(VerifiedPayment,CancellationToken)`→Task<PaymentView>. Add PaymentStore `ReadAsync(Guid accountId,Guid paymentId,CancellationToken)` for owner-scoped status. Client paid flags have no route.

- [ ] **RED — Add 10-way confirmed payment replay and webhook/reconcile race.**
```csharp
[Fact]
public async Task One_verified_charge_creates_one_purchase_grant()
{
    await using var f = await ApiFixture.StartAsync();
    var user = await f.AccountAsync("telegram", "payer");
    var store = new PaymentStore(f.Database);
    var checkoutResponse = await user.Client.PostAsJsonAsync("/v1/payments",
        new PurchaseRequest("test.credits3", "stars", Guid.NewGuid(), false));
    var checkout = await checkoutResponse.Content.ReadFromJsonAsync<PaymentCheckout>();
    var confirmed = new VerifiedPayment("stars", "test", "charge-1", checkout!.PaymentId,
        user.Id, new Money(30, "XTR"), "succeeded", f.Clock.GetUtcNow(), null, null);
    await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.ApplyAsync(confirmed, CancellationToken.None)));
    var access = await user.Client.GetFromJsonAsync<AccessSnapshot>("/v1/access");
    Assert.Equal(3, access!.RemainingDownloads);
}
```
Also wrong currency/amount/SKU/account/environment, negative/overflow money, same key altered body, unknown invoice, pending/precheckout no grant, rollback between payment and grant, replay after refund cannot regrant.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter "FullyQualifiedName~PaymentProjectionTests|FullyQualifiedName~PaymentRaceTests"`.
- [ ] **GREEN — Append provider events; use durable unique charge/environment and one grant key.**
```sql
create unique index payment_provider_charge
on licensing.payments(provider,environment,provider_payment_id)
where provider_payment_id is not null;
create unique index purchase_grant_once
on licensing.entitlement_grants(source_reference)
where source='purchase';
```
Payment rows hold immutable merchant/account/catalog version/amount/currency/environment intent; payment_events append verified transitions; mutable projection status is rebuildable. Lock payment/account rows, validate original catalog/order against provider truth, append event and create source=purchase grant once, ledger addition and first_purchase_at/base_role update together. Never use decimal binary floating point; YooKassa decimal strings convert to exact integer kopecks with invariant parsing.
- [ ] **GREEN run:** suites; actual PostgreSQL race with 100 combined verified callbacks/reconciliation attempts and inspect single grant/history.
- [ ] **Commit:** exact files; `git commit -m "feat: project verified payments into access once"`.

### Task 2: Telegram Stars purchase, support and refund adapter

**Files — Create:** `src/VideoGrabber.Platform.Api/Payments/StarsPaymentAdapter.cs`; `StarsUpdateHandler.cs`; `PaymentReconciliationWorker.cs`; `tests/VideoGrabber.Platform.Tests/StarsPaymentTests.cs`. Modify BotApiClient.cs, BotCommandHandler.cs, TelegramInboxWorker.cs, Mini App purchase screen and parity.csv.

**Interfaces:** IBotApiClient adds `CreateInvoiceAsync(long payerId,string payload,string title,Money amount,int? subscriptionPeriod,CancellationToken)`→Task<Uri>; `AnswerPreCheckoutAsync(string queryId,bool accepted,string? error,CancellationToken)`→Task; `RefundStarsAsync(long payerId,string telegramChargeId,CancellationToken)`→Task<bool>; `ReadStarTransactionsAsync(int offset,int limit,CancellationToken)`→Task<JsonElement>. StarsUpdateHandler `HandleAsync(TelegramUpdate,CancellationToken)`→Task. Never substitute TDLib/MTProto method names for Bot API calls.

- [ ] **RED — Test real JSON payloads via emulator.**
```csharp
[Fact]
public void Precheckout_does_not_mean_payment_succeeded()
{
    var update = JsonSerializer.SerializeToElement(new {
        update_id = 10, pre_checkout_query = new {
            id = "query", currency = "XTR", total_amount = 30, invoice_payload = "opaque-invoice" } });
    Assert.False(StarsUpdateHandler.ContainsSuccessfulPayment(update));
}
```
Add successful_payment payer mismatch, duplicate update/charge, valid XTR catalog amount, non-XTR rejection, refunds after credits spent/held/expired, lost refund ACK, paysupport response and absent bot secret.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~StarsPaymentTests`.
- [ ] **GREEN — Implement invoice payload lookup and exact transport verification.**
```csharp
public static bool ContainsSuccessfulPayment(JsonElement update) =>
    update.TryGetProperty("message", out var message) &&
    message.TryGetProperty("successful_payment", out _);
```
Invoice payload is a random opaque order handle, bound to account/SKU/environment/payer; no client price in final API call. Use Bot API createInvoiceLink/sendInvoice with XTR and empty provider_token; pre-checkout validates inventory/amount/payer and responds within Telegram deadline, without granting. Accept successful_payment only through authenticated durable webhook or reconciled provider transaction; persist telegram_payment_charge_id, payer, amount, recurring flags and paid-through data. /paysupport returns working support instructions and payment reference, with private account lookup only.

Refund command requires admin MFA and reason; write refund intent before calling refundStarPayment, keep same charge reference, mark uncertain result and reconcile; grant adjustment follows verified refund only. Spent value remains journal history; unused availability revoked, holds follow R029; no negative spendable credit. Dashboard displays refund/reconciliation state.
- [ ] **GREEN run:** local tests and emulator invoice→precheckout→success→desktop profile→refund. Live Stars test environment is a separate authorized gate; never send paid invoice to an ordinary user during implementation.
- [ ] **Commit:** exact files; `git commit -m "feat: handle Stars purchases refunds and support"`.

### Task 3: YooKassa external sandbox adapter with verified provider reads

**Files — Create:** `src/VideoGrabber.Platform.Api/Payments/YooKassaPaymentAdapter.cs`; `YooKassaWebhookEndpoints.cs`; `tests/VideoGrabber.Platform.Tests/YooKassaPaymentTests.cs`; `tests/VideoGrabber.Platform.Tests/YooKassaEmulator.cs`; `docs/platform/yookassa-sandbox-qualification.md`. Modify PaymentEndpoints.cs and desktop account purchase link.

**Interfaces:** YooKassaPaymentAdapter implements IPaymentAdapter using named HttpClient with fixed HTTPS https://api.yookassa.ru/v3/; test adapter base URI allowed only in test host. Server stores shop credentials in protected environment/mounted secret, never desktop. Webhook route only consumes provider object ID then calls ReadVerifiedAsync; it does not claim a nonexistent provider HMAC signature.

- [ ] **RED — Mock callback says succeeded but provider says pending; assert no grant.**
```csharp
[Fact]
public void Exact_money_conversion_does_not_round_kopecks()
{
    Assert.Equal(3001, YooKassaPaymentAdapter.ParseRub("30.01").MinorUnits);
    Assert.Throws<FormatException>(() => YooKassaPaymentAdapter.ParseRub("30.001"));
    Assert.Throws<FormatException>(() => YooKassaPaymentAdapter.ParseRub("-1.00"));
}
```
Add spoofed callback, unknown payment ID, merchant/metadata/account/amount mismatch, test=false in test environment, replay, provider 429/timeout, duplicate create Idempotence-Key, refund race and return URL forgery. Emulator records actual HTTP headers/body paths without storing Basic auth values in evidence.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~YooKassaPaymentTests`.
- [ ] **GREEN — Create payment using server catalog and stable UUID Idempotence-Key; verify each notification by GET /payments/{id}.**
```csharp
public static Money ParseRub(string value)
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[0-9]+\.[0-9]{2}$"))
        throw new FormatException("invalid_money");
    var parts = value.Split('.');
    var minor = checked(long.Parse(parts[0], CultureInfo.InvariantCulture) * 100
        + long.Parse(parts[1], CultureInfo.InvariantCulture));
    return new Money(minor, "RUB");
}
```
Send capture=true, amount.value invariant decimal string, currency RUB, confirmation redirect return URI from exact allowlist, metadata opaque order ID, description from approved catalog. Authenticated GET validates paid/status/test/recipient/account mapping before PaymentStore.ApplyAsync. Do not download arbitrary object URL from webhook body; compose path from validated provider ID. Callback IP allowlist may be defense-in-depth but does not replace provider read. Provider 503 leaves pending verification with retry 5s/30s/120s then scheduled reconcile; user browser returning “success” only polls own payment status.
- [ ] **GREEN run:** full adapter emulator flow; sandbox qualification with test shop only after authorized secret injection, record sanitized test=true/amount/status/idempotency proof. No external purchase UI inside Telegram.
- [ ] **Commit:** exact files; `git commit -m "feat: verify external sandbox payments with YooKassa"`.

### Task 4: Recurring periods, cancel, refund/chargeback and convergence

**Files — Create:** `src/VideoGrabber.Platform.Contracts/SubscriptionContracts.cs`; `src/VideoGrabber.Platform.Persistence/SubscriptionStore.cs`; `src/VideoGrabber.Platform.Api/Payments/SubscriptionService.cs`; `src/VideoGrabber.Platform.Persistence/Migrations/041_subscriptions_refunds.sql`; `tests/VideoGrabber.Platform.Tests/SubscriptionRaceTests.cs`; `docs/platform/payment-support-runbook.md`. Modify Stars/YooKassa adapters, admin payments view and both client account screens.

**Interfaces:**
```csharp
public sealed record SubscriptionView(Guid SubscriptionId, Guid AccountId, string State,
    bool AutoRenew, DateTimeOffset PaidThrough, string Provider);
public sealed record RenewalEvent(string Provider, string Environment, string ChargeId,
    Guid SubscriptionId, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, Money Amount);
```
SubscriptionStore `ApplyRenewalAsync(RenewalEvent,CancellationToken)`→Task<SubscriptionView>; SubscriptionService `CancelAsync(Guid accountId,Guid subscriptionId,Guid idempotencyKey,CancellationToken)`→Task<SubscriptionView>. Add IBotApiClient `SetStarSubscriptionCanceledAsync(long payerId,string firstChargeId,bool canceled,CancellationToken)`→Task<bool> using Bot API editUserStarSubscription.

- [ ] **RED — Test renewal/refund/cancel permutations and same-period duplicates.**
```csharp
[Fact]
public void Cancel_keeps_the_already_paid_period()
{
    var paidThrough = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
    var active = new SubscriptionView(Guid.NewGuid(), Guid.NewGuid(), "active", true, paidThrough, "stars");
    var canceled = SubscriptionService.ApplyCancel(active);
    Assert.False(canceled.AutoRenew);
    Assert.Equal(paidThrough, canceled.PaidThrough);
}
```
Add late previous-period webhook, renewal while cancel in flight, refund before success notification, webhook/reconciliation same charge, duplicate subscriptions from same payer, provider chargeback event/read evidence, and expired pending renewal no extra days.
- [ ] **RED run:** `dotnet test tests/VideoGrabber.Platform.Tests/VideoGrabber.Platform.Tests.csproj --filter FullyQualifiedName~SubscriptionRaceTests`.
- [ ] **GREEN — Separate subscription agreement and verified paid periods.**
```csharp
public static SubscriptionView ApplyCancel(SubscriptionView subscription) =>
    subscription with { AutoRenew = false, State = "canceled" };
```
Stars subscription period is 2592000 seconds (30 days), validate against current Bot API before live use; every confirmed renewal charge is a unique event/grant, paid-through comes from verified provider data. YooKassa recurring scheduler creates a new period order only with recorded explicit consent and saved payment_method_id; unique(subscription_id,period_start) prevents duplicate charges. No retry with a new idempotency key after unknown provider outcome. Cancel first persists requested state and disables scheduler, then confirms provider cancellation/reconciles; paid grant remains until expiry.

Refund/chargeback verified after earlier success append terminal adjustment; replayed success cannot undo them. Chargeback with no supported machine-verifiable provider event enters admin review with documented provider evidence, no invented webhook schema. A later genuine new renewal charge can create its own period, never resurrect refunded prior charge. Reconciliation rebuilds projection from immutable verified events ordered by provider occurrence/period and event precedence, not delivery time; test all permutations. Client view shows AutoRenew=false/pending cancellation/paid-through distinctly.
- [ ] **GREEN run:** suite with real PostgreSQL races and both emulator providers; rebuild projected balances/subscriptions from events and compare exact rows.
- [ ] **Commit:** exact files; `git commit -m "feat: reconcile recurring access and cancellations"`.

## Acceptance and live boundary

All local fake/emulator HTTP contracts, exact money, replay, refund and recurring races pass; P2 ledger conservation survives every case; Telegram purchase appears in desktop on the same account; no client flag grants access. Existing media suites stay green. Repeat all Platform tests and actual account UI purchase/status/cancel controls with emulators.

Live Stars and YooKassa sandbox qualification each have positive, replay, refund and cancel evidence. Live catalog/merchant eligibility, receipts/taxes/support policy and stored-consent text require the real deployment configuration before selling; absent configuration is disabled, not a silent test tariff. This plan does not authorize real money movement.

Sources: [Stars digital goods](https://core.telegram.org/bots/payments-stars), [Bot API subscriptions/refunds](https://core.telegram.org/bots/api), [YooKassa notifications](https://yookassa.ru/developers/using-api/webhooks), [YooKassa recurring payments](https://yookassa.ru/developers/payment-acceptance/scenario-extensions/recurring-payments/pay-with-saved).
