namespace VideoGrabber.App;

public sealed partial class MainWindow
{
    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (DispatcherQueue.HasThreadAccess)
            return action();

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.TrySetResult(await action());
            }
            catch (OperationCanceledException ex)
            {
                completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            completion.TrySetException(
                new InvalidOperationException("UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
