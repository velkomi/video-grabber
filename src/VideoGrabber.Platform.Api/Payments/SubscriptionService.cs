using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Payments;

public sealed class SubscriptionService(
    SubscriptionStore subscriptions,
    PaymentStore payments,
    StarsPaymentAdapter stars,
    YooKassaPaymentAdapter yookassa,
    IBotApiClient bot)
{
    public static SubscriptionView ApplyCancel(SubscriptionView subscription)
        => subscription with
        {
            AutoRenew = false,
            State = "canceled"
        };

    public async Task<SubscriptionView?> ProjectInitialAsync(
        VerifiedPayment payment,
        CancellationToken cancellationToken)
    {
        var intent = await payments.ReadIntentAsync(
            payment.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Payment was not found.");
        var product = payments.Catalog.RequireProduct(
            intent.Sku, intent.Provider, intent.Recurring);
        return await subscriptions.ProjectInitialPaymentAsync(
            intent, product.PlanId, payment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionView> ApplyStarsRenewalAsync(
        Guid originPaymentId,
        string chargeId,
        DateTimeOffset paidThrough,
        Money amount,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chargeId);
        var subscription = await subscriptions.ReadByOriginAsync(
            originPaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        if (subscription.Provider != "stars")
            throw new SubscriptionConflictException();
        return await subscriptions.ApplyRenewalAsync(
            new RenewalEvent(
                "stars",
                subscription.Environment,
                chargeId,
                subscription.SubscriptionId,
                paidThrough.AddDays(-30),
                paidThrough,
                amount),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionView> ApplyStarsTerminalAsync(
        Guid originPaymentId,
        string chargeId,
        string kind,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.ReadByOriginAsync(
            originPaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        if (subscription.Provider != "stars")
            throw new SubscriptionConflictException();
        return await subscriptions.ApplyTerminalAdjustmentAsync(
            subscription.SubscriptionId, chargeId, kind, occurredAt,
            cancellationToken).ConfigureAwait(false);
    }
    public async Task<SubscriptionView> CancelAsync(
        Guid accountId,
        Guid subscriptionId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var pending = await subscriptions.RequestCancelAsync(
            accountId, subscriptionId, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        var record = await subscriptions.ReadRecordAsync(
            accountId, subscriptionId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Subscription was not found.");

        if (record.Provider == "stars")
        {
            var payerId = await stars.ResolvePayerAsync(
                accountId, cancellationToken).ConfigureAwait(false);
            var accepted = await bot.SetStarSubscriptionCanceledAsync(
                payerId,
                record.ProviderReference,
                canceled: true,
                cancellationToken).ConfigureAwait(false);
            if (!accepted) return pending;
        }
        else if (record.Provider != "yookassa")
        {
            throw new SubscriptionConflictException();
        }

        return await subscriptions.ConfirmCancelAsync(
            accountId, subscriptionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<YooKassaRenewalCheckout> CreateYooKassaRenewalAsync(
        Guid accountId,
        Guid subscriptionId,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.ReadRecordAsync(
            accountId, subscriptionId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Subscription was not found.");
        return await yookassa.CreateRecurringRenewalAsync(
            subscription, periodStart, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionView> VerifyYooKassaRenewalAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        var renewal = await yookassa.ReadVerifiedRenewalAsync(
            providerPaymentId, cancellationToken).ConfigureAwait(false);
        return await subscriptions.ApplyRenewalAsync(
            renewal, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SubscriptionView>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken)
        => subscriptions.ListAsync(accountId, cancellationToken);
}

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/subscriptions", ListAsync)
            .RequireAuthorization();
        endpoints.MapPost(
                "/v1/subscriptions/{subscriptionId:guid}/cancel",
                CancelAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        SubscriptionService subscriptions,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        return Results.Ok(await subscriptions.ListAsync(
            accountId, cancellationToken));
    }

    private static async Task<IResult> CancelAsync(
        Guid subscriptionId,
        CancelSubscriptionRequest request,
        HttpContext http,
        SubscriptionService subscriptions,
        CancellationToken cancellationToken)
    {
        if (!TryAccount(http, out var accountId)) return Results.Unauthorized();
        try
        {
            return Results.Ok(await subscriptions.CancelAsync(
                accountId,
                subscriptionId,
                request.IdempotencyKey,
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (SubscriptionConflictException)
        { return Results.Conflict(new { code = "subscription_conflict" }); }
        catch (ArgumentException ex)
        { return Results.BadRequest(new { code = ex.Message }); }
    }

    private static bool TryAccount(
        HttpContext http,
        out Guid accountId)
        => Guid.TryParse(
            http.User.FindFirst("account_id")?.Value,
            out accountId);
}
