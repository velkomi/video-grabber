using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadOperationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Programmatic_cleanup_after_direct_completion_preserves_queued_session_and_captured_cookie_selection(bool cancel)
    {
        using var pages = new BrowserPageLifetime();
        var session = new BrowserSessionLifetime();
        var queue = new BrowserDownloadQueue();
        var candidate = new MediaCandidate(Intent.SelectedSource, new("https://school.example/a"), "HLS");
        queue.AddOrUpdate(candidate, 4, "720p", new(candidate.Referer, session.Epoch, BrowserPageMetadata.Empty) { CookieSelection = "embedded" });
        var saved = Assert.Single(queue.Items);
        var directPreparation = new BlockedPreparation();
        var directDownloader = new RecordingDownloader();
        var direct = new BrowserDownloadOperation(directPreparation, directDownloader, new());
        var pending = direct.RunAsync(Intent with { CookieSelection = "embedded" }, pages.Capture(), default);
        await directPreparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancel) direct.Cancel();
        directPreparation.Release.TrySetResult();
        var finished = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(cancel ? OperationOutcome.Cancelled : OperationOutcome.Succeeded, finished.Outcome);

        var visibleSelector = "embedded";
        session.RunProgrammaticSelectionCleanup(() =>
        {
            visibleSelector = "";
            Assert.False(session.OnSelectionChanged());
        });
        Assert.Equal("", visibleSelector);
        Assert.Equal(saved.Context!.SessionEpoch, session.Epoch);
        Assert.Same(saved, Assert.Single(queue.Items));
        Assert.Equal("embedded", saved.Context.CookieSelection);
        UserDownloadIntent? captured = null;
        var preparation = new Preparation((intent, _, _) =>
        {
            captured = intent;
            return Task.FromResult(new PreparedBrowserDownload(Values));
        });
        var downloader = new RecordingDownloader();
        var service = new BrowserDownloadOperation(preparation, downloader, new());
        var queuedIntent = Intent with { SessionEpoch = saved.Context.SessionEpoch, CookieSelection = saved.Context.CookieSelection };
        var result = await service.RunQueuedAsync(saved, queuedIntent, pages.Capture(), session.Epoch, default);
        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal("embedded", captured!.CookieSelection);
        Assert.Equal(1, downloader.Calls);

        Assert.True(session.OnSelectionChanged());
        service.OnSessionChanged(session.Epoch);
        Assert.False(service.CanContinue(saved.Context, session.Epoch));
        var rejected = await service.RunQueuedAsync(saved, queuedIntent, pages.Capture(), session.Epoch, default);
        Assert.Equal(OperationOutcome.Failed, rejected.Outcome);
        Assert.Equal(1, preparation.Calls);
        Assert.Equal(1, downloader.Calls);
        Assert.Same(saved, Assert.Single(queue.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lifecycle_callback_alone_cancels_late_preparation_without_resetting_page(bool sessionChanged)
    {
        using var pages = new BrowserPageLifetime();
        var queue = new BrowserDownloadQueue();
        var candidate = new MediaCandidate(Intent.SelectedSource, new("https://school.example/a"), "HLS");
        queue.AddOrUpdate(candidate, 4, "720p", new(candidate.Referer, 7, BrowserPageMetadata.Empty));
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var entry = Assert.Single(queue.Items);
        var pending = service.RunQueuedAsync(entry, Intent with { SessionEpoch = 7 }, pages.Capture(), 7, default);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.RequestQueue();
        if (sessionChanged) service.OnSessionChanged(8); else service.OnNavigation(7);
        Assert.False(pages.Capture().Token.IsCancellationRequested);
        preparation.Release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal(OperationCompletion.None, result.Completion);
        Assert.Equal(0, downloader.Calls);
        Assert.Same(entry, Assert.Single(queue.Items));
        Assert.Equal(!sessionChanged, service.CanContinue(entry.Context!, sessionChanged ? 8 : 7));
        if (sessionChanged)
        {
            var staleCaller = await service.RunQueuedAsync(entry, Intent with { SessionEpoch = 7 }, pages.Capture(), 7, default);
            Assert.Equal(OperationOutcome.Failed, staleCaller.Outcome);
            Assert.Equal(1, preparation.Calls);
        }
    }

    [Fact]
    public async Task Navigation_preserves_queue_cancels_active_work_and_clears_pending_run_intent()
    {
        using var pages = new BrowserPageLifetime();
        var queue = new BrowserDownloadQueue();
        var candidate = new MediaCandidate(Intent.SelectedSource, new("https://school.example/a"), "HLS");
        queue.AddOrUpdate(candidate, 4, "720p", new(candidate.Referer, 7, new("Lesson A", ["Part A"])));
        var saved = Assert.Single(queue.Items);
        var preparation = new BlockedPreparation();
        var downloader = new RecordingDownloader();
        var coordinator = new OperationCoordinator();
        var service = new BrowserDownloadOperation(preparation, downloader, coordinator);
        var pending = service.RunQueuedAsync(saved, Intent with { SessionEpoch = 7 }, pages.Capture(), 7, default);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.RequestQueue();
        service.OnNavigation(7);
        pages.Reset();
        preparation.Release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal(OperationCompletion.None, result.Completion);
        Assert.Same(saved, Assert.Single(queue.Items));
        Assert.True(service.CanContinue(saved.Context!, 7));
        Assert.Equal(0, downloader.Calls);
        Assert.Equal(OperationCompletion.None, coordinator.Complete(OperationOutcome.Succeeded));

        service.OnSessionChanged(8);
        Assert.False(service.CanContinue(saved.Context!, 8));
        var rejected = await service.RunQueuedAsync(saved, Intent with { SessionEpoch = 7 }, pages.Capture(), 8, default);
        Assert.Equal(OperationOutcome.Failed, rejected.Outcome);
        Assert.Equal(1, preparation.Calls);
        Assert.Equal(0, downloader.Calls);
        Assert.Same(saved, Assert.Single(queue.Items));
    }

    [Fact]
    public async Task Explicit_resume_uses_saved_source_and_quality_after_navigation_while_direct_uses_current_page_generation()
    {
        using var pages = new BrowserPageLifetime();
        var candidate = new MediaCandidate(Intent.SelectedSource, new("https://school.example/a"), "HLS");
        var queue = new BrowserDownloadQueue();
        queue.AddOrUpdate(candidate, 4, "720p", new(candidate.Referer, 7, BrowserPageMetadata.Empty));
        var preparation = new Preparation((captured, _, _) => Task.FromResult(new PreparedBrowserDownload(Values with { Source = captured.SelectedSource })));
        var downloader = new RecordingDownloader();
        var service = new BrowserDownloadOperation(preparation, downloader, new());
        service.OnNavigation(7);
        pages.Reset();
        Assert.Equal(0, preparation.Calls);
        var saved = Assert.Single(queue.Items);
        var resumed = await service.RunQueuedAsync(saved, Intent with { SessionEpoch = 7 }, pages.Capture(), 7, default);
        Assert.Equal(OperationOutcome.Succeeded, resumed.Outcome);
        Assert.Equal(candidate.Source, downloader.Request!.Source);
        Assert.Equal("720p", downloader.Request.Quality);
        var direct = await service.RunAsync(Intent with { SelectedSource = new("https://cdn.example/b.mp4"), SessionEpoch = pages.CurrentGeneration }, pages.Capture(), default);
        Assert.Equal(OperationOutcome.Succeeded, direct.Outcome);
        Assert.Equal(new Uri("https://cdn.example/b.mp4"), downloader.Request.Source);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("quality")]
    [InlineData("intent-session")]
    [InlineData("current-session")]
    [InlineData("missing-context")]
    [InlineData("cookie-selection")]
    public async Task Queued_mismatch_rejects_before_any_preparation_or_downloader(string mismatch)
    {
        using var pages = new BrowserPageLifetime();
        var candidate = new MediaCandidate(Intent.SelectedSource, new("https://school.example/a"), "HLS");
        var queue = new BrowserDownloadQueue();
        queue.AddOrUpdate(candidate, 4, "720p", mismatch == "missing-context" ? null : new(candidate.Referer, 7, BrowserPageMetadata.Empty));
        var preparation = new BlockedPreparation();
        preparation.Release.TrySetResult();
        var downloader = new RecordingDownloader();
        var service = new BrowserDownloadOperation(preparation, downloader, new());
        var intent = Intent with { SessionEpoch = 7 };
        intent = mismatch switch
        {
            "source" => intent with { SelectedSource = new("https://cdn.example/b.m3u8") },
            "quality" => intent with { Quality = "360p" },
            "intent-session" => intent with { SessionEpoch = 8 },
            "cookie-selection" => intent with { CookieSelection = "embedded" },
            _ => intent
        };
        var result = await service.RunQueuedAsync(Assert.Single(queue.Items), intent, pages.Capture(), mismatch == "current-session" ? 8 : 7, default);
        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(0, preparation.Calls);
        Assert.Equal(0, downloader.Calls);
    }

    [Fact]
    public async Task Downloader_provider_resolves_after_preparation_once_per_operation_and_next_run_sees_swap()
    {
        using var pages = new BrowserPageLifetime();
        var preparation = new BlockedPreparation();
        var first = new RecordingDownloader();
        var second = new RecordingDownloader();
        IVideoDownloader current = first;
        var providerCalls = 0;
        var service = new BrowserDownloadOperation(preparation, () =>
        {
            Interlocked.Increment(ref providerCalls);
            return current;
        }, new OperationCoordinator());

        var pending = service.RunAsync(Intent, pages.Capture(), default);
        await preparation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, providerCalls);
        current = second;
        preparation.Release.TrySetResult();
        var firstRun = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Succeeded, firstRun.Outcome);
        Assert.Equal(0, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.Equal(1, providerCalls);

        current = first;
        var secondRun = await service.RunAsync(Intent, pages.Capture(), default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(OperationOutcome.Succeeded, secondRun.Outcome);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.Equal(2, providerCalls);
    }
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
