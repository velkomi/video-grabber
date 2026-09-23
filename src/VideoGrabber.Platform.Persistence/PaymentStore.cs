using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Payments;

namespace VideoGrabber.Platform.Persistence;

public sealed class PaymentConflictException : Exception;
public sealed class PaymentDisabledException : Exception;

public sealed class PaymentStore
{
    private readonly CreditLedger _ledger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _clock;
    private readonly PaymentCatalog? _catalog;
    private readonly Action<string>? _fault;

    public PaymentStore(
        CreditLedger ledger,
        TimeProvider clock,
        PaymentCatalog? catalog = null)
        : this(ledger, clock, catalog, null)
    {
    }

    private PaymentStore(
        CreditLedger ledger,
        TimeProvider clock,
        PaymentCatalog? catalog,
        Action<string>? fault)
    {
        _ledger = ledger;
        _dataSource = ledger.DataSource;
        _clock = clock;
        _catalog = catalog;
        _fault = fault;
    }

    public static PaymentStore CreateForTesting(
        CreditLedger ledger,
        TimeProvider clock,
        PaymentCatalog catalog,
        Action<string>? fault = null)
        => new(ledger, clock, catalog, fault);

    public string Environment
        => Catalog.Environment;

    public PaymentCatalog Catalog
        => _catalog ?? throw new PaymentDisabledException();

    public async Task<PaymentCheckout> BeginAsync(
        Guid accountId,
        PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account is required.", nameof(accountId));
        var catalog = Catalog;
        var product = catalog.RequireProduct(
            request.Sku, request.Provider, request.Recurring);
        var price = product.Prices[request.Provider];
        PaymentProjection.ValidateMoney(
            new Money(price.MinorUnits, price.Currency));
        var requestHash = PaymentProjection.RequestHash(request);
        var now = _clock.GetUtcNow();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(
            connection, transaction, accountId, cancellationToken);

        await using (var existing = new NpgsqlCommand("""
            select payment_id,sku,request_hash,state,invoice_payload
            from licensing.payments
            where account_id=@account and provider=@provider
              and idempotency_key=@key
            for update
            """, connection, transaction))
        {
            existing.Parameters.AddWithValue("account", accountId);
            existing.Parameters.AddWithValue("provider", request.Provider);
            existing.Parameters.AddWithValue("key", request.IdempotencyKey);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var paymentId = reader.GetGuid(0);
                var sku = reader.GetString(1);
                var storedHash = reader.GetString(2);
                var state = reader.GetString(3);
                var payload = reader.IsDBNull(4) ? null : reader.GetString(4);
                await reader.DisposeAsync();
                if (!string.Equals(sku, request.Sku, StringComparison.Ordinal)
                    || !string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                    throw new PaymentConflictException();
                await transaction.CommitAsync(cancellationToken);
                return new PaymentCheckout(paymentId, state, null, payload);
            }
            await reader.DisposeAsync();
        }

        var paymentIdNew = Guid.NewGuid();
        var invoicePayload = "ord_" + Base64Url(
            RandomNumberGenerator.GetBytes(24));
        await using var insert = new NpgsqlCommand("""
            insert into licensing.payments(
              payment_id,account_id,provider,environment,sku,catalog_version,
              expected_minor,expected_currency,recurring,idempotency_key,
              request_hash,invoice_payload,state,created_at,updated_at)
            values(@payment,@account,@provider,@environment,@sku,@catalog,
              @minor,@currency,@recurring,@key,@hash,@payload,'pending',@now,@now)
            """, connection, transaction);
        insert.Parameters.AddWithValue("payment", paymentIdNew);
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("provider", request.Provider);
        insert.Parameters.AddWithValue("environment", catalog.Environment);
        insert.Parameters.AddWithValue("sku", request.Sku);
        insert.Parameters.AddWithValue("catalog", catalog.Version);
        insert.Parameters.AddWithValue("minor", price.MinorUnits);
        insert.Parameters.AddWithValue("currency", price.Currency);
        insert.Parameters.AddWithValue("recurring", request.Recurring);
        insert.Parameters.AddWithValue("key", request.IdempotencyKey);
        insert.Parameters.AddWithValue("hash", requestHash);
        insert.Parameters.AddWithValue("payload", invoicePayload);
        insert.Parameters.AddWithValue("now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentCheckout(
            paymentIdNew, "pending", null, invoicePayload);
    }

