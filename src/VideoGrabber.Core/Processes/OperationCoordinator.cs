namespace VideoGrabber.Core.Processes;

public enum OperationOutcome { Succeeded, Failed, Cancelled }
public enum OperationCompletion { None, StartQueue, CloseWindow }

public sealed class OperationCoordinator
{
    private readonly object _gate = new();
    private bool _busy;
    private bool _queue;
    private bool _close;
    private bool _cancelled;

    public bool IsBusy { get { lock (_gate) return _busy; } }
    public bool TryBegin()
    {
        lock (_gate)
        {
            if (_busy || _close) return false;
            _busy = true;
            _cancelled = false;
            return true;
        }
    }
    public void RequestQueue() { lock (_gate) { if (_busy && !_close && !_cancelled) _queue = true; } }
    public void RequestClose() { lock (_gate) { _close = true; _queue = false; _cancelled = true; } }
    public void Cancel() { lock (_gate) { _cancelled = true; _queue = false; } }
    public OperationCompletion Complete(OperationOutcome outcome)
    {
        lock (_gate)
        {
            if (!_busy) return OperationCompletion.None;
            _busy = false;
            var completion = _close ? OperationCompletion.CloseWindow
                : outcome == OperationOutcome.Succeeded && !_cancelled && _queue ? OperationCompletion.StartQueue : OperationCompletion.None;
            _queue = false;
            return completion;
        }
    }
}
