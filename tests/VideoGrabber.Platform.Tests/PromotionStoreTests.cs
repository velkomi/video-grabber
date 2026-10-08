using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PromotionStoreTests
{
    [Fact]
    public async Task New_unpaid_primary_signup_can_claim_quote_and_pay_without_role_promotion()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await f.AccountAsync("google", "unpaid-inviter", "unpaid-inviter@example.test");
        var friend = await f.AccountAsync("email", "unpaid-friend", "unpaid-friend@example.test");
        await using (var c = await f.Database.OpenConnectionAsync())
        await using (var check = new NpgsqlCommand("select count(*) from licensing.accounts where account_id=any(@ids) and base_role='guest' and first_purchase_at is null", c))
        {
            check.Parameters.AddWithValue("ids", new[] { owner.Id, friend.Id });
            Assert.Equal(2, Convert.ToInt64(await check.ExecuteScalarAsync()));
        }
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var quote = await Quote(s, friend.Id);
        Assert.Equal(135000, quote.Payable.MinorUnits);
        var payment = await Prepare(f, s, friend.Id, quote.QuoteId);
        await Succeed(f, s, friend.Id, payment, true);
        var summary = await s.SummaryAsync(owner.Id, CancellationToken.None);
        Assert.Equal(1, summary.Paid);
        Assert.Equal(13500, Balance(summary).PendingMinor);
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var role = new NpgsqlCommand("select base_role from licensing.accounts where account_id=@a", connection);
        role.Parameters.AddWithValue("a", owner.Id);
        Assert.Equal("guest", await role.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Referral_claim_rejects_self_and_preserves_one_attribution()
    {
        await using var f = await ApiFixture.StartAsync();
        var store = new PromotionStore(f.Service<CreditLedger>(), f.Clock, new PromotionOptions(true));
        var owner = await Primary(f, "880001");
        var friend = await Primary(f, "880002");
        var other = await Primary(f, "880003");
        var invitation = await store.SummaryAsync(owner.Id, CancellationToken.None);
        await Assert.ThrowsAsync<PromotionConflictException>(() => store.ClaimAsync(owner.Id, invitation.Code, CancellationToken.None));
        await store.ClaimAsync(friend.Id, invitation.Code, CancellationToken.None);
        await store.ClaimAsync(friend.Id, invitation.Code, CancellationToken.None);
        var otherCode = (await store.SummaryAsync(other.Id, CancellationToken.None)).Code;
        await Assert.ThrowsAsync<PromotionConflictException>(() => store.ClaimAsync(friend.Id, otherCode, CancellationToken.None));
        Assert.Equal(1, (await store.SummaryAsync(owner.Id, CancellationToken.None)).Invited);
    }

    [Fact]
    public async Task Reward_matures_after_fourteen_days_and_expires_after_365_available_days()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "owner-maturity");
        await Earn(f, s, owner.Id, "maturity-friend");
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).PendingMinor);
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        f.Clock.Advance(TimeSpan.FromDays(14));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        f.Clock.Advance(TimeSpan.FromDays(365));
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
    }

    [Fact]
    public async Task Wallet_reserve_cancel_spend_and_full_refund_conserve_amounts_and_replays()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "wallet-owner");
        await Earn(f, s, owner.Id, "wallet-friend");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var q = await Quote(s, owner.Id, use: true);
        Assert.Equal(13500, q.Bonus.MinorUnits);
        var canceled = await Prepare(f, s, owner.Id, q.QuoteId);
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).ReservedMinor);
        await Hook(f, s, owner.Id, canceled.Id, (c, tx) => s.CancelAsync(c, tx, canceled.Id, CancellationToken.None));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        var payment = await Prepare(f, s, owner.Id, (await Quote(s, owner.Id, use: true)).QuoteId);
        // A reserved invoice may settle after the source funds expire.
        f.Clock.Advance(TimeSpan.FromDays(366));
        await Succeed(f, s, owner.Id, payment, true);
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).ReservedMinor);
        var disabled = Store(f, false);
        await Hook(f, disabled, owner.Id, payment.Id, (c, tx) => disabled.RefundAsync(c, tx, payment.Id, "full", payment.Amount.MinorUnits, payment.Amount.MinorUnits, true, CancellationToken.None));
        await Hook(f, disabled, owner.Id, payment.Id, (c, tx) => disabled.RefundAsync(c, tx, payment.Id, "full", payment.Amount.MinorUnits, payment.Amount.MinorUnits, true, CancellationToken.None));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        Assert.Equal(1, await Count(f, "select count(*) from licensing.bonus_events where kind='restore'"));
        await Assert.ThrowsAsync<PromotionDisabledException>(() => disabled.SummaryAsync(owner.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Spent_reward_refund_debt_is_offset_permanently_by_mature_active_funds()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "debt-owner");
        var repayment = await Primary(f, "debt-repayment");
        var source = await Earn(f, s, owner.Id, "debt-source");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var use = await Prepare(f, s, owner.Id, (await Quote(s, owner.Id, use: true)).QuoteId);
        await Succeed(f, s, owner.Id, use, true);
        await Hook(f, s, source.Account, source.Payment.Id, (c, tx) => s.RefundAsync(c, tx, source.Payment.Id, "partial", 50000, source.Payment.Amount.MinorUnits, false, CancellationToken.None));
        Assert.Equal(5000, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).DebtMinor);
        await Hook(f, s, source.Account, source.Payment.Id, (c, tx) => s.RefundAsync(c, tx, source.Payment.Id, "partial", 50000, source.Payment.Amount.MinorUnits, false, CancellationToken.None));
        Assert.Equal(1, await Count(f, "select count(*) from licensing.bonus_events where kind='clawback'"));
        await Earn(f, s, owner.Id, "debt-repayment", repayment);
        Assert.Equal(5000, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).DebtMinor);
        f.Clock.Advance(TimeSpan.FromDays(14));
        var balanced = Balance(await s.SummaryAsync(owner.Id, CancellationToken.None));
        Assert.Equal(0, balanced.DebtMinor);
        Assert.Equal(8500, balanced.AvailableMinor);
        Assert.Equal(0, await Count(f, "select coalesce(sum(remaining_delta),0)::bigint from licensing.bonus_events where kind like 'offset_%'"));
        f.Clock.Advance(TimeSpan.FromDays(366));
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).DebtMinor);
    }

    [Fact]
    public async Task Coupon_last_use_is_reserved_atomically_and_cancel_releases_budget()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var admin = await Primary(f, "coupon-admin");
        await f.PromoteAdminAsync(admin.Id);
        var now = f.Clock.GetUtcNow();
        await s.SavePromoAsync(admin.Id, new("LAST20", 2000, null, "RUB", now.AddDays(-1), now.AddDays(5), 1, 1, 30000), CancellationToken.None);
        var a = await Primary(f, "coupon-a");
        var b = await Primary(f, "coupon-b");
        var qa = await Quote(s, a.Id, "LAST20");
        var qb = await Quote(s, b.Id, "LAST20");
        async Task<(Guid Account, Prepared? Payment)> Attempt(Guid account, Guid quote)
        {
            try { return (account, await Prepare(f, s, account, quote)); }
            catch (PromotionUnavailableException) { return (account, null); }
        }
        var results = await Task.WhenAll(Attempt(a.Id, qa.QuoteId), Attempt(b.Id, qb.QuoteId));
        var winner = Assert.Single(results, x => x.Payment is not null);
        var view = Assert.Single(await s.ListPromosAsync(CancellationToken.None));
        Assert.Equal(1, view.Reserved);
        Assert.Equal(30000, view.ReservedMinor);
        await Hook(f, s, winner.Account, winner.Payment!.Id, (c, tx) => s.CancelAsync(c, tx, winner.Payment.Id, CancellationToken.None));
        view = Assert.Single(await s.ListPromosAsync(CancellationToken.None));
        Assert.Equal(0, view.Reserved);
        Assert.Equal(0, view.ReservedMinor);
    }

    [Fact]
    public async Task Guest_claim_survives_link_and_rejects_self_referral_after_merge()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "link-owner");
        var guest = await f.AccountAsync("telegram", "880009", telegramOnly: true);
        var primary = await Primary(f, "link-friend");
        var code = (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code;
        await s.ClaimAsync(guest.Id, code, CancellationToken.None);
        await Execute(f, "update licensing.accounts set merged_into=@target where account_id=@source", ("target", primary.Id), ("source", guest.Id));
        Assert.True((await Quote(s, primary.Id)).ReferralApplied);
        await s.ClaimAsync(primary.Id, code, CancellationToken.None);
        Assert.Equal(1, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Invited);
        await Execute(f, "update licensing.accounts set merged_into=@target where account_id=@source", ("target", owner.Id), ("source", primary.Id));
        await Assert.ThrowsAsync<PromotionConflictException>(() => s.ClaimAsync(owner.Id, code, CancellationToken.None));
    }

    [Fact]
    public async Task Quote_price_change_expiry_and_foreign_owner_are_rejected_without_reservations()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var a = await Primary(f, "quote-a");
        var b = await Primary(f, "quote-b");
        var q = await Quote(s, a.Id);
        await Assert.ThrowsAsync<PromotionConflictException>(() => Prepare(f, s, b.Id, q.QuoteId));
        var changed = Product with { Days = 31 };
        await Assert.ThrowsAsync<PromotionConflictException>(() => Prepare(f, s, a.Id, q.QuoteId, changed));
        f.Clock.Advance(TimeSpan.FromMinutes(10));
        await Assert.ThrowsAsync<PromotionConflictException>(() => Prepare(f, s, a.Id, q.QuoteId));
        Assert.Equal(0, await Count(f, "select count(*) from licensing.promotion_payments"));
    }

    [Fact]
    public async Task Expired_positive_funds_cannot_offset_late_refund_debt_and_ledger_is_balanced()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "expiry-debt-owner");
        var unused = await Primary(f, "expiry-unused");
        var source = await Earn(f, s, owner.Id, "expiry-debt-source");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var spend = await Prepare(f, s, owner.Id, (await Quote(s, owner.Id, use: true)).QuoteId);
        await Succeed(f, s, owner.Id, spend, true);
        await Earn(f, s, owner.Id, "expiry-unused", unused);
        f.Clock.Advance(TimeSpan.FromDays(380));
        await Hook(f, s, source.Account, source.Payment.Id, (c, tx) => s.RefundAsync(c, tx, source.Payment.Id, "late-full", source.Payment.Amount.MinorUnits, source.Payment.Amount.MinorUnits, true, CancellationToken.None));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).DebtMinor);
        Assert.Equal(0, await Count(f, "select count(*) from licensing.bonus_events where kind like 'offset_%'"));
        Assert.Equal(0, await Count(f, """
            select count(*) from licensing.bonus_lots l left join (
              select lot_id,sum(remaining_delta) remaining,sum(reserved_delta) reserved from licensing.bonus_events group by lot_id
            ) e using(lot_id) where l.remaining_minor<>coalesce(e.remaining,0) or l.reserved_minor<>coalesce(e.reserved,0)
            """));
    }

    [Fact]
    public async Task Credit_topups_never_award_referral_and_runtime_cannot_mutate_events()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "no-award-owner");
        var friend = await Primary(f, "no-award-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var credit = Product with { Kind = "credits", Credits = 3, Days = 0, PlanId = null };
        var topup = await Prepare(f, s, friend.Id, product: credit);
        await Succeed(f, s, friend.Id, topup, true);
        Assert.Equal(0, await Count(f, "select count(*) from licensing.bonus_lots"));
        var failed = await Assert.ThrowsAsync<PostgresException>(() => Hook(f, s, friend.Id, Guid.Empty, async (c, tx) =>
        {
            await using var cmd = new NpgsqlCommand("update licensing.bonus_events set amount_minor=0", c, tx);
            await cmd.ExecuteNonQueryAsync();
        }));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failed.SqlState);
    }

    [Fact]
    public async Task Initial_recurring_plan_rewards_referrer_without_discount_but_renewal_does_not()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "initial-recurring-owner");
        var friend = await Primary(f, "initial-recurring-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var quote = await s.QuoteAsync(friend.Id, new(Product.Sku, "yookassa", null, false, true), Product, "storage-test", CancellationToken.None);
        Assert.Equal(0, quote.Discount.MinorUnits);
        Assert.Equal(0, quote.Bonus.MinorUnits);
        Assert.Equal(150000, quote.Payable.MinorUnits);
        Assert.False(quote.ReferralApplied);
        var initial = await Prepare(f, s, friend.Id, quote.QuoteId, recurring: true);
        await Succeed(f, s, friend.Id, initial, true);
        Assert.Equal(15000, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).PendingMinor);
        var renewal = await Prepare(f, s, friend.Id, recurring: true);
        await Succeed(f, s, friend.Id, renewal, false);
        Assert.Equal(15000, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).PendingMinor);
        Assert.Equal(1, await Count(f, "select count(*) from licensing.bonus_lots"));
    }

    [Fact]
    public async Task Concurrent_wallet_quotes_cannot_reserve_the_same_funds_twice()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "concurrent-wallet-owner");
        await Earn(f, s, owner.Id, "concurrent-wallet-friend");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var a = await Quote(s, owner.Id, use: true);
        var b = await Quote(s, owner.Id, use: true);
        async Task<Prepared?> Attempt(Guid quote)
        {
            try { return await Prepare(f, s, owner.Id, quote); }
            catch (PromotionUnavailableException e) when (e.Code == "bonus_unavailable") { return null; }
        }
        var payments = await Task.WhenAll(Attempt(a.QuoteId), Attempt(b.QuoteId));
        var winner = Assert.Single(payments, x => x is not null)!;
        Assert.Equal(1, await Count(f, "select count(*) from licensing.promotion_payments where bonus_minor>0"));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).ReservedMinor);
        await Succeed(f, Store(f, false), owner.Id, winner, true);
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).ReservedMinor);
    }

    [Fact]
    public async Task Reward_refund_during_reservation_preserves_invoice_then_creates_auditable_debt()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "reserved-clawback-owner");
        var source = await Earn(f, s, owner.Id, "reserved-clawback-friend");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var purchase = await Prepare(f, s, owner.Id, (await Quote(s, owner.Id, use: true)).QuoteId);
        await Hook(f, s, source.Account, source.Payment.Id, (c, tx) => s.RefundAsync(c, tx, source.Payment.Id, "reserved-partial", 50000, source.Payment.Amount.MinorUnits, false, CancellationToken.None));
        var held = Balance(await s.SummaryAsync(owner.Id, CancellationToken.None));
        Assert.Equal(13500, held.ReservedMinor);
        Assert.Equal(5000, held.DebtMinor);
        await Succeed(f, s, owner.Id, purchase, true);
        var settled = Balance(await s.SummaryAsync(owner.Id, CancellationToken.None));
        Assert.Equal(0, settled.ReservedMinor);
        Assert.Equal(5000, settled.DebtMinor);
        Assert.Equal(0, settled.AvailableMinor);
    }

    [Fact]
    public async Task First_qualifying_paid_receipt_counts_even_when_reward_rounds_to_zero()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "tiny-reward-owner");
        var friend = await Primary(f, "tiny-reward-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var tiny = Product with { Prices = new Dictionary<string, CatalogPrice> { ["yookassa"] = new(9, "RUB") } };
        var quote = await s.QuoteAsync(friend.Id, new(tiny.Sku, "yookassa", null, false), tiny, "storage-test", CancellationToken.None);
        var payment = await Prepare(f, s, friend.Id, quote.QuoteId, tiny);
        await Succeed(f, s, friend.Id, payment, true);
        var summary = await s.SummaryAsync(owner.Id, CancellationToken.None);
        Assert.Equal(1, summary.Paid);
        Assert.Equal(0, Balance(summary).PendingMinor);
        Assert.Equal(0, await Count(f, "select count(*) from licensing.bonus_lots"));
    }

    [Fact]
    public async Task Reserved_clawback_cannot_consume_new_funds_into_expired_lot_before_cancel()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "reserved-offset-owner");
        var future = await Primary(f, "reserved-offset-future");
        var source = await Earn(f, s, owner.Id, "reserved-offset-source");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var held = await Prepare(f, s, owner.Id, (await Quote(s, owner.Id, use: true)).QuoteId);
        await Hook(f, s, source.Account, source.Payment.Id, (c, tx) => s.RefundAsync(c, tx, source.Payment.Id, "full-held", source.Payment.Amount.MinorUnits, source.Payment.Amount.MinorUnits, true, CancellationToken.None));
        f.Clock.Advance(TimeSpan.FromDays(365));
        await Earn(f, s, owner.Id, "reserved-offset-future", future);
        f.Clock.Advance(TimeSpan.FromDays(14));
        Assert.Equal(0, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        await Hook(f, s, owner.Id, held.Id, (c, tx) => s.CancelAsync(c, tx, held.Id, CancellationToken.None));
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
        Assert.Equal(0, await Count(f, "select count(*) from licensing.bonus_events where kind like 'offset_%'"));
    }

    [Fact]
    public async Task First_discount_rejects_any_prior_pending_full_price_invoice_including_legacy()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "pending-old-owner");
        var friend = await Primary(f, "pending-old-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var prior = await Prepare(f, Store(f, false), friend.Id);
        var quote = await Quote(s, friend.Id);
        var failed = await Assert.ThrowsAsync<PromotionUnavailableException>(() => Prepare(f, s, friend.Id, quote.QuoteId));
        Assert.Equal("first_purchase_reserved", failed.Code);
        await Execute(f, "update licensing.payments set state='canceled' where payment_id=@p", ("p", prior.Id));
        Assert.Equal(135000, (await Prepare(f, s, friend.Id, quote.QuoteId)).Amount.MinorUnits);
    }

    [Fact]
    public async Task Pending_first_discount_prevents_new_full_price_invoices_even_after_disabling_feature()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "pending-new-owner");
        var friend = await Primary(f, "pending-new-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var held = await Prepare(f, s, friend.Id, (await Quote(s, friend.Id)).QuoteId);
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => Prepare(f, s, friend.Id));
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => Prepare(f, Store(f, false), friend.Id));
        await Hook(f, s, friend.Id, held.Id, (c, tx) => s.CancelAsync(c, tx, held.Id, CancellationToken.None));
        await Execute(f, "update licensing.payments set state='canceled' where payment_id=@p", ("p", held.Id));
        Assert.Equal(150000, (await Prepare(f, s, friend.Id)).Amount.MinorUnits);
    }

    [Fact]
    public async Task Parallel_first_discount_and_full_price_checkout_allow_only_one_pending_invoice()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "parallel-first-owner");
        var friend = await Primary(f, "parallel-first-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        var quote = await Quote(s, friend.Id);
        async Task<Prepared?> Attempt(Guid? q)
        {
            try { return await Prepare(f, s, friend.Id, q); }
            catch (PromotionUnavailableException e) when(e.Code == "first_purchase_reserved") { return null; }
        }
        var outcomes = await Task.WhenAll(Attempt(quote.QuoteId), Attempt(null));
        Assert.Single(outcomes, x => x is not null);
        Assert.Equal(1, await Count(f, "select count(*) from licensing.payments"));
    }

    [Fact]
    public async Task Admin_role_can_read_merge_metadata_but_cannot_rewrite_attribution()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "metadata-owner");
        var friend = await Primary(f, "metadata-friend");
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner.Id, CancellationToken.None)).Code, CancellationToken.None);
        await using var c = await f.Database.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("set local role vg_admin", c, tx)) await role.ExecuteNonQueryAsync();
        await using (var read = new NpgsqlCommand("select count(*) from licensing.referrals", c, tx))
            Assert.Equal(1, Convert.ToInt64(await read.ExecuteScalarAsync()));
        await using var write = new NpgsqlCommand("update licensing.referrals set referrer_id=referrer_id", c, tx);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
    }

    [Fact]
    public async Task Coupon_budget_per_account_currency_and_first_purchase_limits_are_independent()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var admin = await Primary(f, "independent-limits-admin");
        var a = await Primary(f, "independent-limits-a");
        var b = await Primary(f, "independent-limits-b");
        await f.PromoteAdminAsync(admin.Id);
        var now = f.Clock.GetUtcNow();
        await s.SavePromoAsync(admin.Id, new("BUDGET20", 2000, null, "RUB", now.AddDays(-1), now.AddDays(5), 10, 10, 45000, false), CancellationToken.None);
        var budgetA = await Quote(s, a.Id, "BUDGET20");
        var budgetB = await Quote(s, b.Id, "BUDGET20");
        await Prepare(f, s, a.Id, budgetA.QuoteId);
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => Prepare(f, s, b.Id, budgetB.QuoteId));
        var budget = Assert.Single(await s.ListPromosAsync(CancellationToken.None));
        Assert.Equal(1, budget.Reserved);
        Assert.Equal(30000, budget.ReservedMinor);

        await s.SavePromoAsync(admin.Id, new("ACCOUNT20", 2000, null, "RUB", now.AddDays(-1), now.AddDays(5), 10, 1, 300000, false), CancellationToken.None);
        await Prepare(f, s, a.Id, (await Quote(s, a.Id, "ACCOUNT20")).QuoteId);
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => Quote(s, a.Id, "ACCOUNT20"));
        Assert.Equal(30000, (await Quote(s, b.Id, "ACCOUNT20")).Discount.MinorUnits);
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => s.QuoteAsync(b.Id,
            new(Product.Sku, "stars", "ACCOUNT20", false), Product, "storage-test", CancellationToken.None));

        await s.SavePromoAsync(admin.Id, new("FIRST20", 2000, null, "RUB", now.AddDays(-1), now.AddDays(5), 10, 1, 300000), CancellationToken.None);
        await Execute(f, "update licensing.accounts set first_purchase_at=@now where account_id=@a", ("now", now), ("a", b.Id));
        await Assert.ThrowsAsync<PromotionUnavailableException>(() => Quote(s, b.Id, "FIRST20"));
        Assert.Equal(30000, (await Quote(s, b.Id, "ACCOUNT20")).Discount.MinorUnits);
    }

    [Fact]
    public async Task Ruble_wallet_funds_never_reduce_a_stars_quote()
    {
        await using var f = await ApiFixture.StartAsync();
        var s = Store(f);
        var owner = await Primary(f, "currency-wallet-owner");
        await Earn(f, s, owner.Id, "currency-wallet-friend");
        f.Clock.Advance(TimeSpan.FromDays(14));
        var quote = await s.QuoteAsync(owner.Id, new(Product.Sku, "stars", null, true), Product, "storage-test", CancellationToken.None);
        Assert.Equal("XTR", quote.Payable.Currency);
        Assert.Equal(150, quote.Payable.MinorUnits);
        Assert.Equal(0, quote.Bonus.MinorUnits);
        Assert.Equal(13500, Balance(await s.SummaryAsync(owner.Id, CancellationToken.None)).AvailableMinor);
    }

    private static PromotionStore Store(ApiFixture f, bool enabled = true) => new(f.Service<CreditLedger>(), f.Clock, new PromotionOptions(enabled));
    private static Task<TestAccount> Primary(ApiFixture f, string key)
        => f.AccountAsync("google", key, key + "@example.test");
    private static BonusBalance Balance(ReferralSummary s) => Assert.Single(s.Balances, x => x.Currency == "RUB");
    private static readonly CatalogProduct Product = new("storage.start", "time", 0, 30, true,
        new Dictionary<string, CatalogPrice> { ["yookassa"] = new(150000, "RUB"), ["stars"] = new(150, "XTR") }) { PlanId = "start" };
    private static Task<PromotionQuote> Quote(PromotionStore s, Guid account, string? code = null, bool use = false)
        => s.QuoteAsync(account, new(Product.Sku, "yookassa", code, use), Product, "storage-test", CancellationToken.None);
    private sealed record Prepared(Guid Id, Money Amount);
    private static async Task<Prepared> Prepare(ApiFixture f, PromotionStore s, Guid account, Guid? quote = null, CatalogProduct? product = null, bool recurring = false)
    {
        product ??= Product;
        var p = Guid.NewGuid();
        var request = new PurchaseRequest(product.Sku, "yookassa", Guid.NewGuid(), recurring, quote);
        Money? amount = null;
        await Hook(f, s, account, Guid.Empty, async (c, tx) =>
        {
            amount = await s.PreparePaymentAsync(c, tx, account, p, request, product, CancellationToken.None);
            await using var cmd = new NpgsqlCommand("""
                insert into licensing.payments(payment_id,account_id,provider,environment,sku,catalog_version,expected_minor,expected_currency,recurring,idempotency_key,request_hash,state,created_at,updated_at)
                values(@p,@a,'yookassa','test',@sku,'storage-test',@minor,'RUB',@recurring,@key,'storage-test','pending',@now,@now)
                """, c, tx);
            cmd.Parameters.AddWithValue("p", p); cmd.Parameters.AddWithValue("a", account); cmd.Parameters.AddWithValue("sku", product.Sku);
            cmd.Parameters.AddWithValue("minor", amount.MinorUnits); cmd.Parameters.AddWithValue("key", request.IdempotencyKey); cmd.Parameters.AddWithValue("now", f.Clock.GetUtcNow());
            cmd.Parameters.AddWithValue("recurring", recurring);
            await cmd.ExecuteNonQueryAsync();
        });
        return new(p, amount!);
    }
    private static Task Succeed(ApiFixture f, PromotionStore s, Guid a, Prepared p, bool first)
        => Hook(f, s, a, p.Id, (c, tx) => s.SuccessAsync(c, tx, new("yookassa", "test", "storage-" + p.Id, p.Id, a, p.Amount, "succeeded", f.Clock.GetUtcNow(), null, null), Product, first, CancellationToken.None));
    private static async Task<(Guid Account, Prepared Payment)> Earn(ApiFixture f, PromotionStore s, Guid owner, string key, TestAccount? existing = null)
    {
        var friend = existing ?? await Primary(f, key);
        await s.ClaimAsync(friend.Id, (await s.SummaryAsync(owner, CancellationToken.None)).Code, CancellationToken.None);
        var payment = await Prepare(f, s, friend.Id, (await Quote(s, friend.Id)).QuoteId);
        await Succeed(f, s, friend.Id, payment, true);
        await Succeed(f, s, friend.Id, payment, true);
        return (friend.Id, payment);
    }
    private static async Task Hook(ApiFixture f, PromotionStore s, Guid a, Guid p, Func<NpgsqlConnection, NpgsqlTransaction, Task> hook)
    {
        await using var c = await f.Database.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("set local role vg_ledger", c, tx)) await role.ExecuteNonQueryAsync();
        await s.LockPaymentAccountsAsync(c, tx, a, p, CancellationToken.None);
        await hook(c, tx);
        await tx.CommitAsync();
    }
    private static async Task<long> Count(ApiFixture f, string sql)
    { await using var c = await f.Database.OpenConnectionAsync(); await using var cmd = new NpgsqlCommand(sql, c); return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    private static async Task Execute(ApiFixture f, string sql, params (string Key, object Value)[] parameters)
    {
        await using var c = await f.Database.OpenConnectionAsync(); await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (key, value) in parameters) cmd.Parameters.AddWithValue(key, value);
        await cmd.ExecuteNonQueryAsync();
    }
}
