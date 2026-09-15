using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserPageLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_read_cannot_apply_after_page_or_browser_replacement(bool replaceBrowser)
    {
        using var pages = new BrowserPageLifetime();
        var lease = pages.Capture();
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentBrowser = new object();
        var capturedBrowser = currentBrowser;
        var title = "B";
        var read = pages.ReadAndApplyAsync(lease, () => response.Task,
            () => ReferenceEquals(capturedBrowser, currentBrowser), value => title = value);
        if (replaceBrowser) currentBrowser = new object(); else pages.Reset();
        response.SetResult("A");
        Assert.False(await read);
        Assert.Equal("B", title);
    }

    [Fact]
    public async Task Stale_read_does_not_start_and_current_read_applies()
    {
        using var pages = new BrowserPageLifetime();
        var old = pages.Capture();
        pages.Reset();
        var started = 0;
        Task<string> Read() { started++; return Task.FromResult("B"); }
        var title = "";
        Assert.False(await pages.ReadAndApplyAsync(old, Read, () => true, value => title = value));
        Assert.Equal(0, started);
        Assert.True(await pages.ReadAndApplyAsync(pages.Capture(), Read, () => true, value => title = value));
        Assert.Equal("B", title);
        Assert.Equal(1, started);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_hundred_probes_are_bounded_and_navigation_or_close_cancels_running_and_queued(bool close)
    {
        using var pages = new BrowserPageLifetime();
        var lease = pages.Capture();
        var startedThree = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var peak = 0;
        var started = 0;
        var tasks = Enumerable.Range(0, 200).Select(_ => pages.RunProbeAsync(lease, async token =>
        {
            var count = Interlocked.Increment(ref active);
            Interlocked.Increment(ref started);
            int previous;
            do { previous = Volatile.Read(ref peak); }
            while (count > previous && Interlocked.CompareExchange(ref peak, count, previous) != previous);
            if (count == 3) startedThree.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            finally { Interlocked.Decrement(ref active); }
        })).ToArray();
        await startedThree.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var peakBeforeCancel = Volatile.Read(ref peak);
        if (close) pages.Dispose(); else pages.Reset();
        foreach (var task in tasks) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(3, peakBeforeCancel);
        Assert.Equal(3, started);
        Assert.Equal(0, active);
        Assert.False(pages.IsCurrent(lease));
    }

    [Fact]
    public void Reset_invalidates_previous_lease()
    {
        using var pages = new BrowserPageLifetime();
        var previous = pages.Capture();
        pages.Reset();
        Assert.True(previous.Token.IsCancellationRequested);
        Assert.False(pages.IsCurrent(previous));
        Assert.True(pages.IsCurrent(pages.Capture()));
        Assert.Equal(previous.Generation + 1, pages.CurrentGeneration);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task All_two_hundred_probes_complete_in_bounded_waves(int limit)
    {
        using var pages = new BrowserPageLifetime(limit);
        var gates = Enumerable.Range(0, 200).Select(_ =>
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var started = Enumerable.Range(0, 200).Select(_ =>
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var active = 0;
        var sequence = 0;
        var tasks = Enumerable.Range(0, 200).Select(_ => pages.RunProbeAsync(pages.Capture(), async token =>
        {
            var index = Interlocked.Increment(ref sequence) - 1;
            var count = Interlocked.Increment(ref active);
            try
            {
                Assert.InRange(count, 1, limit);
                started[index].SetResult();
                await gates[index].Task.WaitAsync(token);
            }
            finally { Interlocked.Decrement(ref active); }
        })).ToArray();
        for (var i = 0; i < 200; i++)
        {
            await started[i].Task.WaitAsync(TimeSpan.FromSeconds(10));
            gates[i].SetResult();
        }
        await Task.WhenAll(tasks);
        Assert.Equal(200, sequence);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task Failed_probe_releases_slot_and_foreign_or_disposed_lease_cannot_start()
    {
        using var pages = new BrowserPageLifetime(1);
        using var other = new BrowserPageLifetime();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pages.RunProbeAsync(other.Capture(), _ => Task.CompletedTask));
        await Assert.ThrowsAsync<IOException>(() => pages.RunProbeAsync(pages.Capture(), _ => throw new IOException()));
        var ran = false;
        await pages.RunProbeAsync(pages.Capture(), _ => { ran = true; return Task.CompletedTask; });
        Assert.True(ran);
        var lease = pages.Capture();
        pages.Dispose();
        Assert.False(pages.TryApply(lease, () => true, () => throw new InvalidOperationException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pages.RunProbeAsync(lease, _ => Task.CompletedTask));
    }
}
