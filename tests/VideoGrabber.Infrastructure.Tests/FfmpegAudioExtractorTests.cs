using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Audio;
using VideoGrabber.Infrastructure.Components;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class FfmpegAudioExtractorTests
{
    [Fact]
    public async Task ExtractAsync_rejects_missing_no_audio_overwrite_and_pre_cancel()
    {
        var root = Path.Combine(Path.GetTempPath(), "VideoGrabberTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runner = new RecordingRunner();
        var probe = new StubProbe(new MediaProbeResult(true, false, true, null));
        var extractor = new FfmpegAudioExtractor(runner, new ToolLocator(root, root), probe);
        try
        {
            var missing = await extractor.ExtractAsync(Path.Combine(root, "missing.mp4"), Path.Combine(root, "missing.mp3"), CancellationToken.None);
            Assert.False(missing.Success);
            Assert.Empty(runner.Specs);

            var source = Path.Combine(root, "source.mp4");
            await File.WriteAllTextAsync(source, "media");
            var noAudio = await extractor.ExtractAsync(source, Path.Combine(root, "no-audio.mp3"), CancellationToken.None);
            Assert.False(noAudio.Success);
            Assert.Empty(runner.Specs);

            var existing = Path.Combine(root, "existing.mp3");
            await File.WriteAllTextAsync(existing, "keep");
            var overwrite = await extractor.ExtractAsync(source, existing, CancellationToken.None);
            Assert.False(overwrite.Success);
            Assert.Equal("keep", await File.ReadAllTextAsync(existing));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                extractor.ExtractAsync(source, Path.Combine(root, "cancelled.mp3"), cancelled.Token));
            Assert.Empty(runner.Specs);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<ProcessSpec> Specs { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            Specs.Add(spec);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class StubProbe(MediaProbeResult result) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
