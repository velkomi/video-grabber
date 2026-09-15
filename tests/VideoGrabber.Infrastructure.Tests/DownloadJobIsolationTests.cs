using System.Diagnostics;
using System.Security.Cryptography;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadJobIsolationTests
{
    [Fact]
    public void Workspace_rejects_sibling_prefix_and_preexisting_files()
    {
        var root = FixtureRoot();
        var sentinel = Path.Combine(root, "Lesson.mp4");
        File.WriteAllBytes(sentinel, [1, 2, 3]);
        var hash = FileHash(sentinel);
        var job = DownloadWorkspace.Create(root, null);
        Assert.Empty(Directory.GetFileSystemEntries(job.Root));
        Assert.False(job.Owns(sentinel));
        Assert.False(job.Owns(job.Root + "-sibling\\file.mp4"));
        Assert.Throws<InvalidOperationException>(() => job.RegisterCreatedFile(sentinel));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(sentinel));
        AssertUnchanged(sentinel, hash);
        var created = Path.Combine(job.Root, "created.mp4");
        File.WriteAllBytes(created, [5, 7, 11]);
        Assert.False(job.Owns(created));
        job.RegisterCreatedFile(created);
        Assert.True(job.Owns(created));
        Assert.Contains(created, job.OwnedFiles);
    }

    [Fact]
    public async Task Download_runs_inside_job_directory_then_promotes_verified_file_to_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-job-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var job = Path.Combine(output, "job-parent");
        Directory.CreateDirectory(output);
        try
        {
            Directory.CreateDirectory(job);
            var sentinel = Path.Combine(job, "Lesson.mp4");
            File.WriteAllBytes(sentinel, [11, 13, 17]);
            var hash = FileHash(sentinel);
            var runner = new JobRunner();
            var probe = new StubProbe(new MediaProbeResult(true, true, true, "aac", DurationSeconds: 3723));
            var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), probe).DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), output, "720p", DirectManifest: true,
                    SuggestedBaseName: "01 - Часть 1 - 720p", JobDirectory: job), null, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.OutputPath);
            Assert.StartsWith(output, result.OutputPath!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("01h02m03s", Path.GetFileName(result.OutputPath!), StringComparison.Ordinal);
            Assert.Equal(job, Path.GetDirectoryName(runner.ObservedWorkingDirectory));
            Assert.StartsWith(".vg-job-", Path.GetFileName(runner.ObservedWorkingDirectory));
            Assert.False(Directory.Exists(runner.ObservedWorkingDirectory));
            Assert.True(Directory.Exists(job));
            AssertUnchanged(sentinel, hash);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BogusFilepathOutsideJobMustFail(bool split)
    {
        var root = FixtureRoot();
        var sentinel = Path.Combine(root, "Lesson.mp4");
        File.WriteAllBytes(sentinel, [19, 23, 29]);
        var hash = FileHash(sentinel);
        var runner = new CallbackRunner((spec, output) =>
        {
            File.WriteAllBytes(Path.Combine(spec.WorkingDirectory!, "owned.mp4"), [1, 2, 3]);
            output?.Invoke("filepath:" + sentinel);
            return new(0, "", "");
        });
        var result = await Downloader(root, runner).DownloadAsync(Request(root, split), null, CancellationToken.None);
        Assert.False(result.Success);
        AssertUnchanged(sentinel, hash);
        Assert.Single(Directory.GetFiles(root, "*.mp4"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelWithOnlyRecoverableTempMustPreserveIt(bool split)
    {
        var root = FixtureRoot();
        using var cancel = new CancellationTokenSource();
        string? partial = null;
        string? hash = null;
        var messages = new List<DownloadProgress>();
        var runner = new CallbackRunner((spec, output) =>
        {
            partial = Path.Combine(spec.WorkingDirectory!, "Lesson - downloading.temp.mp4");
            File.WriteAllBytes(partial, [31, 37, 41]);
            hash = FileHash(partial);
            output?.Invoke("videograbber:100|100|NA|NA|NA|1MiB/s|00:00");
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        });
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloader(root, runner).DownloadAsync(Request(root, split), new InlineProgress(messages.Add), cancel.Token));
        AssertUnchanged(partial!, hash!);
        Assert.Equal(Path.GetDirectoryName(partial), error.Data["JobDirectory"]);
        Assert.Contains(messages, message => message.Status.Contains(Path.GetDirectoryName(partial!)!, StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(root, "*.mp4"));
    }

    [Fact]
    public async Task JunctionMustNotEscapeJob()
    {
        var root = FixtureRoot();
        var output = Path.Combine(root, "output");
        var foreign = Path.Combine(root, "foreign");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(foreign);
        var sentinel = Path.Combine(foreign, "Lesson.mp4");
        File.WriteAllBytes(sentinel, [43, 47, 53]);
        var hash = FileHash(sentinel);
        var junction = Path.Combine(output, "linked-parent");
        CreateJunction(junction, foreign);
        Assert.Throws<InvalidOperationException>(() => DownloadWorkspace.Create(output, junction));
        Assert.Throws<InvalidOperationException>(() => DownloadWorkspace.Create(junction, null));
        var runner = new CallbackRunner((spec, callback) =>
        {
            var link = Path.Combine(spec.WorkingDirectory!, "escape");
            CreateJunction(link, foreign);
            callback?.Invoke("filepath:" + Path.Combine(link, "Lesson.mp4"));
            return new(0, "", "");
        });
        var result = await Downloader(root, runner).DownloadAsync(Request(output), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Reparse", result.Details);
        AssertUnchanged(sentinel, hash);
        Assert.Empty(Directory.GetDirectories(foreign));
    }

    [Fact]
    public async Task ReadOnlyForeignFileMustBeUnchanged()
    {
        var root = FixtureRoot();
        var sentinel = Path.Combine(root, "Lesson.mp4");
        File.WriteAllBytes(sentinel, [59, 61, 67]);
        var hash = FileHash(sentinel);
        File.SetAttributes(sentinel, FileAttributes.ReadOnly);
        var result = await Downloader(root, new CallbackRunner((_, callback) =>
        {
            callback?.Invoke("filepath:" + sentinel);
            return new(1, "", "synthetic failure");
        })).DownloadAsync(Request(root), null, CancellationToken.None);
        Assert.False(result.Success);
        AssertUnchanged(sentinel, hash);
        Assert.True((File.GetAttributes(sentinel) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public async Task Promotion_rejects_reparse_destination_and_preserves_source()
    {
        var root = FixtureRoot();
        var output = Path.Combine(root, "output");
        var foreign = Path.Combine(root, "foreign");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(foreign);
        var sentinel = Path.Combine(foreign, "original.mp4");
        File.WriteAllBytes(sentinel, [157, 163, 167]);
        var hash = FileHash(sentinel);
        CreateJunction(Path.Combine(output, "Lesson.mp4"), foreign);
        string? source = null;
        var runner = new CallbackRunner((spec, callback) =>
        {
            source = Path.Combine(spec.WorkingDirectory!, "source.mp4");
            File.WriteAllBytes(source, [173, 179, 181]);
            callback?.Invoke("filepath:" + source);
            return new(0, "", "");
        });
        var result = await Downloader(root, runner).DownloadAsync(Request(output), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Reparse", result.Details);
        AssertUnchanged(sentinel, hash);
        Assert.Equal(new byte[] { 173, 179, 181 }, File.ReadAllBytes(source!));
    }

    [Fact]
    public async Task HeldHandleMustRetainOwnedPartial()
    {
        var root = FixtureRoot();
        string? partial = null;
        string? hash = null;
        FileStream? held = null;
        try
        {
            var runner = new CallbackRunner((spec, callback) =>
            {
                partial = Path.Combine(spec.WorkingDirectory!, "held.mp4");
                File.WriteAllBytes(partial, [71, 73, 79]);
                hash = FileHash(partial);
                held = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.Read);
                callback?.Invoke("filepath:" + partial);
                return new(0, "", "");
            });
            var result = await Downloader(root, runner).DownloadAsync(Request(root), null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Contains(Path.GetDirectoryName(partial!)!, result.Details);
            AssertUnchanged(partial!, hash!);
            Assert.Empty(Directory.GetFiles(root, "*.mp4"));
        }
        finally { held?.Dispose(); }
        AssertUnchanged(partial!, hash!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimulatedDiskFullMustNotRemoveOriginal(bool split)
    {
        var root = FixtureRoot();
        var sentinel = Path.Combine(root, "Lesson.mp4");
        File.WriteAllBytes(sentinel, [83, 89, 97]);
        var sentinelHash = FileHash(sentinel);
        string? partial = null;
        string? partialHash = null;
        var files = new FixtureFileOperations();
        var runner = new CallbackRunner((spec, _) =>
        {
            partial = Path.Combine(spec.WorkingDirectory!, "recoverable.temp.mp4");
            files.WriteAllBytes(partial, [101, 103, 107]);
            partialHash = FileHash(partial);
            files.DiskFull = true;
            files.WriteAllBytes(Path.Combine(spec.WorkingDirectory!, "next.part"), [109]);
            throw new InvalidOperationException("The disk-full fixture did not fail.");
        });
        var result = await Downloader(root, runner).DownloadAsync(Request(root, split), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("disk full", result.Details);
        Assert.Contains(Path.GetDirectoryName(partial!)!, result.Details);
        AssertUnchanged(sentinel, sentinelHash);
        AssertUnchanged(partial!, partialHash!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Promotion_collision_preserves_original_and_explicit_parent(bool split)
    {
        var root = FixtureRoot();
        var parent = Path.Combine(root, "requested-parent");
        Directory.CreateDirectory(parent);
        var sentinel = Path.Combine(root, "Lesson.mp4");
        var parentSentinel = Path.Combine(parent, "old.temp.mp4");
        File.WriteAllBytes(sentinel, [113, 127]);
        File.WriteAllBytes(parentSentinel, [131, 137]);
        var hash = FileHash(sentinel);
        var parentHash = FileHash(parentSentinel);
        string? jobRoot = null;
        var runner = new CallbackRunner((spec, callback) =>
        {
            jobRoot = spec.WorkingDirectory;
            Assert.Equal(parent, Path.GetDirectoryName(jobRoot));
            Assert.StartsWith(".vg-job-", Path.GetFileName(jobRoot));
            var index = spec.Arguments.ToList().IndexOf("-o");
            var path = index >= 0 ? spec.Arguments[index + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal) : spec.Arguments[^1];
            File.WriteAllBytes(path, [139, 149, 151]);
            callback?.Invoke("filepath:" + path);
            return new(0, "", "");
        });
        var result = await Downloader(root, runner).DownloadAsync(Request(root, split) with { JobDirectory = parent }, null, CancellationToken.None);
        Assert.True(result.Success, result.Message + result.Details);
        Assert.Equal(Path.Combine(root, "Lesson (2).mp4"), result.OutputPath);
        Assert.Equal(new byte[] { 139, 149, 151 }, File.ReadAllBytes(result.OutputPath!));
        AssertUnchanged(sentinel, hash);
        AssertUnchanged(parentSentinel, parentHash);
        Assert.False(Directory.Exists(jobRoot));
    }

    [Fact]
    public void Workspace_rejects_outside_parent_before_writing()
    {
        var root = FixtureRoot();
        var output = Path.Combine(root, "output");
        var outside = Path.Combine(root, "output-sibling", "new-parent");
        Assert.Throws<InvalidOperationException>(() => DownloadWorkspace.Create(output, outside));
        Assert.False(Directory.Exists(output));
        Assert.False(Directory.Exists(outside));
    }

    [Fact]
    public async Task Successful_cleanup_preserves_files_not_registered_after_tool_exit()
    {
        var root = FixtureRoot();
        string? intermediate = null;
        string? lateFile = null;
        var runner = new CallbackRunner((spec, callback) =>
        {
            var source = Path.Combine(spec.WorkingDirectory!, "source.mp4");
            intermediate = Path.Combine(spec.WorkingDirectory!, "source.temp.mp4");
            File.WriteAllBytes(source, [191, 193, 197]);
            File.WriteAllBytes(intermediate, [199, 211]);
            callback?.Invoke("filepath:" + source);
            return new(0, "", "");
        });
        var probe = new CallbackProbe(path =>
        {
            lateFile = Path.Combine(Path.GetDirectoryName(path)!, "unregistered.temp.mp4");
            File.WriteAllBytes(lateFile, [223, 227, 229]);
            return new(true, true, true, "aac");
        });
        var result = await new YtDlpDownloader(runner, new ToolLocator(root, root), probe)
            .DownloadAsync(Request(root), null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.False(File.Exists(intermediate));
        Assert.True(File.Exists(lateFile));
        Assert.Equal(SHA256.HashData(new byte[] { 223, 227, 229 }), SHA256.HashData(File.ReadAllBytes(lateFile!)));
        Assert.Equal(Path.Combine(root, "Lesson.mp4"), result.OutputPath);
        Assert.Equal(new byte[] { 191, 193, 197 }, File.ReadAllBytes(result.OutputPath!));
    }

    private static string FixtureRoot()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath(),
            "ownership-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void AssertUnchanged(string path, string hash)
    {
        Assert.True(File.Exists(path), "Original path must remain: " + path);
        Assert.Equal(hash, FileHash(path));
    }

    private static void CreateJunction(string path, string target)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{path.Replace("'", "''", StringComparison.Ordinal)}' -Target '{target.Replace("'", "''", StringComparison.Ordinal)}' -ErrorAction Stop | Out-Null");
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(10_000), "BLOCKED: junction fixture timed out.");
        Assert.True(process.ExitCode == 0, "BLOCKED: junction fixture unavailable. " + stdout + stderr);
        Assert.True((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
    }

    private static DownloadRequest Request(string root, bool split = false) => new(
        new Uri("https://audit.example/master.m3u8"), root, "720p", SuggestedBaseName: "Lesson",
        HlsVideoSource: split ? new Uri("https://audit.example/video.m3u8") : null,
        HlsAudioSource: split ? new Uri("https://audit.example/audio.m3u8") : null);
    private static YtDlpDownloader Downloader(string root, IProcessRunner runner) =>
        new(runner, new ToolLocator(root, root), new StubProbe(new(true, true, true, "aac")));
    private sealed class CallbackRunner(Func<ProcessSpec, Action<string>?, ProcessResult> run) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => Task.FromResult(run(spec, onOutput));
    }
    private sealed class InlineProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
    private sealed class CallbackProbe(Func<string, MediaProbeResult> probe) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(probe(path));
    }
    private sealed class FixtureFileOperations
    {
        public bool DiskFull { get; set; }
        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (DiskFull) throw new IOException("Synthetic disk full", unchecked((int)0x80070070));
            File.WriteAllBytes(path, bytes);
        }
    }

    private sealed class JobRunner : IProcessRunner
    {
        public string? ObservedWorkingDirectory { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            ObservedWorkingDirectory = spec.WorkingDirectory;
            var path = Path.Combine(spec.WorkingDirectory!, "01 - Часть 1 - 720p.mp4");
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            onOutput?.Invoke("filepath:" + path);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
