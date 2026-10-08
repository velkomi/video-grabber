using System.Diagnostics;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Operations;

namespace VideoGrabber.Platform.Worker;

public sealed class ServerWorkerService(
    WorkerApiClient api,
    IMediaJobExecutor executor,
    ILogger<ServerWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            AttemptLease? lease;
            try
            {
                lease = await api.ClaimAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Worker claim failed: {ExceptionType}", ex.GetType().Name);
                await DelayAsync(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (lease is null)
            {
                await DelayAsync(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            await RunLeaseAsync(lease, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunLeaseAsync(AttemptLease lease, CancellationToken stoppingToken)
    {
        var start = Stopwatch.GetTimestamp();
        var trace = lease.JobId.ToString("N");
        using var activity = PlatformTelemetry.Activities.StartActivity("worker.execute");
        activity?.SetTag("vg.job_id", trace);
        void Log(string name, string outcome, Exception? error = null) => logger.LogInformation(
            "VG_TELEMETRY {DiagnosticEvent}", JsonSerializer.Serialize(PlatformTelemetry.Create(
                "worker", name, outcome, trace, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                error:error, jobId:lease.JobId, attemptId:lease.AttemptId)));
        Log("job.execute", "started");
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatLoopAsync(lease, operation, stoppingToken);
        AttemptCompletion completion;
        try
        {
            var artifact = await executor.ExecuteAsync(lease, operation.Token).ConfigureAwait(false);
            completion = new AttemptCompletion(
                lease.JobId,
                lease.AttemptId,
                lease.Fence,
                "success",
                artifact,
                artifact.VerificationEvidenceId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Log("job.execute", "cancelled");
            operation.Cancel();
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log("job.policy", "failed", ex);
            completion = Failed(lease, "review_required", "policy-" + StableReason(ex.Message));
        }
        catch (InvalidDataException ex)
        {
            Log("job.verification", "failed", ex);
            completion = Failed(lease, "review_required", "verify-" + StableReason(ex.Message));
        }
        catch (Exception ex)
        {
            Log("job.execute.error", "failed", ex);
            logger.LogWarning("Worker execution failed: {ExceptionType}", ex.GetType().Name);
            completion = Failed(lease, "failed", "worker-failure");
        }
        finally
        {
            operation.Cancel();
            try { await heartbeat.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        try
        {
            Log("job.execute", completion.Outcome);
            var tags = new TagList { { "outcome", completion.Outcome } };
            PlatformTelemetry.Jobs.Add(1, tags);
            PlatformTelemetry.JobDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, tags);
            await api.CompleteAsync(completion, stoppingToken).ConfigureAwait(false);
            Log("job.report", "succeeded");
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            Log("job.report", "failed", ex);
            logger.LogWarning("Worker completion report failed: {ExceptionType}", ex.GetType().Name);
        }
    }

    private async Task HeartbeatLoopAsync(
        AttemptLease lease,
        CancellationTokenSource operation,
        CancellationToken stoppingToken)
    {
        while (!operation.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), operation.Token).ConfigureAwait(false);
            if (!await api.HeartbeatAsync(lease, operation.Token).ConfigureAwait(false))
            {
                operation.Cancel();
                return;
            }
        }
    }

    private static AttemptCompletion Failed(
        AttemptLease lease,
        string outcome,
        string evidence)
        => new(lease.JobId, lease.AttemptId, lease.Fence, outcome, null, evidence);

    private static string StableReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var safe = new string(value.ToLowerInvariant()
            .Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_')
            .Take(48).ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken token)
    {
        try { await Task.Delay(delay, token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
