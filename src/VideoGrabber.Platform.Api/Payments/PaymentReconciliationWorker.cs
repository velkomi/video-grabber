namespace VideoGrabber.Platform.Api.Payments;

public sealed class PaymentReconciliationWorker(
    StarsPaymentAdapter stars,
    IConfiguration configuration,
    ILogger<PaymentReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(
                configuration["VG_PAYMENT_RECONCILIATION_ENABLED"],
                "false",
                StringComparison.OrdinalIgnoreCase))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await stars.ReconcileAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (count > 0)
                    logger.LogInformation(
                        "Payment reconciliation applied {Count} Stars transitions.",
                        count);
                await Task.Delay(
                    TimeSpan.FromSeconds(30), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Payment reconciliation failed: {ExceptionType}",
                    ex.GetType().Name);
                await Task.Delay(
                    TimeSpan.FromSeconds(10), stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
