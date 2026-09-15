using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserPageLease(long Generation, CancellationToken Token);

/// <summary>Owns one browser page and cancels work when that page is replaced.</summary>
public sealed class BrowserPageLifetime : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource _page = new();
    private long _generation;
    private bool _disposed;
    private readonly SemaphoreSlim _probes;
    private sealed class RequestIdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => x is string a && y is string b
            ? StringComparer.Ordinal.Equals(a, b) : ReferenceEquals(x, y);
        public int GetHashCode(object value) => value is string text
            ? StringComparer.Ordinal.GetHashCode(text) : RuntimeHelpers.GetHashCode(value);
    }

    private readonly Dictionary<object, (BrowserPageLease Lease, string DocumentId)> _requests = new(new RequestIdentityComparer());
    public int PendingRequestCount { get { lock (_gate) return _requests.Count; } }

    public bool RememberRequest(object request, string documentId, BrowserPageLease lease)
    {
        lock (_gate)
        {
            if (!IsCurrent(lease)) return false;
            if (_requests.Count >= 4096 && !_requests.ContainsKey(request)) return false;
            _requests[request] = (lease, documentId);
            return true;
        }
    }

    public bool TryGetRequestLease(object request, string? documentId, [NotNullWhen(true)] out BrowserPageLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (!_requests.TryGetValue(request, out var pending)
                || !IsCurrent(pending.Lease)
                || (documentId is not null && !string.Equals(documentId, pending.DocumentId, StringComparison.Ordinal))) return false;
            lease = pending.Lease;
            return true;
        }
    }

    public bool ForgetRequest(object request, BrowserPageLease lease)
    {
        lock (_gate)
            return _requests.TryGetValue(request, out var pending) && pending.Lease == lease
                && _requests.Remove(request);
    }

    public BrowserPageLifetime(int maxConcurrentProbes = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentProbes, 1);
        _probes = new(maxConcurrentProbes);
    }

    public long CurrentGeneration { get { lock (_gate) return _generation; } }
    public BrowserPageLease Capture()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new(_generation, _page.Token);
        }
    }
    public bool IsCurrent(BrowserPageLease lease)
    {
        lock (_gate) return !_disposed && lease.Generation == _generation
            && lease.Token == _page.Token && !lease.Token.IsCancellationRequested;
    }
    public void Reset()
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = _page;
            _page = new();
            _generation++;
            _requests.Clear();
        }
        previous.Cancel();
        previous.Dispose();
    }
    public async Task RunProbeAsync(BrowserPageLease lease, Func<CancellationToken, Task> work)
    {
        if (!IsCurrent(lease)) throw new OperationCanceledException(lease.Token);
        await _probes.WaitAsync(lease.Token);
        try
        {
            if (!IsCurrent(lease)) throw new OperationCanceledException(lease.Token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lease.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            await work(timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
        }
        finally { _probes.Release(); }
    }

    public bool TryApply(BrowserPageLease lease, Func<bool> isCurrentBrowser, Action apply)
    {
        lock (_gate)
        {
            if (!IsCurrent(lease) || !isCurrentBrowser()) return false;
            apply();
            return true;
        }
    }

    public async Task<bool> ReadAndApplyAsync<T>(BrowserPageLease lease, Func<Task<T>> read,
        Func<bool> isCurrentBrowser, Action<T> apply)
    {
        if (!IsCurrent(lease) || !isCurrentBrowser()) return false;
        var value = await read();
        return TryApply(lease, isCurrentBrowser, () => apply(value));
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _requests.Clear();
        }
        _page.Cancel();
        _page.Dispose();
        // Pending work still releases the semaphore. It never creates a wait handle,
        // so let the managed semaphore be collected after those continuations finish.
    }
}
