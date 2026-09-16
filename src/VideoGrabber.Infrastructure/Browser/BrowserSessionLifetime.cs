namespace VideoGrabber.Infrastructure.Browser;

/// <summary>Tracks local session identity independently from one-shot selector cleanup.</summary>
public sealed class BrowserSessionLifetime
{
    private int _cleanupDepth;
    public long Epoch { get; private set; }
    public void Invalidate() => Epoch++;
    public bool OnSelectionChanged()
    {
        if (_cleanupDepth > 0) return false;
        Invalidate();
        return true;
    }

    // The synchronous UI assignment raises SelectionChanged inside this scope.
    // Logout/browser replacement calls Invalidate directly and is never suppressed.
    public void RunProgrammaticSelectionCleanup(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        _cleanupDepth++;
        try { cleanup(); }
        finally { _cleanupDepth--; }
    }
}
