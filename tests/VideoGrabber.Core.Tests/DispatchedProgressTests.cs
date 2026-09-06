using VideoGrabber.Core.Processes;

namespace VideoGrabber.Core.Tests;

public sealed class DispatchedProgressTests
{
    [Fact]
    public void Report_enqueues_the_handler_before_it_updates_the_consumer()
    {
        Action? queuedAction = null;
        int? received = null;
        var progress = new DispatchedProgress<int>(
            action =>
            {
                queuedAction = action;
                return true;
            },
            value => received = value);

        progress.Report(42);

        Assert.Null(received);
        Assert.NotNull(queuedAction);
        queuedAction();
        Assert.Equal(42, received);
    }
}
