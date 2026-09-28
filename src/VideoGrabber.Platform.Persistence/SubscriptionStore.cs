using System.Data;
using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class SubscriptionConflictException : Exception;

public sealed class SubscriptionStore(
    CreditLedger ledger,
    TimeProvider clock)
{
    private readonly NpgsqlDataSource _dataSource = ledger.DataSource;

    public Task<SubscriptionView?> ProjectInitialPaymentAsync(
        PaymentIntentRecord intent,
        VerifiedPayment payment,
        CancellationToken cancellationToken)
        => ProjectInitialPaymentAsync(intent, null, payment, cancellationToken);

    public async Task<SubscriptionView?> ProjectInitialPaymentAsync(
        PaymentIntentRecord intent,
        string? planId,
        VerifiedPayment payment,
        CancellationToken cancellationToken)
    {
        if (!intent.Recurring || payment.Status != "succeeded")
            return null;
        if (payment.PaidThrough is not DateTimeOffset paidThrough
            || paidThrough <= payment.OccurredAt)
            throw new SubscriptionConflictException();
        paidThrough = NormalizeTime(paidThrough);
        var providerReference = payment.Provider switch
        {
            "stars" => payment.ProviderPaymentId,
            "yookassa" => payment.SubscriptionId
                ?? throw new SubscriptionConflictException(),
            _ => throw new SubscriptionConflictException()
        };
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, intent.AccountId, cancellationToken);

        var existing = await ReadByOriginForUpdateAsync(
            connection, transaction, intent.PaymentId, cancellationToken);
        if (existing is not null)
        {
            if (existing.AccountId != intent.AccountId
                || existing.Provider != payment.Provider
                || (planId is not null && existing.PlanId != planId))
                throw new SubscriptionConflictException();
            if (paidThrough > existing.PaidThrough)
                await UpdatePaidThroughAsync(
                    connection, transaction, existing.SubscriptionId,
                    paidThrough, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await ReadRequiredAsync(
                intent.AccountId, existing.SubscriptionId, cancellationToken);
        }

        var subscriptionId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            insert into licensing.subscriptions(
              subscription_id,account_id,origin_payment_id,provider,environment,
              provider_reference,state,auto_renew,paid_through,plan_id,created_at,updated_at)
            values(@subscription,@account,@payment,@provider,@environment,
              @reference,'active',true,@paid_through,@plan_id,@now,@now)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("subscription", subscriptionId);
            insert.Parameters.AddWithValue("account", intent.AccountId);
            insert.Parameters.AddWithValue("payment", intent.PaymentId);
            insert.Parameters.AddWithValue("provider", payment.Provider);
            insert.Parameters.AddWithValue("environment", payment.Environment);
            insert.Parameters.AddWithValue("reference", providerReference);
            insert.Parameters.AddWithValue("paid_through", paidThrough);
            insert.Parameters.AddWithValue("plan_id", (object?)planId ?? DBNull.Value);
            insert.Parameters.AddWithValue("now", now);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new SubscriptionConflictException();
            }
        }

        await InsertEventAsync(
            connection, transaction,
            subscriptionId,
            "initial:" + payment.ProviderPaymentId,
            "initial",
            payment.Provider,
            payment.Environment,
            payment.ProviderPaymentId,
            payment.OccurredAt,
            paidThrough,
            payment.Amount,
            payment.OccurredAt,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new SubscriptionView(
            subscriptionId, intent.AccountId, "active", true, paidThrough, payment.Provider)
        {
            PlanId = planId
        };
    }

    public async Task<SubscriptionView> ApplyRenewalAsync(
        RenewalEvent renewal,
        CancellationToken cancellationToken)
    {
        ValidateRenewal(renewal);
        renewal = renewal with
        {
            PeriodStart = NormalizeTime(renewal.PeriodStart),
            PeriodEnd = NormalizeTime(renewal.PeriodEnd)
        };
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var row = await ReadForUpdateAsync(
            connection, transaction, renewal.SubscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        if (row.Provider != renewal.Provider
            || row.Environment != renewal.Environment)
            throw new SubscriptionConflictException();

        var inserted = await InsertEventAsync(
            connection, transaction,
            renewal.SubscriptionId,
            "renewal:" + renewal.ChargeId,
            "renewal",
            renewal.Provider,
            renewal.Environment,
            renewal.ChargeId,
            renewal.PeriodStart,
            renewal.PeriodEnd,
            renewal.Amount,
            renewal.PeriodEnd,
            now,
            cancellationToken);
        if (!inserted)
        {
            await transaction.CommitAsync(cancellationToken);
            return row.ToView();
        }

        await CreateRenewalGrantAsync(
            connection, transaction, row.AccountId, row.PlanId, renewal, now, cancellationToken);

        var nextPaidThrough = renewal.PeriodEnd > row.PaidThrough
            ? renewal.PeriodEnd
            : row.PaidThrough;
        await UpdatePaidThroughAsync(
            connection, transaction, row.SubscriptionId,
            nextPaidThrough, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await ReadRequiredAsync(
            row.AccountId, row.SubscriptionId, cancellationToken);
    }

    public async Task<SubscriptionView> ApplyRenewalByOriginPaymentAsync(
        Guid paymentId,
        string chargeId,
        DateTimeOffset periodEnd,
        Money amount,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var row = await ReadByOriginAsync(paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        var periodStart = periodEnd.AddDays(-30);
        return await ApplyRenewalAsync(
            new RenewalEvent(
                row.Provider,
                row.Environment,
                chargeId,
                row.SubscriptionId,
                periodStart,
                periodEnd,
                amount),
            cancellationToken);
    }

    public async Task<SubscriptionView> RequestCancelAsync(
        Guid accountId,
        Guid subscriptionId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || subscriptionId == Guid.Empty
            || idempotencyKey == Guid.Empty)
            throw new ArgumentException("Cancellation request is incomplete.");
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);

        var row = await ReadForUpdateAsync(
            connection, transaction, subscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        if (row.AccountId != accountId)
            throw new KeyNotFoundException("Subscription was not found.");
        if (row.State == "canceled")
        {
            await transaction.CommitAsync(cancellationToken);
            return row.ToView();
        }

        await InsertEventAsync(
            connection, transaction,
            subscriptionId,
            "cancel-requested:" + idempotencyKey.ToString("N"),
            "cancel_requested",
            row.Provider,
            row.Environment,
            row.ProviderReference,
            null,
            null,
            new Money(0, "XXX"),
            now,
            now,
            cancellationToken);

        await using var update = new NpgsqlCommand("""
            update licensing.subscriptions
            set state='cancel_pending',auto_renew=false,updated_at=@now
            where subscription_id=@subscription and account_id=@account
            """, connection, transaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("subscription", subscriptionId);
        update.Parameters.AddWithValue("account", accountId);
        await update.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new SubscriptionView(
            subscriptionId, accountId, "cancel_pending",
            false, row.PaidThrough, row.Provider)
        {
            PlanId = row.PlanId
        };
    }

    public async Task<SubscriptionView> ConfirmCancelAsync(
        Guid accountId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var row = await ReadForUpdateAsync(
            connection, transaction, subscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        if (row.AccountId != accountId)
            throw new KeyNotFoundException("Subscription was not found.");

        await InsertEventAsync(
            connection, transaction,
            subscriptionId,
            "cancel-confirmed:" + row.ProviderReference,
            "cancel_confirmed",
            row.Provider,
            row.Environment,
            row.ProviderReference,
            null,
            null,
            new Money(0, "XXX"),
            now,
            now,
            cancellationToken);

        await using var update = new NpgsqlCommand("""
            update licensing.subscriptions
            set state='canceled',auto_renew=false,updated_at=@now
            where subscription_id=@subscription and account_id=@account
            """, connection, transaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("subscription", subscriptionId);
        update.Parameters.AddWithValue("account", accountId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubscriptionView(
            subscriptionId, accountId, "canceled",
            false, row.PaidThrough, row.Provider)
        {
            PlanId = row.PlanId
        };
    }

    public async Task<SubscriptionView> ApplyTerminalAdjustmentAsync(
        Guid subscriptionId,
        string chargeId,
        string kind,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (kind is not ("refund" or "chargeback"))
            throw new ArgumentException("Unsupported terminal adjustment.", nameof(kind));
        var now = clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var row = await ReadForUpdateAsync(
            connection, transaction, subscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");

        var sourceReference = await FindRenewalSourceReferenceAsync(
            connection, transaction, subscriptionId, chargeId, cancellationToken);
        if (sourceReference is not null)
        {
            await RevokeGrantAsync(
                connection, transaction, row.AccountId,
                sourceReference, chargeId, now, cancellationToken);
        }

        await InsertEventAsync(
            connection, transaction,
            subscriptionId,
            kind + ":" + chargeId,
            kind,
            row.Provider,
            row.Environment,
            chargeId,
            null,
            null,
            new Money(0, "XXX"),
            occurredAt,
            now,
            cancellationToken);

        if (kind == "chargeback")
        {
            await using var update = new NpgsqlCommand("""
                update licensing.subscriptions
                set state='review_required',auto_renew=false,updated_at=@now
                where subscription_id=@subscription
                """, connection, transaction);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("subscription", subscriptionId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await ReadRequiredAsync(
            row.AccountId, row.SubscriptionId, cancellationToken);
    }

    public async Task<SubscriptionRecord?> ReadRecordAsync(
        Guid accountId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where account_id=@account and subscription_id=@subscription
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
    }
    public async Task<SubscriptionView?> ReadAsync(
        Guid accountId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where account_id=@account and subscription_id=@subscription
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRow(reader).ToView()
            : null;
    }

    public async Task<IReadOnlyList<SubscriptionView>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where account_id=@account
            order by created_at,subscription_id
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SubscriptionView>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRow(reader).ToView());
        return result;
    }

    public async Task<SubscriptionRecord?> ReadRecordAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where subscription_id=@subscription
            """, connection);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
    }
    public async Task<SubscriptionRecord?> ReadByOriginAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where origin_payment_id=@payment
            """, connection);
        command.Parameters.AddWithValue("payment", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRow(reader)
            : null;
    }

    private async Task<SubscriptionView> ReadRequiredAsync(
        Guid accountId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
        => await ReadAsync(accountId, subscriptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Subscription was not found.");

    private static async Task<SubscriptionRecord?> ReadForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where subscription_id=@subscription
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRow(reader)
            : null;
    }

    private static async Task<SubscriptionRecord?> ReadByOriginForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select subscription_id,account_id,provider,environment,provider_reference,
                   state,auto_renew,paid_through,origin_payment_id,plan_id
            from licensing.subscriptions
            where origin_payment_id=@payment
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("payment", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRow(reader)
            : null;
    }

    private static async Task UpdatePaidThroughAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid subscriptionId,
        DateTimeOffset paidThrough,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var update = new NpgsqlCommand("""
            update licensing.subscriptions
            set paid_through=greatest(paid_through,@paid_through),
                updated_at=@now
            where subscription_id=@subscription
            """, connection, transaction);
        update.Parameters.AddWithValue("paid_through", paidThrough);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("subscription", subscriptionId);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid subscriptionId,
        string eventKey,
        string kind,
        string provider,
        string environment,
        string chargeId,
        DateTimeOffset? periodStart,
        DateTimeOffset? periodEnd,
        Money amount,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.subscription_events(
              event_id,subscription_id,event_key,event_kind,provider,environment,
              charge_id,period_start,period_end,amount_minor,currency,occurred_at,created_at)
            values(@event,@subscription,@key,@kind,@provider,@environment,
              @charge,@start,@end,@amount,@currency,@occurred,@now)
            on conflict do nothing
            """, connection, transaction);
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("subscription", subscriptionId);
        command.Parameters.AddWithValue("key", eventKey);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("environment", environment);
        command.Parameters.AddWithValue("charge", chargeId);
        command.Parameters.AddWithValue("start", (object?)periodStart ?? DBNull.Value);
        command.Parameters.AddWithValue("end", (object?)periodEnd ?? DBNull.Value);
        command.Parameters.AddWithValue("amount", amount.MinorUnits);
        command.Parameters.AddWithValue("currency", amount.Currency);
        command.Parameters.AddWithValue("occurred", occurredAt);
        command.Parameters.AddWithValue("now", now);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (inserted) return true;
        await using var verify = new NpgsqlCommand("""
            select event_kind,provider,environment,charge_id,period_start,period_end,amount_minor,currency
            from licensing.subscription_events
            where subscription_id=@subscription and event_key=@key
            """, connection, transaction);
        verify.Parameters.AddWithValue("subscription", subscriptionId);
        verify.Parameters.AddWithValue("key", eventKey);
        await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new SubscriptionConflictException();
        var exact = reader.GetString(0) == kind
            && reader.GetString(1) == provider
            && reader.GetString(2) == environment
            && reader.GetString(3) == chargeId
            && (reader.IsDBNull(4) ? periodStart is null : reader.GetFieldValue<DateTimeOffset>(4) == periodStart)
            && (reader.IsDBNull(5) ? periodEnd is null : reader.GetFieldValue<DateTimeOffset>(5) == periodEnd)
            && reader.GetInt64(6) == amount.MinorUnits
            && reader.GetString(7) == amount.Currency;
        return exact ? false : throw new SubscriptionConflictException();
    }
    private static async Task CreateRenewalGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string? planId,
        RenewalEvent renewal,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceReference =
            "subscription:" + renewal.SubscriptionId.ToString("D")
            + ":" + renewal.PeriodStart.ToUnixTimeSeconds();
        await using var insert = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,source_reference,plan_id,
              valid_from,valid_until,available,reserved,original_amount,
              reason,created_at)
            values(@grant,@account,'time','purchase',@source_reference,@plan_id,
              @from,@until,0,0,0,@reason,@now)
            on conflict(source_reference)
              where source='purchase' and source_reference is not null
            do nothing
            """, connection, transaction);
        insert.Parameters.AddWithValue("grant", Guid.NewGuid());
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("source_reference", sourceReference);
        insert.Parameters.AddWithValue("plan_id", (object?)planId ?? DBNull.Value);
        insert.Parameters.AddWithValue("from", renewal.PeriodStart);
        insert.Parameters.AddWithValue("until", renewal.PeriodEnd);
        insert.Parameters.AddWithValue("reason", "subscription renewal");
        insert.Parameters.AddWithValue("now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> FindRenewalSourceReferenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid subscriptionId,
        string chargeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select period_start
            from licensing.subscription_events
            where subscription_id=@subscription
              and event_kind='renewal'
              and charge_id=@charge
            order by occurred_at desc
            limit 1
            """, connection, transaction);
        command.Parameters.AddWithValue("subscription", subscriptionId);
        command.Parameters.AddWithValue("charge", chargeId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        DateTimeOffset? start = value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => null
        };
        return start is DateTimeOffset timestamp
            ? "subscription:" + subscriptionId.ToString("D")
              + ":" + timestamp.ToUnixTimeSeconds()
            : null;    }

    private static async Task RevokeGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string sourceReference,
        string chargeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            update licensing.entitlement_grants
            set revoked_at=coalesce(revoked_at,@now)
            where account_id=@account
              and source='purchase'
              and source_reference=@reference
            """, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("reference", sourceReference);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _ = chargeId;
    }

    private static async Task LockAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@account,20260919))",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset NormalizeTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
    private static void ValidateRenewal(RenewalEvent renewal)
    {
        if (renewal.SubscriptionId == Guid.Empty
            || string.IsNullOrWhiteSpace(renewal.Provider)
            || string.IsNullOrWhiteSpace(renewal.Environment)
            || string.IsNullOrWhiteSpace(renewal.ChargeId)
            || renewal.PeriodEnd <= renewal.PeriodStart
            || renewal.Amount.MinorUnits <= 0)
            throw new ArgumentException("Renewal event is invalid.");
    }

    private static SubscriptionRecord ReadRow(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetBoolean(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetGuid(8),
            reader.IsDBNull(9) ? null : reader.GetString(9));
}

public sealed record SubscriptionRecord(
    Guid SubscriptionId,
    Guid AccountId,
    string Provider,
    string Environment,
    string ProviderReference,
    string State,
    bool AutoRenew,
    DateTimeOffset PaidThrough,
    Guid OriginPaymentId,
    string? PlanId)
{
    public SubscriptionView ToView()
        => new(
            SubscriptionId,
            AccountId,
            State,
            AutoRenew,
            PaidThrough,
            Provider)
        {
            PlanId = PlanId
        };
}
