using System.Text.Json;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public sealed class StarsUpdateHandler(
    PaymentStore payments,
    StarsPaymentAdapter stars,
    IBotApiClient bot,
    TimeProvider clock)
{
    public static bool ContainsSuccessfulPayment(JsonElement update)
        => update.ValueKind == JsonValueKind.Object
           && update.TryGetProperty("message", out var message)
           && message.ValueKind == JsonValueKind.Object
           && message.TryGetProperty("successful_payment", out var payment)
           && payment.ValueKind == JsonValueKind.Object;

    public async Task<bool> HandleAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        var root = update.Body;
        if (root.ValueKind != JsonValueKind.Object) return false;

        if (root.TryGetProperty("pre_checkout_query", out var preCheckout)
            && preCheckout.ValueKind == JsonValueKind.Object)
        {
            await HandlePreCheckoutAsync(preCheckout, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (ContainsSuccessfulPayment(root))
        {
            await HandleSuccessfulPaymentAsync(
                root.GetProperty("message"),
                cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (root.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("refunded_payment", out var refunded)
            && refunded.ValueKind == JsonValueKind.Object)
        {
            await HandleRefundedPaymentAsync(
                message, refunded, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private async Task HandlePreCheckoutAsync(
        JsonElement query,
        CancellationToken cancellationToken)
    {
        var queryId = RequiredString(query, "id");
        var payload = RequiredString(query, "invoice_payload");
        var currency = RequiredString(query, "currency");
        var amount = RequiredInt64(query, "total_amount");
        var payerId = RequiredUserId(query, "from");

        var accepted = false;
        string? error = null;
        try
        {
            var intent = await payments.ReadIntentByInvoiceAsync(
                payload, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Unknown invoice.");
            if (intent.Provider != "stars"
                || intent.State is not ("pending" or "refund_pending")
                || currency != "XTR"
                || intent.Amount.Currency != "XTR"
                || intent.Amount.MinorUnits != amount)
                throw new PaymentConflictException();

            var expectedPayer = await stars.ResolvePayerAsync(
                intent.AccountId, cancellationToken).ConfigureAwait(false);
            if (expectedPayer != payerId)
                throw new PaymentConflictException();
            accepted = true;
        }
        catch (Exception ex) when (
            ex is KeyNotFoundException
            or PaymentConflictException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            error = "Payment order is invalid or expired.";
        }

        await bot.AnswerPreCheckoutAsync(
            queryId, accepted, error, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleSuccessfulPaymentAsync(
        JsonElement message,
        CancellationToken cancellationToken)
    {
        var payerId = RequiredUserId(message, "from");
        var successful = message.GetProperty("successful_payment");
        var currency = RequiredString(successful, "currency");
        var amount = RequiredInt64(successful, "total_amount");
        var payload = RequiredString(successful, "invoice_payload");
        var chargeId = RequiredString(
            successful, "telegram_payment_charge_id");
        if (currency != "XTR" || amount <= 0)
            throw new PaymentConflictException();

        var intent = await payments.ReadIntentByInvoiceAsync(
            payload, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Unknown Stars invoice.");
        if (intent.Provider != "stars"
            || intent.Amount.Currency != "XTR"
            || intent.Amount.MinorUnits != amount)
            throw new PaymentConflictException();
        var expectedPayer = await stars.ResolvePayerAsync(
            intent.AccountId, cancellationToken).ConfigureAwait(false);
        if (payerId != expectedPayer)
            throw new PaymentConflictException();

        DateTimeOffset occurredAt = clock.GetUtcNow();
        if (message.TryGetProperty("date", out var date)
            && date.ValueKind == JsonValueKind.Number
            && date.TryGetInt64(out var unix)
            && unix >= 0)
            occurredAt = DateTimeOffset.FromUnixTimeSeconds(unix);

        DateTimeOffset? paidThrough = null;
        if (successful.TryGetProperty(
                "subscription_expiration_date",
                out var expiration)
            && expiration.ValueKind == JsonValueKind.Number
            && expiration.TryGetInt64(out var expirationUnix)
            && expirationUnix > 0)
            paidThrough = DateTimeOffset.FromUnixTimeSeconds(expirationUnix);

        await payments.ApplyAsync(
            new VerifiedPayment(
                "stars",
                intent.Environment,
                chargeId,
                intent.PaymentId,
                intent.AccountId,
                new Money(amount, "XTR"),
                "succeeded",
                occurredAt,
                intent.Recurring ? chargeId : null,
                paidThrough),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleRefundedPaymentAsync(
        JsonElement message,
        JsonElement refunded,
        CancellationToken cancellationToken)
    {
        var payerId = RequiredUserId(message, "from");
        var payload = RequiredString(refunded, "invoice_payload");
        var chargeId = RequiredString(
            refunded, "telegram_payment_charge_id");
        var currency = RequiredString(refunded, "currency");
        var amount = RequiredInt64(refunded, "total_amount");
        if (currency != "XTR" || amount <= 0)
            throw new PaymentConflictException();

        var intent = await payments.ReadIntentByInvoiceAsync(
            payload, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Unknown Stars refund.");
        var expectedPayer = await stars.ResolvePayerAsync(
            intent.AccountId, cancellationToken).ConfigureAwait(false);
        if (payerId != expectedPayer
            || intent.Provider != "stars"
            || intent.Amount.MinorUnits != amount)
            throw new PaymentConflictException();

        await payments.ApplyAsync(
            new VerifiedPayment(
                "stars",
                intent.Environment,
                chargeId,
                intent.PaymentId,
                intent.AccountId,
                new Money(amount, "XTR"),
                "refunded",
                clock.GetUtcNow(),
                intent.Recurring ? chargeId : null,
                null),
            cancellationToken).ConfigureAwait(false);
    }

    private static string RequiredString(
        JsonElement element,
        string property)
        => element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.String
           && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException(
                "Telegram payment field " + property + " is invalid.");

    private static long RequiredInt64(
        JsonElement element,
        string property)
        => element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt64(out var result)
            ? result
            : throw new InvalidDataException(
                "Telegram payment field " + property + " is invalid.");

    private static long RequiredUserId(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(property, out var user)
            || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("id", out var id)
            || !id.TryGetInt64(out var userId)
            || userId <= 0)
            throw new InvalidDataException(
                "Telegram payment payer is invalid.");
        return userId;
    }
}
