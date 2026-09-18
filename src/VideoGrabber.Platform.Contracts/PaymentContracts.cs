namespace VideoGrabber.Platform.Contracts;

public sealed record Money(long MinorUnits, string Currency);

public sealed record PurchaseRequest(
    string Sku,
    string Provider,
    Guid IdempotencyKey,
    bool Recurring);

public sealed record PaymentCheckout(
    Guid PaymentId,
    string State,
    Uri? RedirectUri,
    string? InvoicePayload);

public sealed record VerifiedPayment(
    string Provider,
    string Environment,
    string ProviderPaymentId,
    Guid PaymentId,
    Guid AccountId,
    Money Amount,
    string Status,
    DateTimeOffset OccurredAt,
    string? SubscriptionId,
    DateTimeOffset? PaidThrough);

public sealed record RefundRequest(
    Guid PaymentId,
    Guid IdempotencyKey,
    string Reason);

public sealed record PaymentView(
    Guid PaymentId,
    string Status,
    Money Amount,
    string Sku);

public interface IPaymentAdapter
{
    Task<PaymentCheckout> CreateAsync(
        Guid accountId,
        PurchaseRequest request,
        CancellationToken cancellationToken);

    Task<VerifiedPayment> ReadVerifiedAsync(
        string providerPaymentId,
        CancellationToken cancellationToken);

    Task<string> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken);
}
