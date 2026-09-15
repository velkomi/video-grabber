using VideoGrabber.Core.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public interface IBrowserDownloadPreparation
{
    Task<PreparedBrowserDownload> PrepareAsync(UserDownloadIntent intent, BrowserPageLease lease, CancellationToken token);
}

/// <summary>Owns the cookie and routing scopes until the download has stopped.</summary>
public sealed class PreparedBrowserDownload(PreparedDownload values, params IDisposable[] resources) : IAsyncDisposable
{
    private int _disposed;
    public PreparedDownload Values { get; } = values;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
        List<Exception>? errors = null;
        foreach (var resource in resources.Reverse())
        {
            try { resource.Dispose(); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        return errors is null ? ValueTask.CompletedTask : ValueTask.FromException(new AggregateException(errors));
    }
}
