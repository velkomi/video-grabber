using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Payments;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class PromotionPaymentTests
{
    [Theory]
    [InlineData("yookassa")]
    [InlineData("stars")]
    public async Task Reserved_checkout_retry_uses_snapshot_after_product_is_removed(string provider)
    {
        await using var f = await Start();
        var buyer = await Primary(f, "removed-product-buyer");
        if (provider == "stars")
        {
            await using var connection = await f.Database.OpenConnectionAsync();
            await using var identity = new NpgsqlCommand("insert into licensing.identities(identity_id,account_id,issuer,provider,provider_subject) values(@i,@a,'https://telegram.broker.test/','telegram','942899')", connection);
            identity.Parameters.AddWithValue("i", Guid.NewGuid());
            identity.Parameters.AddWithValue("a", buyer.Id);
            await identity.ExecuteNonQueryAsync();
        }
        var quote = await Quote(buyer, provider);
        var request = new PurchaseRequest("plan.start30", provider, Guid.NewGuid(), false, quote.QuoteId);
        // The provider request can time out after the local reservation has committed.
        var reserved = await f.Service<PaymentStore>().BeginAsync(buyer.Id, request, default);
        var authorization = buyer.Client.DefaultRequestHeaders.Authorization;
        var changed = (await File.ReadAllTextAsync(f.PaymentCatalogPath!)).Replace("plan.start30", "plan.new30", StringComparison.Ordinal);
        await File.WriteAllTextAsync(f.PaymentCatalogPath!, changed);
        await f.RestartAsync();
        using var retryClient = f.TelegramWebClient();
        retryClient.DefaultRequestHeaders.Authorization = authorization;
        using var retry = await retryClient.PostAsJsonAsync("/v1/payments", request);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(reserved.PaymentId, (await retry.Content.ReadFromJsonAsync<PaymentCheckout>())!.PaymentId);
        var intent = (await f.Service<PaymentStore>().ReadIntentAsync(reserved.PaymentId, default))!;
        Assert.Equal(quote.Payable, intent.Amount);
        Assert.Equal("plan.start30", intent.ProductSnapshot!.Sku);
    }

    [Fact]
    public async Task Legacy_paid_source_cannot_merge_into_first_only_reserved_checkout_until_canceled()
    {
        await using var f = await Start();
        var source = await f.AccountAsync("google", "legacy-paid-merge");
        var target = await f.AccountAsync("email", "first-only-merge");
        using var admin = await f.AdminAsync();
        var owner = (await admin.GetFromJsonAsync<AccountProfile>("/v1/me"))!;
        await using (var connection = await f.Database.OpenConnectionAsync())
        {
            await using var mark = new NpgsqlCommand("update licensing.accounts set first_purchase_at=@now where account_id=@source", connection);
            mark.Parameters.AddWithValue("now", f.Clock.GetUtcNow());
            mark.Parameters.AddWithValue("source", source.Id);
            await mark.ExecuteNonQueryAsync();
        }
        await f.Service<PromotionStore>().SavePromoAsync(owner.AccountId,
            new PromoDefinition("FIRST10", 1000, null, "RUB", f.Clock.GetUtcNow().AddMinutes(-1),
                f.Clock.GetUtcNow().AddDays(1), 10, 1, 150000), default);
        using var quoted = await target.Client.PostAsJsonAsync("/v1/promotions/quote",
            new PromotionQuoteRequest("plan.start30", "yookassa", "FIRST10", false));
        quoted.EnsureSuccessStatusCode();
        var quote = (await quoted.Content.ReadFromJsonAsync<PromotionQuote>())!;
        using var created = await target.Client.PostAsJsonAsync("/v1/payments",
            new PurchaseRequest("plan.start30", "yookassa", Guid.NewGuid(), false, quote.QuoteId));
        created.EnsureSuccessStatusCode();
        var paymentId = (await created.Content.ReadFromJsonAsync<PaymentCheckout>())!.PaymentId;
        string Proof(string provider, string subject, string side) => f.Broker.Issue(
            f.Partition(provider).Issuer.AbsoluteUri, f.Partition(provider).Audience, provider, subject,
            $"merge:{source.Id:D}:{target.Id:D}:{side}", f.Clock.GetUtcNow().AddMinutes(5));
        var request = new MergeRequest(source.Id, target.Id, "legacy first-purchase protection",
            Proof("google", "legacy-paid-merge", "source"), Proof("email", "first-only-merge", "target"));
        using var blocked = await admin.PostAsJsonAsync("/v1/admin/accounts/merge", request);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("merge_requires_reconciliation",
            JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        var payments = f.Service<PaymentStore>();
        var intent = (await payments.ReadIntentAsync(paymentId, default))!;
        await payments.ApplyAsync(new VerifiedPayment("yookassa", "test", intent.ProviderPaymentId!,
            paymentId, target.Id, intent.Amount, "canceled", f.Clock.GetUtcNow(), null, null), default);
        using var allowed = await admin.PostAsJsonAsync("/v1/admin/accounts/merge", request);
        allowed.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("reserved")]
    [InlineData("succeeded")]
    [InlineData("refunded")]
    public async Task Account_merge_cannot_reset_repeatable_coupon_usage(string state)
    {
        await using var f = await Start();
        var source = await f.AccountAsync("google", "coupon-merge-source");
        var target = await f.AccountAsync("email", "coupon-merge-target");
        using var admin = await f.AdminAsync();
        var owner = (await admin.GetFromJsonAsync<AccountProfile>("/v1/me"))!;
        await f.Service<PromotionStore>().SavePromoAsync(owner.AccountId,
            new PromoDefinition("REPEAT10", 1000, null, "RUB", f.Clock.GetUtcNow().AddMinutes(-1),
                f.Clock.GetUtcNow().AddDays(1), 10, 1, 150000, FirstPurchaseOnly: false), default);
        using var quoted = await source.Client.PostAsJsonAsync("/v1/promotions/quote",
            new PromotionQuoteRequest("plan.start30", "yookassa", "REPEAT10", false));
        quoted.EnsureSuccessStatusCode();
        var quote = (await quoted.Content.ReadFromJsonAsync<PromotionQuote>())!;
        using var created = await source.Client.PostAsJsonAsync("/v1/payments",
            new PurchaseRequest("plan.start30", "yookassa", Guid.NewGuid(), false, quote.QuoteId));
        created.EnsureSuccessStatusCode();
        var paymentId = (await created.Content.ReadFromJsonAsync<PaymentCheckout>())!.PaymentId;
        var payments = f.Service<PaymentStore>();
        var intent = (await payments.ReadIntentAsync(paymentId, default))!;
        if (state != "reserved")
        {
            await payments.ApplyAsync(new VerifiedPayment("yookassa", "test", intent.ProviderPaymentId!,
                paymentId, source.Id, intent.Amount, "succeeded", f.Clock.GetUtcNow(), null, null), default);
            if (state == "refunded")
                await payments.ApplyVerifiedRefundAsync(new VerifiedRefund("yookassa", "test", "merge-refund",
                    intent.ProviderPaymentId!, paymentId, source.Id, intent.Amount, f.Clock.GetUtcNow()), default);
        }
        string Proof(string provider, string subject, string side) => f.Broker.Issue(
            f.Partition(provider).Issuer.AbsoluteUri, f.Partition(provider).Audience, provider, subject,
            $"merge:{source.Id:D}:{target.Id:D}:{side}", f.Clock.GetUtcNow().AddMinutes(5));
        using var merged = await admin.PostAsJsonAsync("/v1/admin/accounts/merge",
            new MergeRequest(source.Id, target.Id, "coupon usage regression",
                Proof("google", "coupon-merge-source", "source"), Proof("email", "coupon-merge-target", "target")));
        Assert.Equal(HttpStatusCode.Conflict, merged.StatusCode);
        Assert.Equal("merge_requires_reconciliation",
            JsonDocument.Parse(await merged.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var inspect = new NpgsqlCommand("select merged_into from licensing.accounts where account_id=@source", connection);
        inspect.Parameters.AddWithValue("source", source.Id);
        Assert.Equal(DBNull.Value, await inspect.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Telegram_guest_invitation_survives_real_account_link_and_cannot_be_self_claimed()
    {
        await using var f=await Start();var inviter=await Primary(f,"link-inviter");var buyer=await Primary(f,"link-target");
        const long telegramId=942885;
        var guest=await f.AccountAsync("telegram",telegramId.ToString(),includeStarter:true,telegramOnly:true);
        var store=f.Service<PromotionStore>();var code=(await store.SummaryAsync(inviter.Id,default)).Code;
        await store.ClaimAsync(guest.Id,code,default);
        var ticket=await f.Service<TelegramAccountLinkService>().BeginAsync(guest.Id,telegramId,default);
        var raw=ticket.LinkUri.Query.TrimStart('?').Split('&').Single(x=>x.StartsWith("telegram_link=",StringComparison.Ordinal)).Split('=',2)[1];
        using var link=await buyer.Client.PostAsJsonAsync("/v1/telegram/account-link/complete",new CompleteTelegramAccountLinkRequest(Uri.UnescapeDataString(raw)));
        Assert.Equal(HttpStatusCode.NoContent,link.StatusCode);
        Assert.Equal(135000,(await Quote(buyer,"yookassa")).Payable.MinorUnits);
        var self=(await store.SummaryAsync(buyer.Id,default)).Code;
        await Assert.ThrowsAsync<PromotionConflictException>(()=>store.ClaimAsync(buyer.Id,self,default));
    }

    [Fact]
    public async Task Yoo_refund_webhook_reverifies_provider_and_deduplicates_exact_partial_refund()
    {
        await using var f=await Start();var inviter=await Primary(f,"refund-inviter");var buyer=await Primary(f,"refund-buyer");
        var store=f.Service<PromotionStore>();await store.ClaimAsync(buyer.Id,(await store.SummaryAsync(inviter.Id,default)).Code,default);
        var quote=await Quote(buyer,"yookassa");
        using var create=await buyer.Client.PostAsJsonAsync("/v1/payments",new PurchaseRequest("plan.start30","yookassa",Guid.NewGuid(),false,quote.QuoteId));create.EnsureSuccessStatusCode();
        var checkout=(await create.Content.ReadFromJsonAsync<PaymentCheckout>())!;var intent=(await f.Service<PaymentStore>().ReadIntentAsync(checkout.PaymentId,default))!;
        f.YooKassaApi.SetPayment(intent.ProviderPaymentId!,new{ id=intent.ProviderPaymentId,status="succeeded",paid=true,test=true,amount=new{value="1350.00",currency="RUB"},metadata=new{order_id=intent.InvoicePayload},created_at=f.Clock.GetUtcNow().ToString("O")});
        using(var paid=await f.Anonymous.PostAsJsonAsync("/v1/payments/yookassa/webhook",new{type="notification",@event="payment.succeeded",@object=new{id=intent.ProviderPaymentId}}))paid.EnsureSuccessStatusCode();
        f.YooKassaApi.SetRefund("verified-refund",new{id="verified-refund",status="succeeded",payment_id=intent.ProviderPaymentId,amount=new{value="350.00",currency="RUB"}});
        for(var i=0;i<2;i++)
        { using var refund=await f.Anonymous.PostAsJsonAsync("/v1/payments/yookassa/webhook",new{type="notification",@event="refund.succeeded",@object=new{id="verified-refund",amount=new{value="99999.00",currency="RUB"}}});refund.EnsureSuccessStatusCode(); }
        Assert.Equal(10000,(await store.SummaryAsync(inviter.Id,default)).Balances.Single(x=>x.Currency=="RUB").PendingMinor);
        Assert.Contains(f.YooKassaApi.Requests,x=>x.Method=="GET"&&x.Path.EndsWith("/refunds/verified-refund",StringComparison.Ordinal));
    }
    [Fact]
    public async Task Server_quote_is_bound_to_account_and_invoice_and_callbacks_keep_frozen_price()
    {
        await using var f=await Start();
        var inviter=await Primary(f,"inviter");var buyer=await Primary(f,"buyer");var other=await Primary(f,"other");
        var store=f.Service<PromotionStore>();var code=(await store.SummaryAsync(inviter.Id,default)).Code;
        Assert.True(await store.ClaimAsync(buyer.Id,code,default));
        var quote=await Quote(buyer,"yookassa");
        Assert.Equal(135000,quote.Payable.MinorUnits);
        var order=new PurchaseRequest("plan.start30","yookassa",Guid.NewGuid(),false,quote.QuoteId);
        using(var stolen=await other.Client.PostAsJsonAsync("/v1/payments",order)) Assert.Equal(HttpStatusCode.Conflict,stolen.StatusCode);
        using var create=await buyer.Client.PostAsJsonAsync("/v1/payments",order);create.EnsureSuccessStatusCode();
        var checkout=(await create.Content.ReadFromJsonAsync<PaymentCheckout>())!;
        var request=Assert.Single(f.YooKassaApi.Requests,x=>x.Method=="POST"&&x.Path.EndsWith("/payments",StringComparison.Ordinal));
        Assert.Equal("1350.00",JsonDocument.Parse(request.Body).RootElement.GetProperty("amount").GetProperty("value").GetString());
        var payments=f.Service<PaymentStore>();var intent=(await payments.ReadIntentAsync(checkout.PaymentId,default))!;
        var changed=(await File.ReadAllTextAsync(f.PaymentCatalogPath!)).Replace("150000","600000",StringComparison.Ordinal);
        await File.WriteAllTextAsync(f.PaymentCatalogPath!,changed);await f.RestartAsync();payments=f.Service<PaymentStore>();
        // A catalog price change cannot invalidate an existing paid invoice.
        var snapshot=new VerifiedPayment("yookassa","test",intent.ProviderPaymentId!,checkout.PaymentId,buyer.Id,intent.Amount,"succeeded",f.Clock.GetUtcNow(),null,null);
        await payments.ApplyAsync(snapshot,default);await payments.ApplyAsync(snapshot,default);
        var summary=await store.SummaryAsync(inviter.Id,default);
        Assert.Equal(13500,summary.Balances.Single(x=>x.Currency=="RUB").PendingMinor);
        Assert.Equal(1,summary.Paid);
        f.PromotionsEnabled=false;await f.RestartAsync();
        var partial=new VerifiedRefund("yookassa","test","refund-a",intent.ProviderPaymentId!,checkout.PaymentId,buyer.Id,new Money(35000,"RUB"),f.Clock.GetUtcNow());
        await f.Service<PaymentStore>().ApplyVerifiedRefundAsync(partial,default);
        await f.Service<PaymentStore>().ApplyVerifiedRefundAsync(partial,default);
        await using var inspection=await f.Database.OpenConnectionAsync();
        await using var q=new NpgsqlCommand("select reward_minor from licensing.promotion_payments where payment_id=@p",inspection);
        q.Parameters.AddWithValue("p",checkout.PaymentId);
        Assert.Equal(10000L,await q.ExecuteScalarAsync());
        var full=partial with {RefundId="refund-b",Amount=new Money(100000,"RUB")};
        var final=await f.Service<PaymentStore>().ApplyVerifiedRefundAsync(full,default);
        Assert.Equal("refunded",final.Status);
        await f.Service<PaymentStore>().ApplyAsync(snapshot,default);
        Assert.Equal("refunded",(await f.Service<PaymentStore>().ReadIntentAsync(checkout.PaymentId,default))!.State);
    }

    [Fact]
    public async Task Discounted_stars_invoice_and_precheckout_use_quoted_integer_amount()
    {
        await using var f=await Start();var buyer=await Primary(f,"stars");
        await using(var c=await f.Database.OpenConnectionAsync())
        {
            await using var q=new NpgsqlCommand("insert into licensing.identities(identity_id,account_id,issuer,provider,provider_subject) values(@i,@a,'https://telegram.broker.test/','telegram','942881')",c);
            q.Parameters.AddWithValue("i",Guid.NewGuid());q.Parameters.AddWithValue("a",buyer.Id);await q.ExecuteNonQueryAsync();
        }
        var store=f.Service<PromotionStore>();var admin=await f.AccountAsync("google","promo-admin");
        await using(var c=await f.Database.OpenConnectionAsync())
        {await using var q=new NpgsqlCommand("update licensing.accounts set base_role='owner_admin' where account_id=@a",c);q.Parameters.AddWithValue("a",admin.Id);await q.ExecuteNonQueryAsync();}
        await store.SavePromoAsync(admin.Id,new PromoDefinition("SAVE20",2000,null,"XTR",f.Clock.GetUtcNow().AddDays(-1),f.Clock.GetUtcNow().AddDays(1),10,1,10000),default);
        using var qr=await buyer.Client.PostAsJsonAsync("/v1/promotions/quote",new PromotionQuoteRequest("plan.start30","stars","SAVE20",false));qr.EnsureSuccessStatusCode();
        var quote=(await qr.Content.ReadFromJsonAsync<PromotionQuote>())!;
        using var create=await buyer.Client.PostAsJsonAsync("/v1/payments",new PurchaseRequest("plan.start30","stars",Guid.NewGuid(),false,quote.QuoteId));create.EnsureSuccessStatusCode();
        var invoice=Assert.Single(f.TelegramApi.Requests,x=>x.Path.EndsWith("/createInvoiceLink",StringComparison.Ordinal));
        Assert.Equal(240,JsonDocument.Parse(invoice.Body).RootElement.GetProperty("prices")[0].GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task New_marketing_calls_are_disabled_by_default()
    {
        await using var f=await ApiFixture.StartAsync();var user=await f.AccountAsync("google","disabled-marketing");
        using var response=await user.Client.GetAsync("/v1/referrals");Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
    }
    [Fact]
    public async Task Anonymous_or_nonowner_cannot_create_coupons()
    {
        await using var f=await Start();var user=await Primary(f,"nonowner");
        var def=new PromoDefinition("UNSAFE10",1000,null,"RUB",f.Clock.GetUtcNow(),f.Clock.GetUtcNow().AddDays(1),10,1,50000);
        using var a=await f.Anonymous.PostAsJsonAsync("/v1/admin/promotions",def);Assert.Equal(HttpStatusCode.Unauthorized,a.StatusCode);
        using var u=await user.Client.PostAsJsonAsync("/v1/admin/promotions",def);Assert.Equal(HttpStatusCode.Forbidden,u.StatusCode);
    }

    private static async Task<ApiFixture> Start()
    {
        var f=await ApiFixture.StartAsync();f.PromotionsEnabled=true;
        var root=Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")!,"marketing-catalogs");Directory.CreateDirectory(root);
        var path=Path.Combine(root,Guid.NewGuid()+".json");
        await File.WriteAllTextAsync(path,"""
            {"version":"referral-test1","environment":"test","products":[
              {"sku":"plan.start30","kind":"time","planId":"start","credits":0,"days":30,"recurringAllowed":true,"prices":{"yookassa":{"minorUnits":150000,"currency":"RUB"},"stars":{"minorUnits":300,"currency":"XTR"}}}
            ]}
            """);
        f.PaymentCatalogPath=path;await f.RestartAsync();return f;
    }
    private static async Task<TestAccount> Primary(ApiFixture f,string subject)
    {
        return await f.AccountAsync("google",subject);
    }
    private static async Task<PromotionQuote> Quote(TestAccount a,string provider)
    {
        using var r=await a.Client.PostAsJsonAsync("/v1/promotions/quote",new PromotionQuoteRequest("plan.start30",provider,null,false));r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<PromotionQuote>())!;
    }
}
