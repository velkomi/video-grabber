using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ComponentActivationTests
{
    [Fact]
    public void Pointer_selects_one_complete_verified_generation()
    {
        var root = CreateRoot();
        try
        {
            var generation = CreateGeneration(root, "g1");
            WritePointer(root, generation);
            var locator = new ToolLocator(Path.Combine(root, "app"), root);
            Assert.Equal(generation, Path.GetDirectoryName(locator.YtDlp));
            Assert.Equal(generation, Path.GetDirectoryName(locator.Ffmpeg));
            Assert.Equal(generation, Path.GetDirectoryName(locator.Ffprobe));
            Assert.Equal(generation, Path.GetDirectoryName(locator.Deno));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Pointer_failure_never_falls_back_per_file_to_legacy_tools()
    {
        var root = CreateRoot();
        try
        {
            foreach (var name in Names) File.WriteAllText(Path.Combine(root, name), "legacy");
            var generation = CreateGeneration(root, "g1");
            WritePointer(root, generation);
            File.Delete(Path.Combine(generation, "ffprobe.exe"));
            Assert.Throws<InvalidDataException>(() => new ToolLocator(Path.Combine(root, "app"), root));
        }
        finally { Directory.Delete(root, true); }
    }

    private static readonly string[] Names = ["yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe"];

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-components-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateGeneration(string root, string name)
    {
        var generation = Path.Combine(root, "generations", name);
        Directory.CreateDirectory(generation);
        foreach (var tool in Names) File.WriteAllText(Path.Combine(generation, tool), tool + "-content");
        return generation;
    }

    private static void WritePointer(string root, string generation)
    {
        var hashes = Names.ToDictionary(name => name,
            name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(generation, name)))).ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);
        var pointer = new
        {
            schemaVersion = 1,
            generation = Path.GetRelativePath(root, generation).Replace('\\', '/'),
            sha256 = hashes
        };
        File.WriteAllText(Path.Combine(root, "components-current.json"), JsonSerializer.Serialize(pointer));
    }
}

public sealed class ComponentTransactionTests
{
    private static readonly string[] ToolNames = ["yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe"];

    [Fact]
    public async Task Commit_switches_pointer_only_after_full_set_self_tests()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePrepared(root, nextGeneration);
            var runner = new RecordingRunner((_, _) => new ProcessResult(0, "ok", ""));

            await new ComponentInstallTransaction(runner).CommitAsync(root, CancellationToken.None);

            var locator = new ToolLocator(Path.Combine(root, "app"), root);
            Assert.Equal(nextGeneration, Path.GetDirectoryName(locator.YtDlp));
            Assert.Equal(4, runner.Specs.Count);
            Assert.All(runner.Specs, spec => Assert.StartsWith(nextGeneration, spec.FileName, StringComparison.OrdinalIgnoreCase));
            Assert.Contains("\"State\":\"committed\"", File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Self_test_failure_restores_previous_verified_pointer()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePrepared(root, nextGeneration);
            var runner = new RecordingRunner((spec, _) =>
                Path.GetFileName(spec.FileName).Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
                    ? new ProcessResult(9, "", "bad") : new ProcessResult(0, "ok", ""));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new ComponentInstallTransaction(runner).CommitAsync(root, CancellationToken.None));

