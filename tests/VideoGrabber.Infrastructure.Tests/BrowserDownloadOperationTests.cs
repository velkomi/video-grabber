using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadOperationTests
{
    private static readonly UserDownloadIntent Intent = new(new("https://cdn.example/master.m3u8"), "720p", false, "output", null, 0);
    private static PreparedDownload Values => new(new("https://cdn.example/720.m3u8"), null, null, null, null,
        null, null, true, true, "part-2", 12, true);

    [Fact]
    public async Task Repeated_click_has_one_inflight_preparation()
    {
        using var page = new BrowserPageLifetime();
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var first = service.RunAsync(Intent, page.Capture(), default);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.RunAsync(Intent, page.Capture(), default);
        var calls = preparation.Calls;
        preparation.Release.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        Assert.Equal(1, downloader.Calls);
        Assert.Equal(OperationOutcome.Failed, results[1].Outcome);
        Assert.False(coordinator.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_or_close_during_preparation_cannot_start_downloader(bool close)
    {
        using var page = new BrowserPageLifetime();
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var pending = service.RunAsync(Intent, page.Capture(), default);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.RequestQueue();
        if (close) service.RequestClose(); else service.Cancel();
        preparation.Release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, downloader.Calls);
        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal(close ? OperationCompletion.CloseWindow : OperationCompletion.None, result.Completion);
        Assert.Equal(1, preparation.Resource.Disposals);
        Assert.False(coordinator.IsBusy);
        Assert.Equal(OperationCompletion.None, coordinator.Complete(OperationOutcome.Succeeded));
    }

    [Theory]
    [InlineData("401")]
    [InlineData("403")]
    [InlineData("timeout")]
    [InlineData("network")]
    public async Task Preparation_failure_is_one_failed_result_without_starting_queue(string failure)
    {
        using var page = new BrowserPageLifetime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var preparation = new Preparation(async (_, _, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            throw failure switch
            {
                "401" => new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized),
                "403" => new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden),
                "timeout" => new TaskCanceledException("Preflight deadline elapsed"),
                _ => new IOException("Connection interrupted")
            };
        });
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var pending = service.RunAsync(Intent, page.Capture(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.RequestQueue();
        release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(OperationCompletion.None, result.Completion);
        Assert.False(result.Download!.Success);
        Assert.Equal(0, downloader.Calls);
        Assert.Equal(1, preparation.Calls);
        Assert.False(coordinator.IsBusy);
        Assert.Equal(OperationCompletion.None, coordinator.Complete(OperationOutcome.Succeeded));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Page_and_window_cancellation_invalidate_late_preparation(bool windowClose)
    {
        using var page = new BrowserPageLifetime();
        using var window = new CancellationTokenSource();
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var service = new BrowserDownloadOperation(preparation, downloader, new());
        var pending = service.RunAsync(Intent, page.Capture(), window.Token);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (windowClose) window.Cancel(); else page.Reset();
        preparation.Release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, downloader.Calls);
        Assert.Equal(1, preparation.Resource.Disposals);
    }

    [Fact]
    public async Task Stale_intent_and_local_media_owner_reject_before_preparation()
    {
        using var page = new BrowserPageLifetime();
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var stale = await service.RunAsync(Intent with { SessionEpoch = 99 }, page.Capture(), default);
        Assert.Equal(OperationOutcome.Cancelled, stale.Outcome);
        Assert.True(coordinator.TryBegin());
        var busy = await service.RunAsync(Intent, page.Capture(), default);
        Assert.Equal(OperationOutcome.Failed, busy.Outcome);
        Assert.True(coordinator.IsBusy);
        Assert.Equal(0, preparation.Calls);
        Assert.Equal(0, downloader.Calls);
        coordinator.RequestClose();
        Assert.Equal(OperationCompletion.CloseWindow, coordinator.Complete(OperationOutcome.Cancelled));
    }

    [Theory]
    [InlineData("success", true, OperationOutcome.Succeeded, OperationCompletion.StartQueue)]
    [InlineData("success", false, OperationOutcome.Succeeded, OperationCompletion.None)]
    [InlineData("failed", true, OperationOutcome.Failed, OperationCompletion.None)]
    [InlineData("throw", true, OperationOutcome.Failed, OperationCompletion.None)]
    [InlineData("cancel", true, OperationOutcome.Cancelled, OperationCompletion.None)]
    [InlineData("close", true, OperationOutcome.Cancelled, OperationCompletion.CloseWindow)]
    public async Task Download_completion_disposes_once_and_suppresses_stale_progress(string effect, bool queue,
        OperationOutcome expectedOutcome, OperationCompletion expectedCompletion)
    {
        using var page = new BrowserPageLifetime();
        var resource = new Resource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<DownloadProgress>? lateProgress = null;
        var updates = new List<DownloadProgress>();
        var downloader = new RecordingDownloader
        {
            Effect = async (_, progress, token) =>
            {
                lateProgress = progress;
                progress!.Report(new(10, "working"));
                entered.TrySetResult();
                await release.Task;
                if (effect == "throw") throw new IOException("Download failed");
                return new(effect != "failed", "finished");
            }
        };
        var preparation = new Preparation((_, _, _) => Task.FromResult(new PreparedBrowserDownload(Values, resource)));
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator) { Progress = new ProgressSink(updates) };
        var intent = Intent;
        var pending = service.RunAsync(intent, page.Capture(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        intent = intent with { Quality = "1080p", OutputDirectory = "changed", AudioOnly = true };
        if (queue) service.RequestQueue();
        if (effect == "cancel") service.Cancel();
        if (effect == "close") { service.RequestClose(); service.RequestClose(); }
        Assert.Equal(0, resource.Disposals);
        release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        lateProgress!.Report(new(90, "stale"));
        Assert.Single(updates);
        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedCompletion, result.Completion);
        Assert.Equal(1, downloader.Calls);
        Assert.Equal(1, preparation.Calls);
        Assert.Equal(1, resource.Disposals);
        Assert.Equal("720p", downloader.Request!.Quality);
        Assert.Equal("output", downloader.Request.OutputDirectory);
        Assert.Equal(Values.Source, downloader.Request.Source);
        Assert.False(downloader.Request.AudioOnly);
        Assert.False(coordinator.IsBusy);
        Assert.Equal(OperationCompletion.None, coordinator.Complete(OperationOutcome.Succeeded));
    }

    [Fact]
    public async Task Disposal_failure_still_completes_once_and_close_wins()
    {
        using var page = new BrowserPageLifetime();
        var disposed = new Resource();
        var broken = new ThrowingResource();
        var coordinator = new OperationCoordinator();
        var preparation = new Preparation((_, _, _) => Task.FromResult(new PreparedBrowserDownload(Values, disposed, broken)));
        var downloader = new RecordingDownloader { Effect = (_, _, _) => { coordinator.RequestClose(); return Task.FromResult(new DownloadResult(true, "done")); } };
        var result = await new BrowserDownloadOperation(preparation, downloader, coordinator).RunAsync(Intent, page.Capture(), default);
        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(OperationCompletion.CloseWindow, result.Completion);
        Assert.Equal(1, disposed.Disposals);
        Assert.Equal(1, broken.Disposals);
        Assert.False(coordinator.IsBusy);
        Assert.Equal(OperationCompletion.None, coordinator.Complete(OperationOutcome.Failed));
    }

    private sealed class Preparation(Func<UserDownloadIntent, BrowserPageLease, CancellationToken, Task<PreparedBrowserDownload>> prepare)
        : IBrowserDownloadPreparation
    {
        public int Calls;
        public Task<PreparedBrowserDownload> PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token)
        {
            Calls++;
            return prepare(intent, lease, token);
        }
    }
    private sealed class ProgressSink(List<DownloadProgress> values) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => values.Add(value);
    }
    private sealed class ThrowingResource : IDisposable
    {
        public int Disposals;
        public void Dispose() { Disposals++; throw new IOException("Cleanup failed"); }
    }

    private sealed class BlockedPreparation : IBrowserDownloadPreparation
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly Resource Resource = new();
        public int Calls;
        public async Task<PreparedBrowserDownload> PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            // Deliberately returns late after cancellation, like a non-cancellable WebView2 read.
            await Release.Task;
            return new(Values, Resource);
        }
    }
    private sealed class Resource : IDisposable
    {
        public int Disposals;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }
    private sealed class RecordingDownloader : IVideoDownloader
    {
        public int Calls;
        public DownloadRequest? Request;
        public Func<DownloadRequest, IProgress<DownloadProgress>?, CancellationToken, Task<DownloadResult>>? Effect;
        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Request = request;
            return Effect?.Invoke(request, progress, cancellationToken) ?? Task.FromResult(new DownloadResult(true, "done", "output.mp4"));
        }
    }
}
