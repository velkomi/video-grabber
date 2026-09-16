namespace VideoGrabber.Platform.Tests;

public sealed class AdjustableTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _utcNow;
    }

    public void Advance(TimeSpan value)
    {
        lock (_gate) _utcNow = _utcNow.Add(value);
    }
}
