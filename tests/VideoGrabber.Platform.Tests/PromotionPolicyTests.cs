using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PromotionPolicyTests
{
    [Theory]
    [InlineData(150000,135000,13500)]
    [InlineData(250000,225000,22500)]
    [InlineData(500000,450000,45000)]
    public void First_payment_gives_ten_percent_and_reward_uses_actual_cash(long price,long cash,long reward)
    {
        var quote=PromotionPolicy.Calculate(new Money(price,"RUB"),0,0,true,true,false,false);
        Assert.Equal(cash,quote.Payable.MinorUnits);
        Assert.Equal(reward,PromotionPolicy.Reward(cash));
    }
    [Fact]
    public void Best_discount_and_wallet_share_one_thirty_percent_cap()
    {
        var quote=PromotionPolicy.Calculate(new Money(150000,"RUB"),2000,999999,true,true,false,true);
        Assert.Equal(30000,quote.Discount.MinorUnits);
        Assert.Equal(15000,quote.Bonus.MinorUnits);
        Assert.Equal(105000,quote.Payable.MinorUnits);
        Assert.False(quote.ReferralApplied);
    }
    [Fact]
    public void Renewal_does_not_repeat_friend_discount()
    {
        Assert.Equal(150000,PromotionPolicy.Calculate(new Money(150000,"RUB"),0,0,true,false,false,false).Payable.MinorUnits);
    }
    [Fact]
    public void Stars_amounts_are_integer_and_small_invoices_remain_positive()
    {
        Assert.Equal(1,PromotionPolicy.Calculate(new Money(1,"XTR"),2000,99,false,true,false,true).Payable.MinorUnits);
        Assert.Equal(0,PromotionPolicy.Reward(1));
    }
    [Fact]
    public void Discounted_recurring_invoice_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(()=>PromotionPolicy.Calculate(new Money(100,"XTR"),1000,0,false,true,true,false));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(2001)]
    public void Invalid_percentage_is_rejected(int bps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>PromotionPolicy.Calculate(new Money(100,"RUB"),bps,0,false,true,false,false));
    }
    [Fact]
    public void Large_integer_amounts_do_not_overflow_or_use_floating_point()
    {
        var q=PromotionPolicy.Calculate(new Money(long.MaxValue,"RUB"),2000,long.MaxValue,true,true,false,true);
        Assert.Equal(long.MaxValue,q.Discount.MinorUnits+q.Bonus.MinorUnits+q.Payable.MinorUnits);
        Assert.True(q.Payable.MinorUnits>0);
    }
}
