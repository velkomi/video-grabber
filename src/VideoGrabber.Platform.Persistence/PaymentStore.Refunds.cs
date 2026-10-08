using System.Data;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;

namespace VideoGrabber.Platform.Persistence;

public sealed partial class PaymentStore
{
    public async Task<PaymentIntentRecord?> ReadIntentByProviderAsync(string provider,string providerId,CancellationToken ct)
    {
        await using var c=await _dataSource.OpenConnectionAsync(ct);
        await using var q=new NpgsqlCommand("""
            select payment_id,account_id,provider,environment,sku,expected_minor,expected_currency,recurring,state,
              provider_payment_id,invoice_payload,product_snapshot
            from licensing.payments where provider=@provider and environment=@env and provider_payment_id=@id
            """,c);
        q.Parameters.AddWithValue("provider",provider);q.Parameters.AddWithValue("env",Environment);q.Parameters.AddWithValue("id",providerId);
        await using var r=await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadIntent(r) : null;
    }

    public async Task<PaymentView> ApplyVerifiedRefundAsync(VerifiedRefund refund,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(refund);
        if (refund.Amount.MinorUnits<=0 || string.IsNullOrWhiteSpace(refund.RefundId) || refund.RefundId.Length>128)
            throw new ArgumentException("invalid_refund");
        await using var c=await _dataSource.OpenConnectionAsync(ct);
        await using var tx=await c.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        if (_promotions is not null) await _promotions.LockPaymentAccountsAsync(c,tx,refund.AccountId,refund.PaymentId,ct);
        await LockAccountAsync(c,tx,refund.AccountId,ct);
        var row=await ReadPaymentForUpdateAsync(c,tx,refund.PaymentId,ct) ?? throw new KeyNotFoundException();
        if (row.AccountId!=refund.AccountId || row.Provider!=refund.Provider || row.Environment!=refund.Environment
            || row.ProviderPaymentId!=refund.ProviderPaymentId || row.ExpectedCurrency!=refund.Amount.Currency) throw new PaymentConflictException();
        if (row.State is "pending" or "reconcile_required")
        {
            var proof=refund.OriginalPayment;
            var product=row.ProductSnapshot ?? Catalog.RequireProduct(row.Sku,row.Provider,row.Recurring);
            if (proof is null || proof.Status!="succeeded" || proof.Provider!=row.Provider || proof.ProviderPaymentId!=row.ProviderPaymentId
                || proof.PaymentId!=row.PaymentId || proof.AccountId!=row.AccountId) throw new PaymentConflictException();
            PaymentProjection.ValidateVerified(proof,row.Environment,product,new CatalogPrice(row.ExpectedMinor,row.ExpectedCurrency));
            await using var first=new NpgsqlCommand("select first_purchase_at is null from licensing.accounts where account_id=@a",c,tx);
            first.Parameters.AddWithValue("a",row.AccountId);
            var isFirst=(bool)(await first.ExecuteScalarAsync(ct))!;
            if (_promotions is not null) await _promotions.SuccessAsync(c,tx,proof,product,isFirst,ct);
            await ApplyPurchaseGrantAsync(c,tx,row,product,proof,_clock.GetUtcNow(),ct);
            await SetPaymentStateAsync(c,tx,row.PaymentId,"succeeded",_clock.GetUtcNow(),ct);
            row=row with {State="succeeded"};
        }
        if (row.State is not ("succeeded" or "refund_pending" or "refunded")) throw new PaymentConflictException();
        await using (var existing=new NpgsqlCommand("""
            select payment_id,account_id,amount_minor,currency from licensing.verified_refunds
            where provider=@provider and environment=@env and refund_id=@id
            """,c,tx))
        {
            existing.Parameters.AddWithValue("provider",refund.Provider);existing.Parameters.AddWithValue("env",refund.Environment);existing.Parameters.AddWithValue("id",refund.RefundId);
            await using var r=await existing.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                if (r.GetGuid(0)!=refund.PaymentId || r.GetGuid(1)!=refund.AccountId || r.GetInt64(2)!=refund.Amount.MinorUnits || r.GetString(3)!=refund.Amount.Currency)
                    throw new PaymentConflictException();
                await r.DisposeAsync();await tx.CommitAsync(ct);return row.ToView();
            }
        }
        await using var sum=new NpgsqlCommand("select coalesce(sum(amount_minor),0)::bigint from licensing.verified_refunds where payment_id=@p",c,tx);
        sum.Parameters.AddWithValue("p",refund.PaymentId);
        var total=checked((long)(await sum.ExecuteScalarAsync(ct))!+refund.Amount.MinorUnits);
        if (total>row.ExpectedMinor || row.State=="refunded") throw new PaymentConflictException();
        await using var insert=new NpgsqlCommand("""
            insert into licensing.verified_refunds(provider,environment,refund_id,payment_id,account_id,provider_payment_id,amount_minor,currency,occurred_at)
            values(@provider,@env,@id,@p,@a,@charge,@amount,@currency,@at)
            """,c,tx);
        insert.Parameters.AddWithValue("provider",refund.Provider);insert.Parameters.AddWithValue("env",refund.Environment);insert.Parameters.AddWithValue("id",refund.RefundId);
        insert.Parameters.AddWithValue("p",refund.PaymentId);insert.Parameters.AddWithValue("a",refund.AccountId);insert.Parameters.AddWithValue("charge",refund.ProviderPaymentId);
        insert.Parameters.AddWithValue("amount",refund.Amount.MinorUnits);insert.Parameters.AddWithValue("currency",refund.Amount.Currency);insert.Parameters.AddWithValue("at",refund.OccurredAt);
        try { await insert.ExecuteNonQueryAsync(ct); }
        catch (PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation) { throw new PaymentConflictException(); }
        var full=total==row.ExpectedMinor;
        if (_promotions is not null) await _promotions.RefundAsync(c,tx,refund.PaymentId,"provider:"+refund.RefundId,total,row.ExpectedMinor,full,ct);
        if (full)
        {
            await ApplyRefundProjectionAsync(c,tx,row,_clock.GetUtcNow(),ct);
            await CancelRefundedSubscriptionAsync(c,tx,row.PaymentId,_clock.GetUtcNow(),ct);
            await SetPaymentStateAsync(c,tx,row.PaymentId,"refunded",_clock.GetUtcNow(),ct);
        }
        _fault?.Invoke("before_refund_commit");
        await tx.CommitAsync(ct);
        return await ReadRequiredAsync(refund.AccountId,refund.PaymentId,ct);
    }

    private static async Task CancelRefundedSubscriptionAsync(NpgsqlConnection c,NpgsqlTransaction tx,Guid paymentId,DateTimeOffset now,CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("update licensing.subscriptions set auto_renew=false,state='canceled',updated_at=@now where origin_payment_id=@p",c,tx);
        q.Parameters.AddWithValue("p",paymentId);q.Parameters.AddWithValue("now",now);await q.ExecuteNonQueryAsync(ct);
    }
}