            Assert.Equal(oldGeneration, Path.GetDirectoryName(new ToolLocator(Path.Combine(root, "app"), root).YtDlp));
            Assert.Contains("\"State\":\"rolledBack\"", File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Pending_journal_recovery_restores_old_pointer_before_readiness()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var oldPointer = File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName));
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePointer(root, nextGeneration);
            var newPointer = File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName));
            File.WriteAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName), JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                state = "pending",
                oldPointer,
                newPointer,
                generation = Path.GetRelativePath(root, nextGeneration).Replace('\\', '/')
            }));

            await new ComponentInstallTransaction(new RecordingRunner((_, _) => new ProcessResult(0, "", "")))
                .AbortRecoveryAsync(root, CancellationToken.None);

            Assert.Equal(oldGeneration, Path.GetDirectoryName(new ToolLocator(Path.Combine(root, "app"), root).YtDlp));
            Assert.Contains("rolledBack", File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName)),
                StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Transaction_lock_blocks_concurrent_commit_without_changing_pointer()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var originalPointer = File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName));
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePrepared(root, nextGeneration);
            using var held = new FileStream(Path.Combine(root, ".components-transaction.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            await Assert.ThrowsAnyAsync<IOException>(() =>
                new ComponentInstallTransaction(new RecordingRunner((_, _) => new ProcessResult(0, "ok", "")))
                    .CommitAsync(root, CancellationToken.None));

            Assert.Equal(originalPointer, File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Pending_journal_before_pointer_swap_is_idempotently_recovered_on_restart()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var oldPointer = File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName));
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePointer(root, nextGeneration);
            var newPointer = File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName));
            File.WriteAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName), oldPointer);
            File.WriteAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName), JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                state = "pending",
                oldPointer,
                newPointer,
                generation = Path.GetRelativePath(root, nextGeneration).Replace('\\', '/')
            }));

            await new ComponentInstallTransaction(new RecordingRunner((_, _) => new ProcessResult(0, "", "")))
                .AbortRecoveryAsync(root, CancellationToken.None);

            Assert.Equal(oldGeneration, Path.GetDirectoryName(new ToolLocator(Path.Combine(root, "app"), root).YtDlp));
            Assert.Contains("rolledBack", File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName)),
                StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Locked_pointer_fails_commit_without_mutation_and_restart_recovery_remains_possible()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var pointerPath = Path.Combine(root, ComponentInstallTransaction.PointerFileName);
            var originalPointer = File.ReadAllText(pointerPath);
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePrepared(root, nextGeneration);
            var transaction = new ComponentInstallTransaction(new RecordingRunner((_, _) => new ProcessResult(0, "ok", "")));

            using (var held = new FileStream(pointerPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await Assert.ThrowsAnyAsync<IOException>(() => transaction.CommitAsync(root, CancellationToken.None));
                Assert.Equal(originalPointer, File.ReadAllText(pointerPath));
            }

            await new ComponentInstallTransaction(new RecordingRunner((_, _) => new ProcessResult(0, "", "")))
                .AbortRecoveryAsync(root, CancellationToken.None);
            Assert.Equal(oldGeneration, Path.GetDirectoryName(new ToolLocator(Path.Combine(root, "app"), root).YtDlp));
        }
        finally { Directory.Delete(root, true); }
    }
    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-component-txn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateGeneration(string root, string name, string content)
    {
        var generation = Path.Combine(root, "generations", name);
        Directory.CreateDirectory(generation);
        foreach (var tool in ToolNames) File.WriteAllText(Path.Combine(generation, tool), content + ":" + tool);
        return generation;
    }

    private static void WritePrepared(string root, string generation)
    {
        File.WriteAllText(Path.Combine(root, ComponentInstallTransaction.PreparedFileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generation = Path.GetRelativePath(root, generation).Replace('\\', '/'),
                sha256 = Hashes(generation)
            }));
    }

    private static void WritePointer(string root, string generation)
    {
        File.WriteAllText(Path.Combine(root, ComponentInstallTransaction.PointerFileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generation = Path.GetRelativePath(root, generation).Replace('\\', '/'),
                sha256 = Hashes(generation)
            }));
    }

    private static Dictionary<string, string> Hashes(string generation)
        => ToolNames.ToDictionary(name => name,
            name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(generation, name)))).ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task User_cancel_during_real_commit_rolls_back_the_just_committed_pointer()
    {
        var root = CreateRoot();
        try
        {
            var oldGeneration = CreateGeneration(root, "old", "old");
            WritePointer(root, oldGeneration);
            var nextGeneration = CreateGeneration(root, "next", "next");
            WritePrepared(root, nextGeneration);
            var runner = new CommitBlockingRunner();
            var transaction = new ComponentInstallTransaction(runner);
            var installer = new ComponentInstaller(runner, transaction);
            using var cancellation = new CancellationTokenSource();

            var pending = installer.InstallAsync("synthetic-install.ps1", root, cancellation.Token);
            await runner.CommitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            runner.CommitRelease.TrySetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(oldGeneration, Path.GetDirectoryName(new ToolLocator(Path.Combine(root, "app"), root).YtDlp));
            Assert.Contains("rolledBack", File.ReadAllText(Path.Combine(root, ComponentInstallTransaction.JournalFileName)),
                StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class CommitBlockingRunner : IProcessRunner
    {
        private int _calls;
        public TaskCompletionSource CommitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CommitRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1) return new ProcessResult(0, "prepared", "");
            if (call == 2)
            {
                CommitEntered.TrySetResult();
                await CommitRelease.Task.WaitAsync(cancellationToken);
            }
            return new ProcessResult(0, "ok", "");
        }
    }
    private sealed class RecordingRunner(Func<ProcessSpec, int, ProcessResult> result) : IProcessRunner
    {
        public List<ProcessSpec> Specs { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Specs.Add(spec);
            return Task.FromResult(result(spec, Specs.Count));
        }
    }
}
