using System.Security.Cryptography;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Worker;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class WorkerMediaOperationTests
{
    [Theory]
    [InlineData("mp3")]
    [InlineData("trim")]
    public async Task Single_input_operations_create_verified_artifact(string operation)
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var tools = Tools();
            var runner = new BoundedProcessRunner();
            var input = await MakeVideoAsync(root, "input.mp4", tools, runner, 3);
            var descriptor = Descriptor(input, "video/mp4");
            var resolver = new FixedArtifacts([descriptor]);
            var executor = Executor(root, tools, runner, resolver);
            var lease = Lease(
                operation,
                [descriptor.ArtifactId],
                operation == "trim" ? 500 : null,
                operation == "trim" ? 1000 : null);

            var receipt = await executor.ExecuteAsync(lease, CancellationToken.None);

            Assert.True(receipt.Bytes > 0);
            Assert.Equal(64, receipt.Sha256.Length);
            Assert.True(File.Exists(receipt.StoragePath));
            Assert.Equal(
                operation == "mp3" ? "audio/mpeg" : "video/mp4",
                receipt.MediaType);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Join_two_compatible_owned_inputs_creates_verified_video()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var tools = Tools();
            var runner = new BoundedProcessRunner();
            var first = await MakeVideoAsync(root, "one.mp4", tools, runner, 1);
            var second = await MakeVideoAsync(root, "two.mp4", tools, runner, 1);
            var descriptors = new[]
            {
                Descriptor(first, "video/mp4"),
                Descriptor(second, "video/mp4")
            };
            var executor = Executor(
                root, tools, runner, new FixedArtifacts(descriptors));
            var lease = Lease(
                "join", descriptors.Select(x => x.ArtifactId).ToArray(), null, null);

            var receipt = await executor.ExecuteAsync(lease, CancellationToken.None);

            Assert.Equal("video/mp4", receipt.MediaType);
            Assert.True(receipt.Bytes > 0);
            Assert.True(File.Exists(receipt.StoragePath));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Transcribe_fails_closed_without_qualified_model()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var tools = Tools();
            var runner = new BoundedProcessRunner();
            var input = await MakeVideoAsync(root, "speech-input.mp4", tools, runner, 1);
            var descriptor = Descriptor(input, "video/mp4");
            var executor = Executor(
                root, tools, runner, new FixedArtifacts([descriptor]), whisperModel: null);
            var lease = Lease("transcribe", [descriptor.ArtifactId], null, null);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => executor.ExecuteAsync(lease, CancellationToken.None));
            Assert.Equal("server_asr_model_unavailable", ex.Message);
        }
        finally { SafeDelete(root); }
    }

    private static MediaJobExecutor Executor(
        string root,
        WorkerToolLocator tools,
        BoundedProcessRunner runner,
        IWorkerArtifactResolver artifacts,
        string? whisperModel = null)
        => new(
            runner,
            tools,
            new ArtifactVerifier(runner, tools),
            new NeverSourceResolver(),
            artifacts,
            Path.Combine(root, "jobs"),
            new Uri("http://127.0.0.1:3128/"),
            socialProxyUri: null,
            whisperModel: whisperModel);

    private static AttemptLease Lease(
        string kind,
        Guid[] inputs,
        long? start,
        long? duration)
    {
        var work = new CreateJob(
            Guid.NewGuid(),
            new string('a', 64),
            kind,
            "server_worker",
            null,
            "src_fixture",
            "720p",
            inputs,
            start,
            duration);
        return new AttemptLease(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow.AddMinutes(1),
            "capability",
            work);
    }

    private static WorkerArtifactDescriptor Descriptor(string path, string mediaType)
    {
        var bytes = File.ReadAllBytes(path);
        return new WorkerArtifactDescriptor(
            Guid.NewGuid(),
            path,
            mediaType,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            bytes.LongLength);
    }

    private static async Task<string> MakeVideoAsync(
        string root,
        string name,
        WorkerToolLocator tools,
        BoundedProcessRunner runner,
        int seconds)
    {
        var path = Path.Combine(root, name);
        var result = await runner.RunAsync(new WorkerProcessSpec(
            tools.Ffmpeg,
            ["-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=25",
             "-f","lavfi","-i","sine=frequency=880:sample_rate=44100",
             "-t",seconds.ToString(),
             "-c:v","libx264","-threads","1","-pix_fmt","yuv420p",
             "-c:a","aac","-shortest","-y",path],
            root,
            TimeSpan.FromMinutes(1)), CancellationToken.None);
        Assert.True(result.Success, result.StandardError);
        return path;
    }

    private static WorkerToolLocator Tools()
        => new(
            Environment.GetEnvironmentVariable("VG_WORKER_YTDLP") ?? "yt-dlp",
            Environment.GetEnvironmentVariable("VG_WORKER_FFMPEG") ?? "ffmpeg",
            Environment.GetEnvironmentVariable("VG_WORKER_FFPROBE") ?? "ffprobe",
            Environment.GetEnvironmentVariable("VG_WORKER_WHISPER") ?? "whisper-cli");

    private sealed class FixedArtifacts(
        IReadOnlyList<WorkerArtifactDescriptor> values) : IWorkerArtifactResolver
    {
        public Task<IReadOnlyList<WorkerArtifactDescriptor>> ResolveArtifactsAsync(
            AttemptLease lease,
            CancellationToken cancellationToken)
            => Task.FromResult(values);
    }

    private sealed class NeverSourceResolver : IWorkerSourceResolver
    {
        public Task<WorkerResolvedSource> ResolveAsync(
            string sourceId,
            string quality,
            CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("Source resolver must not be called for artifact operation.");
    }

    private static string TempRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "vg-worker-ops-" + Guid.NewGuid().ToString("N"));

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
