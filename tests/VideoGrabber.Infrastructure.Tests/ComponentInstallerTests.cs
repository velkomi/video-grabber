using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Processes;

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
        Assert.Contains("-Phase", spec.Arguments);
        Assert.Contains("Prepare", spec.Arguments);
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
    public void App_prefers_bundled_runtime_and_does_not_auto_install_during_download()
    {
        var app = ReadApp("MainWindow.xaml.cs");
        var download = ReadApp("MainWindow.Download.cs");
        var media = ReadApp("MainWindow.MediaActions.cs");
        Assert.Contains("initialTools.UsesBundledRuntime", app);
        Assert.Contains("Встроенные инструменты", app);
        Assert.DoesNotContain("var install = PrimaryButton(\"Установить или обновить\")", app);
        Assert.DoesNotContain("InstallComponentsAsync(forceUpdate: false", download);
        Assert.Contains("Встроенные инструменты VideoGrabber отсутствуют или повреждены", download);
        Assert.Contains("service.RunAsync(intent, lease, operation.Token)", download);
        Assert.Contains("() => Volatile.Read(ref _componentServices).Downloader", download);
        Assert.Contains("components.Tools.WhisperCli", media);
        Assert.Contains("components.Tools.WhisperModel", media);
        Assert.Contains("components.Tools.WhisperAvailable", media);
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
public sealed class ComponentInstallerRecoveryTests
{
    [Fact]
    public async Task Cancel_during_commit_waits_for_abort_recovery_before_returning()
    {
        var runner = new SuccessRunner();
        var transaction = new BlockingTransaction();
        var installer = new ComponentInstaller(runner, transaction);
        using var cancellation = new CancellationTokenSource();

        var pending = installer.InstallAsync("install.ps1", "destination", cancellation.Token);
        await transaction.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Task.Delay(100);
        Assert.False(pending.IsCompleted, "Commit is shielded and cancellation must wait.");

        transaction.CommitRelease.TrySetResult();
        await transaction.AbortEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted, "InstallAsync returned before rollback/recovery completed.");
        transaction.AbortRelease.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(transaction.RecoveryTokenWasUsable);
    }

    private sealed class SuccessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => Task.FromResult(new ProcessResult(0, "ok", ""));
    }

    private sealed class BlockingTransaction : IComponentInstallTransaction
    {
        public TaskCompletionSource CommitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CommitRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AbortEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AbortRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool RecoveryTokenWasUsable { get; private set; }

        public async Task CommitAsync(string destination, CancellationToken recoveryDeadline)
        {
            RecoveryTokenWasUsable = !recoveryDeadline.IsCancellationRequested;
            CommitEntered.TrySetResult();
            await CommitRelease.Task.WaitAsync(recoveryDeadline);
        }

        public async Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline)
        {
            RecoveryTokenWasUsable &= !recoveryDeadline.IsCancellationRequested;
            AbortEntered.TrySetResult();
            await AbortRelease.Task.WaitAsync(recoveryDeadline);
        }
    }
}

public sealed class ComponentInstallerProcessTreeTests
{
    [Fact]
    public async Task Cancelled_prepare_kills_real_descendant_and_waits_for_recovery()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "VG-component-child-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "prepare.ps1");
        var pidPath = Path.Combine(root, "child.pid");
        File.WriteAllText(script, """
            param([string]$Destination,[string]$Phase)
            $child = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 60' -PassThru
            [IO.File]::WriteAllText((Join-Path $Destination 'child.pid'), [string]$child.Id)
            Start-Sleep -Seconds 60
            """);
        var transaction = new RecoveryRecordingTransaction();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pending = new ComponentInstaller(new ProcessRunner(), transaction)
                .InstallAsync(script, root, cancellation.Token);
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!File.Exists(pidPath) && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.True(File.Exists(pidPath), "Prepare fixture did not publish child PID.");
            var childPid = int.Parse(File.ReadAllText(pidPath));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(8)));
            Assert.True(transaction.Aborted);

            var exited = false;
            for (var i = 0; i < 80 && !exited; i++)
            {
                try
                {
                    using var child = System.Diagnostics.Process.GetProcessById(childPid);
                    child.Refresh();
                    exited = child.HasExited;
                }
                catch (ArgumentException) { exited = true; }
                if (!exited) await Task.Delay(25);
            }
            Assert.True(exited, "Cancelled Prepare left a descendant process alive.");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private sealed class RecoveryRecordingTransaction : IComponentInstallTransaction
    {
        public bool Aborted { get; private set; }
        public Task CommitAsync(string destination, CancellationToken recoveryDeadline)
            => throw new InvalidOperationException("Commit must not run after cancelled Prepare.");
        public Task AbortRecoveryAsync(string destination, CancellationToken recoveryDeadline)
        {
            recoveryDeadline.ThrowIfCancellationRequested();
            Aborted = true;
            return Task.CompletedTask;
        }
    }
}