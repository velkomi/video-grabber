using System.Diagnostics.CodeAnalysis;

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
    private const int MaxDevToolsRequests = 4096;
    private const int MaxWebResourceRequests = 4096;
    private sealed record RequestRegistration(BrowserPageLease Lease, string DocumentId);
    private sealed record WebResourceRegistration(object Request, RequestRegistration Origin);
    private readonly Dictionary<string, RequestRegistration> _devToolsRequests = new(StringComparer.Ordinal);
    private readonly Dictionary<object, LinkedListNode<WebResourceRegistration>> _webResourceRequests = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<WebResourceRegistration> _webResourceOrder = new();

    public int PendingRequestCount { get { lock (_gate) return _devToolsRequests.Count + _webResourceRequests.Count; } }
    public int PendingDevToolsRequestCount { get { lock (_gate) return _devToolsRequests.Count; } }
    public int PendingWebResourceRequestCount { get { lock (_gate) return _webResourceRequests.Count; } }

    public bool RememberRequest(object request, string documentId, BrowserPageLease lease)
    {
        lock (_gate)
        {
            if (!IsCurrent(lease)) return false;
            var origin = new RequestRegistration(lease, documentId);
            if (request is string requestId)
            {
                if (_devToolsRequests.Count >= MaxDevToolsRequests && !_devToolsRequests.ContainsKey(requestId)) return false;
                _devToolsRequests[requestId] = origin;
            }
            else if (_webResourceRequests.TryGetValue(request, out var existing))
            {
                existing.Value = new(request, origin);
            }
            else
            {
                // Unmatched fallback wrappers cannot use CDP capacity or grow indefinitely.
                if (_webResourceRequests.Count >= MaxWebResourceRequests)
                {
                    var oldest = _webResourceOrder.First!;
                    _webResourceRequests.Remove(oldest.Value.Request);
                    _webResourceOrder.RemoveFirst();
                }
                _webResourceRequests.Add(request, _webResourceOrder.AddLast(new WebResourceRegistration(request, origin)));
            }
            return true;
        }
    }

    public bool TryGetRequestLease(object request, string? documentId, [NotNullWhen(true)] out BrowserPageLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            var pending = request is string requestId
                ? _devToolsRequests.GetValueOrDefault(requestId)
                : _webResourceRequests.GetValueOrDefault(request)?.Value.Origin;
            if (pending is null || !IsCurrent(pending.Lease)
                || (documentId is not null && !string.Equals(documentId, pending.DocumentId, StringComparison.Ordinal))) return false;
            lease = pending.Lease;
            return true;
        }
    }

    public bool ForgetRequest(object request, BrowserPageLease lease)
    {
        lock (_gate)
        {
            if (request is string requestId)
                return _devToolsRequests.TryGetValue(requestId, out var pending) && pending.Lease == lease
                    && _devToolsRequests.Remove(requestId);
            if (!_webResourceRequests.TryGetValue(request, out var node) || node.Value.Origin.Lease != lease) return false;
            _webResourceRequests.Remove(request);
            _webResourceOrder.Remove(node);
            return true;
        }
    }

    private void ClearRequests()
    {
        _devToolsRequests.Clear();
        _webResourceRequests.Clear();
        _webResourceOrder.Clear();
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
            ClearRequests();
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
            ClearRequests();
        }
        _page.Cancel();
        _page.Dispose();
        // Pending work still releases the semaphore. It never creates a wait handle,
        // so let the managed semaphore be collected after those continuations finish.
    }
}
