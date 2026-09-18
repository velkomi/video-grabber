namespace VideoGrabber.Platform.Api.Telegram;

public sealed class TelegramInboxWorker(
    TelegramUpdateInbox inbox,
    BotCommandHandler handler)
{
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        var update = await inbox.ClaimAsync(cancellationToken).ConfigureAwait(false);
        if (update is null) return false;
        try
        {
            await handler.HandleAsync(update, cancellationToken).ConfigureAwait(false);
            await inbox.MarkHandledAsync(update.UpdateId, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            try { await inbox.ReleaseAsync(update.UpdateId, CancellationToken.None).ConfigureAwait(false); }
            catch { }
            throw;
        }
    }
}
public sealed class TelegramInboxHostedService(
    TelegramInboxWorker worker,
    TelegramUpdateInbox inbox,
    IConfiguration configuration,
    ILogger<TelegramInboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["VG_TELEGRAM_WORKER_ENABLED"], "false", StringComparison.OrdinalIgnoreCase))
            return;
        try { await inbox.RecoverStaleProcessingAsync(stoppingToken).ConfigureAwait(false); }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Telegram inbox recovery failed: {ExceptionType}", ex.GetType().Name);
        }
        var processed = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = await worker.RunOnceAsync(stoppingToken).ConfigureAwait(false);
                if (!handled)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(350), stoppingToken).ConfigureAwait(false);
                    continue;
                }
                processed++;
                if (processed % 100 == 0)
                    await inbox.CleanupHandledAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Telegram inbox iteration failed: {ExceptionType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}