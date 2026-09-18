using System.Globalization;
using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public sealed class StarsPaymentAdapter(
    PaymentStore payments,
    SubscriptionStore subscriptions,
    NpgsqlDataSource apiDataSource,
    IBotApiClient bot) : IPaymentAdapter
{
    public async Task<PaymentCheckout> CreateAsync(
        Guid accountId,
        PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Provider, "stars", StringComparison.Ordinal))
            throw new ArgumentException("Stars adapter requires provider=stars.", nameof(request));
        var payerId = await ResolvePayerAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        var checkout = await payments.BeginAsync(accountId, request, cancellationToken)
            .ConfigureAwait(false);
        var product = payments.Catalog.RequireProduct(
            request.Sku, "stars", request.Recurring);
        var price = product.Prices["stars"];
        var title = request.Sku.Length <= 32 ? request.Sku : request.Sku[..32];
        var uri = await bot.CreateInvoiceAsync(
            payerId,
            checkout.InvoicePayload
                ?? throw new InvalidDataException("Stars invoice payload is missing."),
            title,
            new Money(price.MinorUnits, price.Currency),
            request.Recurring ? 2592000 : null,
            cancellationToken).ConfigureAwait(false);
        return checkout with { RedirectUri = uri };
    }

    public async Task<VerifiedPayment> ReadVerifiedAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);
        for (var offset = 0; offset < 1000; offset += 100)
        {
            var page = await bot.ReadStarTransactionsAsync(
                offset, 100, cancellationToken).ConfigureAwait(false);
            if (!page.TryGetProperty("transactions", out var transactions)
                || transactions.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Telegram StarTransactions list is missing.");
            var count = 0;
            foreach (var transaction in transactions.EnumerateArray())
            {
                count++;
                if (!transaction.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String
                    || !string.Equals(
                        id.GetString(), providerPaymentId, StringComparison.Ordinal))
                    continue;
                return await ParseTransactionAsync(
                    transaction, cancellationToken).ConfigureAwait(false);
            }
            if (count < 100) break;
        }
        throw new KeyNotFoundException("Telegram Star transaction was not found.");
    }

    public async Task<string> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken)
    {
        var intent = await payments.ReadIntentAsync(
            request.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Payment was not found.");
        if (intent.Provider != "stars"
            || string.IsNullOrWhiteSpace(intent.ProviderPaymentId))
            throw new PaymentConflictException();
        var payerId = await ResolvePayerAsync(
            intent.AccountId, cancellationToken).ConfigureAwait(false);
        await payments.MarkRefundPendingAsync(
            intent.AccountId, request, cancellationToken).ConfigureAwait(false);
        var accepted = await bot.RefundStarsAsync(
            payerId, intent.ProviderPaymentId, cancellationToken).ConfigureAwait(false);
        return accepted ? "refund_pending" : "refund_provider_rejected";
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var candidates = await payments.ListReconcileCandidatesAsync(
            "stars", 100, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0) return 0;
        var reconciled = 0;

        // A single provider read page set can reconcile pending invoice handles.
        var transactions = new List<JsonElement>();
        for (var offset = 0; offset < 1000; offset += 100)
        {
            var page = await bot.ReadStarTransactionsAsync(
                offset, 100, cancellationToken).ConfigureAwait(false);
            if (!page.TryGetProperty("transactions", out var items)
                || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Telegram StarTransactions list is missing.");
            var count = 0;
            foreach (var item in items.EnumerateArray())
            {
                count++;
                transactions.Add(item.Clone());
            }
            if (count < 100) break;
        }

        foreach (var candidate in candidates)
        {
            var match = FindTransaction(candidate, transactions);
            if (match is null) continue;
            var verified = await ParseTransactionAsync(
                match.Value, cancellationToken).ConfigureAwait(false);
            if (verified.PaymentId != candidate.PaymentId) continue;
            await payments.ApplyAsync(verified, cancellationToken).ConfigureAwait(false);
            await subscriptions.ProjectInitialPaymentAsync(candidate, verified, cancellationToken).ConfigureAwait(false);
            reconciled++;
        }

        return reconciled;
    }

    public async Task<long> ResolvePayerAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await apiDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var scope = new NpgsqlCommand(
            "select set_config('vg.account_id', @account, true)",
            connection, transaction))
        {
            scope.Parameters.AddWithValue("account", accountId.ToString("D"));
            await scope.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(
            """
            select provider_subject
            from licensing.identities
            where account_id=@account and provider='telegram'
            order by linked_at
            limit 2
            """,
            connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>(2);
        while (await reader.ReadAsync(cancellationToken))
            values.Add(reader.GetString(0));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        if (values.Count != 1
            || !long.TryParse(
                values[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var payerId)
            || payerId <= 0)
            throw new UnauthorizedAccessException("telegram_payer_identity_required");
        return payerId;
    }

    private async Task<VerifiedPayment> ParseTransactionAsync(
        JsonElement transaction,
        CancellationToken cancellationToken)
    {
        var chargeId = RequiredString(transaction, "id");
        var amount = RequiredInt64(transaction, "amount");
        var date = RequiredInt64(transaction, "date");
        if (date < 0) throw new InvalidDataException("Telegram transaction date is invalid.");

        var status = "succeeded";
        JsonElement partner;
        if (transaction.TryGetProperty("source", out partner)
            && partner.ValueKind == JsonValueKind.Object)
        {
            status = "succeeded";
        }
        else if (transaction.TryGetProperty("receiver", out partner)
                 && partner.ValueKind == JsonValueKind.Object)
        {
            status = "refunded";
        }
        else
        {
            throw new InvalidDataException("Telegram Star transaction partner is missing.");
        }

        if (RequiredString(partner, "type") != "user"
            || (partner.TryGetProperty("transaction_type", out var transactionType)
                && transactionType.ValueKind == JsonValueKind.String
                && transactionType.GetString() != "invoice_payment"))
            throw new InvalidDataException("Telegram Star transaction is not an invoice payment.");

        if (!partner.TryGetProperty("invoice_payload", out var payloadElement)
            || payloadElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(payloadElement.GetString()))
            throw new InvalidDataException("Telegram Star invoice payload is missing.");
        var payload = payloadElement.GetString()!;
        var intent = await payments.ReadIntentByInvoiceAsync(
            payload, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Unknown Stars invoice.");
        if (intent.Provider != "stars")
            throw new PaymentConflictException();

        if (!partner.TryGetProperty("user", out var user)
            || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("id", out var userIdElement)
            || !userIdElement.TryGetInt64(out var payerId)
            || payerId <= 0)
            throw new InvalidDataException("Telegram Star transaction payer is missing.");
        var expectedPayer = await ResolvePayerAsync(
            intent.AccountId, cancellationToken).ConfigureAwait(false);
        if (payerId != expectedPayer)
            throw new PaymentConflictException();

        int? subscriptionPeriod = null;
        if (partner.TryGetProperty("subscription_period", out var periodElement)
            && periodElement.ValueKind == JsonValueKind.Number
            && periodElement.TryGetInt32(out var period))
            subscriptionPeriod = period;
        var occurred = DateTimeOffset.FromUnixTimeSeconds(date);
        var paidThrough = subscriptionPeriod is int seconds && seconds > 0
            ? occurred.AddSeconds(seconds)
            : (DateTimeOffset?)null;

        return new VerifiedPayment(
            "stars",
            intent.Environment,
            chargeId,
            intent.PaymentId,
            intent.AccountId,
            new Money(Math.Abs(amount), "XTR"),
            status,
            occurred,
            intent.Recurring ? chargeId : null,
            paidThrough);
    }

    private static JsonElement? FindTransaction(
        PaymentIntentRecord candidate,
        IReadOnlyList<JsonElement> transactions)
    {
        foreach (var transaction in transactions)
        {
            if (candidate.ProviderPaymentId is { Length: > 0 }
                && transaction.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String
                && string.Equals(
                    id.GetString(), candidate.ProviderPaymentId, StringComparison.Ordinal))
                return transaction;

            if (candidate.InvoicePayload is not { Length: > 0 }) continue;
            foreach (var side in new[] { "source", "receiver" })
            {
                if (transaction.TryGetProperty(side, out var partner)
                    && partner.ValueKind == JsonValueKind.Object
                    && partner.TryGetProperty("invoice_payload", out var payload)
                    && payload.ValueKind == JsonValueKind.String
                    && string.Equals(
                        payload.GetString(), candidate.InvoicePayload, StringComparison.Ordinal))
                    return transaction;
            }
        }
        return null;
    }

    private static string RequiredString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.String
           && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException(
                "Telegram transaction field " + property + " is invalid.");

    private static long RequiredInt64(JsonElement element, string property)
        => element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException(
                "Telegram transaction field " + property + " is invalid.");
}
