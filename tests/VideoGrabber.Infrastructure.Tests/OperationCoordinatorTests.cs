using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class OperationCoordinatorTests
{
    [Theory]
    [InlineData(OperationOutcome.Failed)]
    [InlineData(OperationOutcome.Cancelled)]
    public void Unsuccessful_completion_cannot_start_pending_queue(OperationOutcome outcome)
    {
        var operations = new OperationCoordinator();
        Assert.True(operations.TryBegin());
        operations.RequestQueue();
        Assert.Equal(OperationCompletion.None, operations.Complete(outcome));
        Assert.False(operations.IsBusy);
    }

    [Fact]
    public void Success_starts_only_explicit_queue_request_once()
    {
        var operations = new OperationCoordinator();
        Assert.True(operations.TryBegin());
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Succeeded));
        Assert.True(operations.TryBegin());
        operations.RequestQueue();
        Assert.Equal(OperationCompletion.StartQueue, operations.Complete(OperationOutcome.Succeeded));
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Succeeded));
        Assert.True(operations.TryBegin());
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Succeeded));
    }

    [Fact]
    public void Shared_media_owner_close_wins_and_is_dispatched_once()
    {
        var operations = new OperationCoordinator();
        Assert.True(operations.TryBegin());
        Assert.False(operations.TryBegin());
        operations.RequestQueue();
        operations.RequestClose();
        operations.RequestClose();
        operations.RequestQueue();
        operations.Cancel();
        Assert.Equal(OperationCompletion.CloseWindow, operations.Complete(OperationOutcome.Cancelled));
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Failed));
        Assert.False(operations.TryBegin());
    }

    [Fact]
    public void Cancel_clears_intent_even_if_effect_reports_success()
    {
        var operations = new OperationCoordinator();
        Assert.True(operations.TryBegin());
        operations.RequestQueue();
        operations.Cancel();
        operations.RequestQueue();
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Succeeded));
        Assert.True(operations.TryBegin());
        Assert.Equal(OperationCompletion.None, operations.Complete(OperationOutcome.Succeeded));
    }
}
