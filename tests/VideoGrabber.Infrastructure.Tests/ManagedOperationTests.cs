using VideoGrabber.Core.Licensing;
using Xunit;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ManagedOperationTests
{
    [Fact]
    public async Task Missing_access_prevents_the_download_delegate()
    {
        var started = false;
        var coordinator = new ManagedOperationCoordinator(new DeniedAccessClient());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => coordinator.RunAsync(
            new ManagedOperation(Guid.NewGuid(), "hash", "download", "desktop_worker", Guid.NewGuid()),
            _ => { started = true; return Task.FromResult(0); }, CancellationToken.None));
        Assert.False(started);
    }

    [Theory]
    [InlineData("direct_download")]
    [InlineData("browser_candidate")]
    [InlineData("queue_selected")]
    [InlineData("queue_all")]
    [InlineData("edit")]
    [InlineData("mp3")]
    [InlineData("transcription")]
    [InlineData("server_command")]
    public async Task Denied_access_prevents_all_protected_operation_kinds(string kind)
    {
        var started = false;
        var coordinator = new ManagedOperationCoordinator(new DeniedAccessClient());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => coordinator.RunAsync(
            new ManagedOperation(Guid.NewGuid(), "hash-" + kind, kind, "desktop_worker", Guid.NewGuid()),
            _ => { started = true; return Task.FromResult(0); }, CancellationToken.None));
        Assert.False(started);
    }

    [Fact]
    public async Task Cancellation_before_admission_never_runs_delegate()
    {
        var started = false;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var coordinator = new ManagedOperationCoordinator(new CancellationAwareAccessClient());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RunAsync(
            new ManagedOperation(Guid.NewGuid(), "hash", "download", "desktop_worker", null),
            _ => { started = true; return Task.FromResult(0); }, cts.Token));
        Assert.False(started);
    }

    [Fact]
    public async Task Cancellation_after_admission_reports_cancel_requested()
    {
        var access = new RecordingAccessClient();
        var coordinator = new ManagedOperationCoordinator(access);
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RunAsync(
            new ManagedOperation(Guid.NewGuid(), "hash", "download", "desktop_worker", null),
            token => { cts.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(0); }, cts.Token));
        Assert.Equal("cancel_requested", Assert.Single(access.Reports));
    }

    private sealed class DeniedAccessClient : IManagedAccessClient
    {
        public Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken)
            => throw new UnauthorizedAccessException("no_grant");
        public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class CancellationAwareAccessClient : IManagedAccessClient
    {
        public Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OperationPermit(operation.IntentId, null, false));
        }
        public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class RecordingAccessClient : IManagedAccessClient
    {
        public List<string> Reports { get; } = [];
        public Task<OperationPermit> AuthorizeAsync(ManagedOperation operation, CancellationToken cancellationToken)
            => Task.FromResult(new OperationPermit(operation.IntentId, Guid.NewGuid(), false));
        public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
        {
            Reports.Add(outcome);
            return Task.CompletedTask;
        }
    }
}
