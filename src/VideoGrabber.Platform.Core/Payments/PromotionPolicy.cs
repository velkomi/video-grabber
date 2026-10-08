using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Core.Payments;

public sealed record PromotionAmounts(Money Original, Money Discount, Money Bonus, Money Payable, bool ReferralApplied);

public static class PromotionPolicy
{
    public const int ReferralBasisPoints = 1000;
    public const int MaximumCouponBasisPoints = 2000;
    public const int MaximumCombinedBasisPoints = 3000;
    public const int HoldDays = 14;
    public const int BonusLifetimeDays = 365;

    public static PromotionAmounts Calculate(Money original, int promoBasisPoints, long walletAvailable,
        bool hasReferral, bool firstPayment, bool recurring, bool useBonuses)
    {
        PaymentProjection.ValidateMoney(original);
        if (original.Currency is not ("RUB" or "XTR")) throw new ArgumentException("promotion_currency_unsupported");
        if (promoBasisPoints < 0 || promoBasisPoints > MaximumCouponBasisPoints)
            throw new ArgumentOutOfRangeException(nameof(promoBasisPoints));
        if (walletAvailable < 0) throw new ArgumentOutOfRangeException(nameof(walletAvailable));
        if (recurring && (promoBasisPoints > 0 || (useBonuses && walletAvailable > 0)))
            throw new InvalidOperationException("one_time_discount_requires_single_purchase");
        var friendRate = hasReferral && firstPayment && !recurring ? ReferralBasisPoints : 0;
        var rate = Math.Max(friendRate, promoBasisPoints);
        var discount = Portion(original.MinorUnits, rate);
        var limit = Portion(original.MinorUnits, MaximumCombinedBasisPoints);
        var bonus = useBonuses && !recurring ? Math.Min(walletAvailable, Math.Max(0, limit - discount)) : 0;
        var payable = checked(original.MinorUnits - discount - bonus);
        if (payable <= 0) throw new InvalidOperationException("promotion_amount_invalid");
        return new(original, new Money(discount,original.Currency), new Money(bonus,original.Currency),
            new Money(payable,original.Currency), friendRate > promoBasisPoints && discount > 0);
    }

    public static long Reward(long netPaidMinor)
    {
        if (netPaidMinor < 0) throw new ArgumentOutOfRangeException(nameof(netPaidMinor));
        return Portion(netPaidMinor,ReferralBasisPoints);
    }

    public static long Portion(long amount, int basisPoints)
    {
        if (amount < 0 || basisPoints < 0 || basisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(amount));
        return checked((long)((Int128)amount * basisPoints / 10000));
    }
}
