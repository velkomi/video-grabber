using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PaymentOrderingTests
{
    [Fact]
    public async Task Verified_refund_before_success_is_applied_atomically_from_provider_proof()
    {
        await using var f=await ApiFixture.StartAsync();var a=await f.AccountAsync("google","refund-first");var store=f.Service<PaymentStore>();
        var order=await store.BeginAsync(a.Id,new("test.time30","yookassa",Guid.NewGuid(),false),default);await store.BindProviderReferenceAsync(order.PaymentId,"refund-first-charge",default);
        var proof=new VerifiedPayment("yookassa","test","refund-first-charge",order.PaymentId,a.Id,new(10000,"RUB"),"succeeded",f.Clock.GetUtcNow(),null,null);
        var refund=new VerifiedRefund("yookassa","test","refund-before-success","refund-first-charge",order.PaymentId,a.Id,new(10000,"RUB"),f.Clock.GetUtcNow()){OriginalPayment=proof};
        await store.ApplyVerifiedRefundAsync(refund,default);await store.ApplyAsync(proof,default);
        Assert.Equal("refunded",(await store.ReadIntentAsync(order.PaymentId,default))!.State);
    }
    [Fact]
    public async Task Delayed_pending_cannot_regress_success_or_refund_and_regrant()
    {
        await using var f=await ApiFixture.StartAsync();var a=await f.AccountAsync("google","ordered-payment");var store=f.Service<PaymentStore>();
        var order=await store.BeginAsync(a.Id,new("test.time30","yookassa",Guid.NewGuid(),false),default);
        await store.BindProviderReferenceAsync(order.PaymentId,"ordered-charge",default);
        var paid=new VerifiedPayment("yookassa","test","ordered-charge",order.PaymentId,a.Id,new(10000,"RUB"),"succeeded",f.Clock.GetUtcNow(),null,null);
        await store.ApplyAsync(paid,default);await store.ApplyAsync(paid with{Status="pending"},default);
        Assert.Equal("succeeded",(await store.ReadIntentAsync(order.PaymentId,default))!.State);
        await store.ApplyVerifiedRefundAsync(new("yookassa","test","ordered-refund","ordered-charge",order.PaymentId,a.Id,new(10000,"RUB"),f.Clock.GetUtcNow()),default);
        await store.ApplyAsync(paid with{Status="pending"},default);await store.ApplyAsync(paid,default);
        Assert.Equal("refunded",(await store.ReadIntentAsync(order.PaymentId,default))!.State);
    }
    [Fact]
    public async Task Late_subscription_projection_cannot_reactivate_refunded_initial_payment()
    {
        await using var f=await ApiFixture.StartAsync();var a=await f.AccountAsync("google","subscription-race");var payments=f.Service<PaymentStore>();
        var order=await payments.BeginAsync(a.Id,new("test.time30","yookassa",Guid.NewGuid(),true),default);await payments.BindProviderReferenceAsync(order.PaymentId,"subscription-charge",default);
        var paid=new VerifiedPayment("yookassa","test","subscription-charge",order.PaymentId,a.Id,new(10000,"RUB"),"succeeded",f.Clock.GetUtcNow(),"saved-method",f.Clock.GetUtcNow().AddDays(30));
        await payments.ApplyAsync(paid,default);
        await payments.ApplyVerifiedRefundAsync(new("yookassa","test","late-refund","subscription-charge",order.PaymentId,a.Id,new(10000,"RUB"),f.Clock.GetUtcNow()),default);
        await f.Service<SubscriptionService>().ProjectInitialAsync(paid,default);
        var subscription=await f.Service<SubscriptionStore>().ReadByOriginAsync(order.PaymentId,default);
        Assert.True(subscription is null || !subscription.AutoRenew);
    }
    [Fact]
    public async Task Stars_initial_refund_cancels_server_autorenew()
    {
        await using var f=await ApiFixture.StartAsync();var a=await f.AccountAsync("telegram","946012");var payments=f.Service<PaymentStore>();
        var order=await payments.BeginAsync(a.Id,new("test.time30","stars",Guid.NewGuid(),true),default);
        var paid=new VerifiedPayment("stars","test","stars-first",order.PaymentId,a.Id,new(100,"XTR"),"succeeded",f.Clock.GetUtcNow(),"stars-first",f.Clock.GetUtcNow().AddDays(30));
        await payments.ApplyAsync(paid,default);await f.Service<SubscriptionService>().ProjectInitialAsync(paid,default);
        await payments.ApplyAsync(paid with{Status="refunded"},default);
        var subscription=(await f.Service<SubscriptionStore>().ReadByOriginAsync(order.PaymentId,default))!;
        Assert.False(subscription.AutoRenew);
    }
}
