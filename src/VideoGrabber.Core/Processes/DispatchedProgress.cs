namespace VideoGrabber.Core.Processes;

public sealed class DispatchedProgress<T>(Func<Action, bool> enqueue, Action<T> handler) : IProgress<T>
{
    public void Report(T value)
    {
        _ = enqueue(() => handler(value));
    }
}
