namespace VideoGrabber.Platform.Api.Telegram;

public sealed class DeliveryWorker(
    ArtifactDeliveryService deliveries,
    IConfiguration configuration,
    ILogger<DeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(
                configuration["VG_DELIVERY_WORKER_ENABLED"],
                "false",
                StringComparison.OrdinalIgnoreCase))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await deliveries.ProcessNextAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (item is null)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(500), stoppingToken)
                        .ConfigureAwait(false);
                }
                else if (item.State == "delivery_unknown")
                {
                    logger.LogWarning(
                        "Delivery entered delivery_unknown and requires explicit retry.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Delivery worker iteration failed: {ExceptionType}",
                    ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
