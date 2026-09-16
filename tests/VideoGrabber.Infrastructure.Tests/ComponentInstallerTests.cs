using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ComponentInstallerTests
{
    [Fact]
    public async Task Prepare_is_noninteractive_bounded_and_commits_verified_success()
    {
        var runner = new RecordingRunner((_, _) => Task.FromResult(new ProcessResult(0, "ok", "")));
        var transaction = new RecordingTransaction();
        var installer = new ComponentInstaller(runner, transaction);
        var destination = Path.Combine(Path.GetTempPath(), "vg-components-destination");
        var script = Path.Combine(Path.GetTempPath(), "Install-Components.ps1");

        var result = await installer.InstallAsync(script, destination, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var spec = Assert.Single(runner.Specs);
        Assert.Equal(TimeSpan.FromMinutes(10), spec.Timeout);
        Assert.Contains("-NonInteractive", spec.Arguments);
        Assert.Contains(script, spec.Arguments);
        Assert.Contains(destination, spec.Arguments);
        Assert.Equal(destination, Assert.Single(transaction.Commits));
        Assert.Empty(transaction.Aborts);
    }

    [Fact]
    public async Task User_cancel_waits_for_abort_recovery_then_preserves_cancellation()
    {
        var runnerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new RecordingRunner(async (_, token) =>
        {
            runnerEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new ProcessResult(0, "", "");
        });
        var transaction = new RecordingTransaction();
        var installer = new ComponentInstaller(runner, transaction);
        using var cancellation = new CancellationTokenSource();
        var pending = installer.InstallAsync("install.ps1", "destination", cancellation.Token);
        await runnerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(6)));

        Assert.Empty(transaction.Commits);
        Assert.Equal("destination", Assert.Single(transaction.Aborts));
        Assert.True(transaction.RecoveryTokenWasUsable);
    }

    [Fact]
    public async Task Nonzero_prepare_runs_abort_recovery_and_never_commits()
    {
        var runner = new RecordingRunner((_, _) => Task.FromResult(new ProcessResult(7, "", "failed")));
        var transaction = new RecordingTransaction();
        var result = await new ComponentInstaller(runner, transaction)
            .InstallAsync("install.ps1", "destination", CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.Empty(transaction.Commits);
        Assert.Equal("destination", Assert.Single(transaction.Aborts));
    }

    [Fact]
    public async Task Deferred_transaction_never_activates_live_tools_and_cleans_owned_stage()
    {
        var stage = Path.Combine(Path.GetTempPath(), "VideoGrabber-component-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        await File.WriteAllTextAsync(Path.Combine(stage, "sentinel.txt"), "staged");
        var transaction = new DeferredComponentInstallTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync(stage, CancellationToken.None));
        Assert.True(Directory.Exists(stage));

        await transaction.AbortRecoveryAsync(stage, CancellationToken.None);
        Assert.False(Directory.Exists(stage));
    }

    [Fact]
    public void App_wires_manual_and_auto_install_to_owned_cancellation_lifetime()
    {
        var app = ReadApp("MainWindow.xaml.cs");
        var download = ReadApp("MainWindow.Download.cs");
        Assert.Contains("InstallComponentsAsync(bool forceUpdate, CancellationToken cancellationToken)", app);
        Assert.Contains("_operations.IsBusy || _isInstallingComponents", app);
        Assert.Contains("new ComponentInstaller(new ProcessRunner(), new DeferredComponentInstallTransaction())", app);
        Assert.Contains("InstallComponentsAsync(forceUpdate: false, operation.Token)", download);
        Assert.Contains("service.RunAsync(intent, lease, operation.Token)", download);
        Assert.Contains("if (!installed || installCompletion != OperationCompletion.None)", download);
    }

    private static string ReadApp(string name)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", name));
    }

    private sealed class RecordingRunner(
        Func<ProcessSpec, CancellationToken, Task<ProcessResult>> run) : IProcessRunner
    {
        public List<ProcessSpec> Specs { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Specs.Add(spec);
            return run(spec, cancellationToken);
        }
    }

    private sealed class RecordingTransaction : IComponentInstallTransaction
    {
        public List<string> Commits { get; } = [];
        public List<string> Aborts { get; } = [];
        public bool RecoveryTokenWasUsable { get; private set; }

        public Task CommitAsync(string destination, CancellationToken recoveryDeadline)
        {
            RecoveryTokenWasUsable = !recoveryDeadline.IsCancellationRequested;
            Commits.Add(destination);
            return Task.CompletedTask;
        }

        public Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline)
        {
            RecoveryTokenWasUsable = !recoveryDeadline.IsCancellationRequested;
            Aborts.Add(destination);
            return Task.CompletedTask;
        }
    }
}