    public async Task<PaymentView> ApplyAsync(
        VerifiedPayment payment,
        CancellationToken cancellationToken)
    {
        if (payment is null) throw new ArgumentNullException(nameof(payment));
        var catalog = Catalog;
        var now = _clock.GetUtcNow();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        await LockAccountAsync(
            connection, transaction, payment.AccountId, cancellationToken);
        var row = await ReadPaymentForUpdateAsync(
            connection, transaction, payment.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment was not found.");

        if (row.AccountId != payment.AccountId
            || !string.Equals(row.Provider, payment.Provider, StringComparison.Ordinal))
            throw new PaymentConflictException();
        if (!catalog.Products.TryGetValue(row.Sku, out var product)
            || !product.Prices.TryGetValue(row.Provider, out var price))
            throw new PaymentConflictException();
        try
        {
            PaymentProjection.ValidateVerified(
                payment, row.Environment, product, price);
        }
        catch (Exception ex) when (
            ex is ArgumentException
            or InvalidOperationException
            or ArgumentOutOfRangeException)
        {
            throw new PaymentConflictException();
        }

        if (!string.IsNullOrWhiteSpace(row.ProviderPaymentId)
            && !string.Equals(
                row.ProviderPaymentId,
                payment.ProviderPaymentId,
                StringComparison.Ordinal))
            throw new PaymentConflictException();

        var eventKey = string.Join(
            ":",
            payment.Provider,
            payment.Environment,
            payment.ProviderPaymentId,
            payment.Status);
        var eventInserted = await InsertEventAsync(
            connection, transaction, row, payment, eventKey, now, cancellationToken);
        if (!eventInserted)
        {
            await transaction.CommitAsync(cancellationToken);
            return row.ToView();
        }

        try
        {
            await using var bind = new NpgsqlCommand("""
                update licensing.payments
                set provider_payment_id=coalesce(provider_payment_id,@provider_id),
                    subscription_id=coalesce(@subscription,subscription_id),
                    paid_through=coalesce(@paid_through,paid_through),
                    updated_at=@now
                where payment_id=@payment
                """, connection, transaction);
            bind.Parameters.AddWithValue(
                "provider_id", payment.ProviderPaymentId);
            bind.Parameters.AddWithValue(
                "subscription", (object?)payment.SubscriptionId ?? DBNull.Value);
            bind.Parameters.AddWithValue(
                "paid_through", (object?)payment.PaidThrough ?? DBNull.Value);
            bind.Parameters.AddWithValue("now", now);
            bind.Parameters.AddWithValue("payment", payment.PaymentId);
            await bind.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new PaymentConflictException();
        }

        _fault?.Invoke("after_payment_event");

        switch (payment.Status)
        {
            case "pending":
                await SetPaymentStateAsync(
                    connection, transaction, payment.PaymentId,
                    "pending", now, cancellationToken);
                break;
            case "canceled":
                if (row.State is not ("succeeded" or "refunded"))
                    await SetPaymentStateAsync(
                        connection, transaction, payment.PaymentId,
                        "canceled", now, cancellationToken);
                break;
            case "succeeded":
                if (row.State != "refunded")
                {
                    await ApplyPurchaseGrantAsync(
                        connection, transaction, row, product, payment, now,
                        cancellationToken);
                    await SetPaymentStateAsync(
                        connection, transaction, payment.PaymentId,
                        "succeeded", now, cancellationToken);
                }
                break;
            case "refunded":
                await ApplyRefundProjectionAsync(
                    connection, transaction, row, now, cancellationToken);
                await SetPaymentStateAsync(
                    connection, transaction, payment.PaymentId,
                    "refunded", now, cancellationToken);
                break;
            default:
                throw new PaymentConflictException();
        }

        _fault?.Invoke("before_payment_commit");
        await transaction.CommitAsync(cancellationToken);
        return await ReadRequiredAsync(
            payment.AccountId, payment.PaymentId, cancellationToken);
    }

    public async Task BindProviderReferenceAsync(
        Guid paymentId,
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);
        if (providerPaymentId.Length > 128)
            throw new ArgumentException("Provider payment ID is too long.");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("""
                update licensing.payments
                set provider_payment_id=coalesce(provider_payment_id,@provider_id),
                    updated_at=@now
                where payment_id=@payment
                  and (provider_payment_id is null or provider_payment_id=@provider_id)
                returning payment_id
                """, connection);
            command.Parameters.AddWithValue("provider_id", providerPaymentId);
            command.Parameters.AddWithValue("now", _clock.GetUtcNow());
            command.Parameters.AddWithValue("payment", paymentId);
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
                throw new PaymentConflictException();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new PaymentConflictException();
        }
    }
    public async Task<PaymentView?> ReadAsync(
        Guid accountId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select payment_id,state,expected_minor,expected_currency,sku
            from licensing.payments
            where account_id=@account and payment_id=@payment
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("payment", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new PaymentView(
            reader.GetGuid(0),
            reader.GetString(1),
            new Money(reader.GetInt64(2), reader.GetString(3)),
            reader.GetString(4));
    }

    public async Task<IReadOnlyList<PaymentView>> ListAsync(
        Guid accountId,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select payment_id,state,expected_minor,expected_currency,sku
            from licensing.payments
            where account_id=@account
            order by created_at desc,payment_id desc
            limit @limit
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PaymentView>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new PaymentView(
                reader.GetGuid(0), reader.GetString(1),
                new Money(reader.GetInt64(2), reader.GetString(3)),
                reader.GetString(4)));
        return result;
    }
    public async Task<PaymentIntentRecord?> ReadIntentByInvoiceAsync(
        string invoicePayload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoicePayload)) return null;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select payment_id,account_id,provider,environment,sku,
                   expected_minor,expected_currency,recurring,state,
                   provider_payment_id,invoice_payload
            from licensing.payments
            where invoice_payload=@payload
            """, connection);
        command.Parameters.AddWithValue("payload", invoicePayload);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadIntent(reader)
            : null;
    }

    public async Task<PaymentIntentRecord?> ReadIntentAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select payment_id,account_id,provider,environment,sku,
                   expected_minor,expected_currency,recurring,state,
                   provider_payment_id,invoice_payload
            from licensing.payments
            where payment_id=@payment
            """, connection);
        command.Parameters.AddWithValue("payment", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadIntent(reader)
            : null;
    }

    public async Task<PaymentView> MarkRefundPendingAsync(
        Guid accountId,
        RefundRequest request,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || request.PaymentId == Guid.Empty
            || request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Refund request is incomplete.");
        var now = _clock.GetUtcNow();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken);
        var row = await ReadPaymentForUpdateAsync(connection, transaction, request.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment was not found.");
        if (row.AccountId != accountId || row.State != "succeeded"
            || string.IsNullOrWhiteSpace(row.ProviderPaymentId))
            throw new PaymentConflictException();
        var eventKey = "refund-requested:" + request.IdempotencyKey.ToString("N");
        await using (var evt = new NpgsqlCommand("""
            insert into licensing.payment_events(
              event_id,payment_id,account_id,event_key,provider,environment,
              provider_payment_id,status,amount_minor,currency,occurred_at,created_at)
            values(@event,@payment,@account,@key,@provider,@environment,
              @provider_id,'refund_requested',@amount,@currency,@now,@now)
            on conflict(payment_id,event_key) do nothing
            """, connection, transaction))
        {
            evt.Parameters.AddWithValue("event", Guid.NewGuid());
            evt.Parameters.AddWithValue("payment", row.PaymentId);
            evt.Parameters.AddWithValue("account", row.AccountId);
            evt.Parameters.AddWithValue("key", eventKey);
            evt.Parameters.AddWithValue("provider", row.Provider);
            evt.Parameters.AddWithValue("environment", row.Environment);
            evt.Parameters.AddWithValue("provider_id", row.ProviderPaymentId);
            evt.Parameters.AddWithValue("amount", row.ExpectedMinor);
            evt.Parameters.AddWithValue("currency", row.ExpectedCurrency);
            evt.Parameters.AddWithValue("now", now);
            await evt.ExecuteNonQueryAsync(cancellationToken);
        }
        await SetPaymentStateAsync(connection, transaction, row.PaymentId,
            "refund_pending", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentView(row.PaymentId, "refund_pending",
            new Money(row.ExpectedMinor, row.ExpectedCurrency), row.Sku);
    }

    public async Task<IReadOnlyList<PaymentIntentRecord>> ListReconcileCandidatesAsync(
        string provider,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select payment_id,account_id,provider,environment,sku,
                   expected_minor,expected_currency,recurring,state,
                   provider_payment_id,invoice_payload
            from licensing.payments
            where provider=@provider and state in ('pending','refund_pending','reconcile_required')
            order by updated_at,payment_id
            limit @limit
            """, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PaymentIntentRecord>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadIntent(reader));
        return result;
    }
    private async Task ApplyPurchaseGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PaymentRow row,
        CatalogProduct product,
        VerifiedPayment payment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceReference = "payment:" + row.PaymentId.ToString("D");
        Guid? newGrantId = null;
        var validUntil = product.Kind == "time"
            ? payment.PaidThrough
                ?? payment.OccurredAt.AddDays(product.Days)
            : (DateTimeOffset?)null;
        await using (var insert = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,source_reference,plan_id,
              valid_from,valid_until,available,reserved,original_amount,
              reason,created_at)
            values(@grant,@account,@kind,'purchase',@source_reference,@plan_id,
              @valid_from,@valid_until,@available,0,@original,@reason,@now)
            on conflict(source_reference)
              where source='purchase' and source_reference is not null
            do nothing
            returning grant_id
            """, connection, transaction))
        {
            var grantId = Guid.NewGuid();
            insert.Parameters.AddWithValue("grant", grantId);
            insert.Parameters.AddWithValue("account", row.AccountId);
            insert.Parameters.AddWithValue("kind", product.Kind);
            insert.Parameters.AddWithValue("source_reference", sourceReference);
            insert.Parameters.AddWithValue("plan_id", (object?)product.PlanId ?? DBNull.Value);
            insert.Parameters.AddWithValue("valid_from", payment.OccurredAt);
            insert.Parameters.AddWithValue(
                "valid_until", (object?)validUntil ?? DBNull.Value);
            insert.Parameters.AddWithValue(
                "available", product.Kind == "credits" ? product.Credits : 0);
            insert.Parameters.AddWithValue(
                "original", product.Kind == "credits" ? product.Credits : 0);
            insert.Parameters.AddWithValue("reason", "purchase:" + row.Sku);
            insert.Parameters.AddWithValue("now", now);
            var created = await insert.ExecuteScalarAsync(cancellationToken);
            if (created is Guid id) newGrantId = id;
        }

        if (newGrantId is Guid grant
            && product.Kind == "credits")
        {
            await using var ledgerEvent = new NpgsqlCommand("""
                insert into licensing.credit_ledger(
                  ledger_id,account_id,grant_id,event_kind,
                  available_delta,reserved_delta,spent_delta,void_delta,
                  evidence_id,reason,created_at)
                values(@ledger,@account,@grant,'issue',
                  @available,0,0,0,@evidence,@reason,@now)
                """, connection, transaction);
            ledgerEvent.Parameters.AddWithValue("ledger", Guid.NewGuid());
            ledgerEvent.Parameters.AddWithValue("account", row.AccountId);
            ledgerEvent.Parameters.AddWithValue("grant", grant);
            ledgerEvent.Parameters.AddWithValue("available", product.Credits);
            ledgerEvent.Parameters.AddWithValue(
                "evidence", "payment:" + payment.ProviderPaymentId);
            ledgerEvent.Parameters.AddWithValue("reason", "purchase issued");
            ledgerEvent.Parameters.AddWithValue("now", now);
            await ledgerEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var account = new NpgsqlCommand("""
            update licensing.accounts
            set first_purchase_at=coalesce(first_purchase_at,@occurred),
                base_role=case when base_role='guest' then 'user' else base_role end
            where account_id=@account
            """, connection, transaction);
        account.Parameters.AddWithValue("occurred", payment.OccurredAt);
        account.Parameters.AddWithValue("account", row.AccountId);
        await account.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyRefundProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PaymentRow row,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceReference = "payment:" + row.PaymentId.ToString("D");
        await using var read = new NpgsqlCommand("""
            select grant_id,available,reserved,revoked_at
            from licensing.entitlement_grants
            where source='purchase' and source_reference=@reference
            for update
            """, connection, transaction);
        read.Parameters.AddWithValue("reference", sourceReference);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            return;
        }
        var grantId = reader.GetGuid(0);
        var available = reader.GetInt64(1);
        var reserved = reader.GetInt64(2);
        var alreadyRevoked = !reader.IsDBNull(3);
        await reader.DisposeAsync();
        if (alreadyRevoked) return;

        await using var update = new NpgsqlCommand("""
            update licensing.entitlement_grants
            set available=0,revoked_at=@now
            where grant_id=@grant
            """, connection, transaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("grant", grantId);
        await update.ExecuteNonQueryAsync(cancellationToken);

        if (available > 0)
        {
            await using var adjustment = new NpgsqlCommand("""
                insert into licensing.credit_ledger(
                  ledger_id,account_id,grant_id,event_kind,
                  available_delta,reserved_delta,spent_delta,void_delta,
                  evidence_id,reason,created_at)
                values(@ledger,@account,@grant,'adjustment',
                  @delta,0,0,@void,@evidence,'verified refund',@now)
                """, connection, transaction);
            adjustment.Parameters.AddWithValue("ledger", Guid.NewGuid());
            adjustment.Parameters.AddWithValue("account", row.AccountId);
            adjustment.Parameters.AddWithValue("grant", grantId);
            adjustment.Parameters.AddWithValue("delta", -available);
            adjustment.Parameters.AddWithValue("void", available);
            adjustment.Parameters.AddWithValue(
                "evidence", "refund:" + row.PaymentId.ToString("N"));
            adjustment.Parameters.AddWithValue("now", now);
            await adjustment.ExecuteNonQueryAsync(cancellationToken);
        }
        _ = reserved;
    }

    private static async Task<bool> InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PaymentRow row,
        VerifiedPayment payment,
        string eventKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            insert into licensing.payment_events(
              event_id,payment_id,account_id,event_key,provider,environment,
              provider_payment_id,status,amount_minor,currency,
              occurred_at,created_at)
            values(@event,@payment,@account,@key,@provider,@environment,
              @provider_id,@status,@amount,@currency,@occurred,@now)
            on conflict(payment_id,event_key) do nothing
            """, connection, transaction);
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("payment", row.PaymentId);
        command.Parameters.AddWithValue("account", row.AccountId);
        command.Parameters.AddWithValue("key", eventKey);
        command.Parameters.AddWithValue("provider", payment.Provider);
        command.Parameters.AddWithValue("environment", payment.Environment);
        command.Parameters.AddWithValue(
            "provider_id", payment.ProviderPaymentId);
        command.Parameters.AddWithValue("status", payment.Status);
        command.Parameters.AddWithValue("amount", payment.Amount.MinorUnits);
        command.Parameters.AddWithValue("currency", payment.Amount.Currency);
        command.Parameters.AddWithValue("occurred", payment.OccurredAt);
        command.Parameters.AddWithValue("now", now);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task SetPaymentStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid paymentId,
        string state,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            update licensing.payments
            set state=@state,updated_at=@now
            where payment_id=@payment
            """, connection, transaction);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("payment", paymentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PaymentRow?> ReadPaymentForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select payment_id,account_id,provider,environment,sku,catalog_version,
                   expected_minor,expected_currency,recurring,state,
                   provider_payment_id,invoice_payload
            from licensing.payments
            where payment_id=@payment
            for update
            """, connection, transaction);
        command.Parameters.AddWithValue("payment", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRow(reader)
            : null;
    }

    private async Task<PaymentView> ReadRequiredAsync(
        Guid accountId,
        Guid paymentId,
        CancellationToken cancellationToken)
        => await ReadAsync(accountId, paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment was not found.");

    private static PaymentRow ReadRow(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt64(6),
            reader.GetString(7),
            reader.GetBoolean(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));

    private static PaymentIntentRecord ReadIntent(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            new Money(reader.GetInt64(5), reader.GetString(6)),
            reader.GetBoolean(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));

    private static async Task LockAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtextextended(@account,20260918))",
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+','-')
            .Replace('/','_');

    private sealed record PaymentRow(
        Guid PaymentId,
        Guid AccountId,
        string Provider,
        string Environment,
        string Sku,
        string CatalogVersion,
        long ExpectedMinor,
        string ExpectedCurrency,
        bool Recurring,
        string State,
        string? ProviderPaymentId,
        string? InvoicePayload)
    {
        public PaymentView ToView()
            => new(
                PaymentId,
                State,
                new Money(ExpectedMinor, ExpectedCurrency),
                Sku);
    }
}

public sealed record PaymentIntentRecord(
    Guid PaymentId,
    Guid AccountId,
    string Provider,
    string Environment,
    string Sku,
    Money Amount,
    bool Recurring,
    string State,
    string? ProviderPaymentId,
    string? InvoicePayload);
