using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;

namespace VideoGrabber.Platform.Persistence;

public sealed partial class PromotionStore
{
    public async Task LockPaymentAccountsAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid accountId, Guid paymentId, CancellationToken ct)
    {
        Guid? referrer;
        if (paymentId == Guid.Empty) referrer = _options.Enabled ? await ReferralAsync(c, tx, accountId, ct) : null;
        else referrer = await ScalarAsync(c, tx, "select referrer_id from licensing.promotion_payments where payment_id=@p", ct, ("p", paymentId)) as Guid?;
        foreach (var a in new[] { accountId, referrer ?? accountId }.Distinct().Order()) await AccountLockAsync(c, tx, a, ct);
    }

    public async Task<Money> PreparePaymentAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid accountId, Guid paymentId, PurchaseRequest request, CatalogProduct product, CancellationToken ct)
    {
        var price = product.Prices[request.Provider];
        if (!_options.Enabled && request.QuoteId is not null) throw new PromotionDisabledException();
        // Outstanding first-only invoices keep their eligibility even after
        // marketing is disabled. A competing full-price receipt must not
        // become the first success while that invoice remains unsettled.
        var firstReserved = await ScalarAsync(c, tx, """
            select exists(select 1 from licensing.promotion_payments p
              left join licensing.promo_codes x on x.code=p.promo_code
              where p.account_id=@a and p.state='reserved' and (p.referral_applied or x.first_purchase_only))
            """, ct, ("a", accountId));
        if (firstReserved is true) throw new PromotionUnavailableException("first_purchase_reserved");
        if (!_options.Enabled)
        {
            return new(price.MinorUnits, price.Currency);
        }
        if (request.QuoteId is null)
        {
            var registered = false;
            await using (var cmd = Command(c, tx, """
                select base_role,primary_auth_provider from licensing.accounts
                where account_id=@a and blocked_at is null and merged_into is null
                """, ("a", accountId)))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                if (await r.ReadAsync(ct)) registered = IsPrimaryAccount(accountId, r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1));
            // Enabling marketing cannot change the existing full-price checkout
            // contract for Telegram guests or other accounts outside its scope.
            if (!registered) return new(price.MinorUnits, price.Currency);
        }
        var first = await RequireAccountAsync(c, tx, accountId, false, ct);
        await NormalizeDebtAsync(c, tx, accountId, ct);
        var qualified = Qualifies(product);
        var referrer = qualified && first ? await ReferralAsync(c, tx, accountId, ct) : null;
        if (referrer == accountId) referrer = null;
        string? code = null;
        long original = price.MinorUnits, discount = 0, bonus = 0, payable = price.MinorUnits;
        var referralApplied = false;
        PromoView? promo = null;
        if (request.QuoteId is Guid quoteId)
        {
            await using (var cmd = Command(c, tx, """
                select account_id,sku,provider,product_snapshot,currency,original_minor,discount_minor,bonus_minor,payable_minor,
                    promo_code,referrer_id,referral_applied,recurring,expires_at,payment_id,use_bonuses
                from licensing.promotion_quotes where quote_id=@q for update
                """, ("q", quoteId)))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await r.ReadAsync(ct) || r.GetGuid(0) != accountId || r.GetString(1) != request.Sku || r.GetString(2) != request.Provider
                    || r.GetString(3) != ProductSnapshot(product, request.Provider) || r.GetString(4) != price.Currency
                    || r.GetBoolean(12) != request.Recurring || r.GetFieldValue<DateTimeOffset>(13) <= _clock.GetUtcNow() || !r.IsDBNull(14))
                    throw new PromotionConflictException();
                original = r.GetInt64(5); discount = r.GetInt64(6); bonus = r.GetInt64(7); payable = r.GetInt64(8);
                code = r.IsDBNull(9) ? null : r.GetString(9);
                var quotedReferrer = r.IsDBNull(10) ? (Guid?)null : r.GetGuid(10);
                referralApplied = r.GetBoolean(11);
                if (quotedReferrer != referrer || (referralApplied && !first) || ((!qualified || request.Recurring) && (discount != 0 || bonus != 0))) throw new PromotionConflictException();
            }
            if (code is not null)
            {
                promo = await ReadPromoAsync(c, tx, code, ct) ?? throw new PromotionUnavailableException("promo_unavailable");
                await CheckPromoAsync(c, tx, accountId, promo, product.Sku, price.Currency, first, discount, ct);
                // Reject a quote when a coupon definition no longer matches its
                // stored reduction, including price rounding.
                if (PromotionPolicy.Portion(original, promo.Definition.DiscountBasisPoints) != discount) throw new PromotionConflictException();
            }
            if (referralApplied || promo?.Definition.FirstPurchaseOnly == true)
            {
                var pending = Convert.ToInt64(await ScalarAsync(c, tx, """
                    select count(*) from licensing.payments
                    where account_id=@a and state in ('pending','reconcile_required')
                    """, ct, ("a", accountId)));
                if (pending > 0) throw new PromotionUnavailableException("first_purchase_reserved");
            }
            await ExecAsync(c, tx, "update licensing.promotion_quotes set payment_id=@p where quote_id=@q", ct, ("p", paymentId), ("q", quoteId));
        }
        await ExecAsync(c, tx, """
            insert into licensing.promotion_payments(payment_id,account_id,referrer_id,quote_id,promo_code,currency,
              original_minor,discount_minor,bonus_minor,payable_minor,referral_applied,recurring,qualified,reward_bps,hold_days,lifetime_days,state,created_at)
            values(@p,@a,@r,@q,@code,@currency,@original,@discount,@bonus,@payable,@referral,@recurring,@qualified,@rewardbps,@hold,@lifetime,'reserved',@now)
            """, ct, ("p", paymentId), ("a", accountId), ("r", referrer), ("q", request.QuoteId), ("code", code),
            ("currency", price.Currency), ("original", original), ("discount", discount), ("bonus", bonus), ("payable", payable),
            ("referral", referralApplied), ("recurring", request.Recurring), ("qualified", qualified), ("rewardbps", PromotionPolicy.ReferralBasisPoints),
            ("hold", PromotionPolicy.HoldDays), ("lifetime", PromotionPolicy.BonusLifetimeDays), ("now", _clock.GetUtcNow()));
        if (code is not null)
            await ExecAsync(c, tx, "update licensing.promo_codes set reserved=reserved+1,reserved_minor=reserved_minor+@discount where code=@code", ct, ("discount", discount), ("code", code));
        if (bonus > 0) await ReserveWalletAsync(c, tx, accountId, paymentId, price.Currency, bonus, ct);
        return new(payable, price.Currency);
    }

    public async Task SuccessAsync(NpgsqlConnection c, NpgsqlTransaction tx, VerifiedPayment payment, CatalogProduct product, bool firstPurchase, CancellationToken ct)
    {
        var row = await PaymentPromotionAsync(c, tx, payment.PaymentId, ct);
        if (row is null || row.State is "succeeded" or "refunded") return;
        if (row.State != "reserved" || payment.Status != "succeeded" || row.Account != payment.AccountId
            || row.Payable != payment.Amount.MinorUnits || row.Currency != payment.Amount.Currency) throw new PromotionConflictException();
        await FinalizeWalletAsync(c, tx, payment.PaymentId, true, ct);
        if (row.Code is not null)
            await ExecAsync(c, tx, "update licensing.promo_codes set reserved=reserved-1,reserved_minor=reserved_minor-@d,used=used+1,spent_minor=spent_minor+@d where code=@code", ct, ("d", row.Discount), ("code", row.Code));
        var qualifiesForReward = firstPurchase && row.Qualified && row.Referrer is Guid r && r != row.Account;
        var reward = qualifiesForReward ? PromotionPolicy.Portion(payment.Amount.MinorUnits, row.RewardBasisPoints) : 0;
        if (reward > 0 && row.Referrer is Guid referrer)
        {
            var now = _clock.GetUtcNow();
            var available = now.AddDays(row.HoldDays);
            var lot = Guid.NewGuid();
            await ExecAsync(c, tx, """
                insert into licensing.bonus_lots(lot_id,account_id,currency,source_payment_id,kind,granted_minor,remaining_minor,valid_from,expires_at,created_at)
                values(@lot,@a,@currency,@p,'reward',@amount,@amount,@available,@expires,@now)
                """, ct, ("lot", lot), ("a", referrer), ("currency", row.Currency), ("p", payment.PaymentId), ("amount", reward),
                ("available", available), ("expires", available.AddDays(row.LifetimeDays)), ("now", now));
            await EventAsync(c, tx, lot, referrer, payment.PaymentId, "reward:" + payment.PaymentId, "reward", reward, reward, 0, ct);
        }
        if (qualifiesForReward)
        {
            await ExecAsync(c, tx, """
                with recursive ancestors as (select account_id from licensing.accounts where account_id=@a
                  union select x.account_id from licensing.accounts x join ancestors p on x.merged_into=p.account_id)
                update licensing.referrals set qualified_payment_id=@p where referee_id=(
                  select r.referee_id from licensing.referrals r join ancestors a on r.referee_id=a.account_id
                  where r.qualified_payment_id is null order by r.created_at,r.referee_id limit 1)
                """, ct, ("a", row.Account), ("p", payment.PaymentId));
        }
        await ExecAsync(c, tx, "update licensing.promotion_payments set state='succeeded',reward_minor=@reward where payment_id=@p", ct,
            ("reward", reward), ("p", payment.PaymentId));
    }

    public async Task CancelAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid paymentId, CancellationToken ct)
    {
        var row = await PaymentPromotionAsync(c, tx, paymentId, ct);
        if (row is null || row.State == "canceled") return;
        if (row.State != "reserved") throw new PromotionConflictException();
        await FinalizeWalletAsync(c, tx, paymentId, false, ct);
        if (row.Code is not null)
            await ExecAsync(c, tx, "update licensing.promo_codes set reserved=reserved-1,reserved_minor=reserved_minor-@d where code=@code", ct, ("d", row.Discount), ("code", row.Code));
        await ExecAsync(c, tx, "update licensing.promotion_payments set state='canceled' where payment_id=@p", ct, ("p", paymentId));
        await NormalizeDebtAsync(c, tx, row.Account, ct);
    }

    public async Task RefundAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid paymentId, string refundKey, long cumulativeRefundedMinor, long originalPaidMinor, bool fullRefund, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refundKey) || refundKey.Length > 200 || cumulativeRefundedMinor < 0 || originalPaidMinor <= 0
            || cumulativeRefundedMinor > originalPaidMinor || fullRefund != (cumulativeRefundedMinor == originalPaidMinor)) throw new PromotionConflictException();
        var row = await PaymentPromotionAsync(c, tx, paymentId, ct);
        if (row is null) return;
        if (row.State is not ("succeeded" or "refunded") || row.Payable != originalPaidMinor || cumulativeRefundedMinor < row.Refunded) throw new PromotionConflictException();
        var targetReward = row.Reward > 0 ? PromotionPolicy.Portion(originalPaidMinor - cumulativeRefundedMinor, row.RewardBasisPoints) : 0;
        var delta = checked(row.Reward - targetReward);
        if (delta > 0 && row.Referrer is Guid referrer)
        {
            var lot = (Guid)(await ScalarAsync(c, tx, "select lot_id from licensing.bonus_lots where source_payment_id=@p and kind='reward' for update", ct, ("p", paymentId))
                ?? throw new PromotionConflictException());
            await ExecAsync(c, tx, "update licensing.bonus_lots set remaining_minor=remaining_minor-@d where lot_id=@l", ct, ("d", delta), ("l", lot));
            await EventAsync(c, tx, lot, referrer, paymentId, "refund:" + paymentId + ":" + refundKey, "clawback", -delta, -delta, 0, ct);
            await NormalizeDebtAsync(c, tx, referrer, ct);
        }
        if (fullRefund && row.Bonus > 0 && !row.BonusRestored)
        {
            var lot = Guid.NewGuid();
            var now = _clock.GetUtcNow();
            await ExecAsync(c, tx, """
                insert into licensing.bonus_lots(lot_id,account_id,currency,source_payment_id,kind,granted_minor,remaining_minor,valid_from,expires_at,created_at)
                values(@lot,@a,@currency,@p,'restore',@amount,@amount,@now,@expires,@now)
                """, ct, ("lot", lot), ("a", row.Account), ("currency", row.Currency), ("p", paymentId), ("amount", row.Bonus),
                ("now", now), ("expires", now.AddDays(row.LifetimeDays)));
            await EventAsync(c, tx, lot, row.Account, paymentId, "restore:" + paymentId, "restore", row.Bonus, row.Bonus, 0, ct);
            await ExecAsync(c, tx, "update licensing.bonus_allocations set state='restored' where payment_id=@p and state='spent'", ct, ("p", paymentId));
            await NormalizeDebtAsync(c, tx, row.Account, ct);
        }
        await ExecAsync(c, tx, "update licensing.promotion_payments set reward_minor=@reward,refunded_minor=@refund,bonus_restored=bonus_restored or @full,state=case when @full then 'refunded' else state end where payment_id=@p", ct,
            ("reward", targetReward), ("refund", cumulativeRefundedMinor), ("full", fullRefund), ("p", paymentId));
    }

    private sealed record PromotionPayment(Guid Account, Guid? Referrer, string? Code, string Currency, long Discount, long Bonus, long Payable, bool Recurring, bool Qualified, string State, long Reward, long Refunded, bool BonusRestored, int RewardBasisPoints, int HoldDays, int LifetimeDays);
    private static async Task<PromotionPayment?> PaymentPromotionAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid p, CancellationToken ct)
    {
        await using var cmd = Command(c, tx, "select account_id,referrer_id,promo_code,currency,discount_minor,bonus_minor,payable_minor,recurring,qualified,state,reward_minor,refunded_minor,bonus_restored,reward_bps,hold_days,lifetime_days from licensing.promotion_payments where payment_id=@p for update", ("p", p));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return !await r.ReadAsync(ct) ? null : new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), r.GetBoolean(7), r.GetBoolean(8), r.GetString(9), r.GetInt64(10), r.GetInt64(11), r.GetBoolean(12), r.GetInt32(13), r.GetInt32(14), r.GetInt32(15));
    }
}
