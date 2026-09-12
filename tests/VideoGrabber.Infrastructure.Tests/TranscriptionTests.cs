using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class TranscriptionTests
{
    [Fact]
    public async Task Missing_input_or_model_is_not_a_success_and_pre_cancel_throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-asr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new NeverRun();
            var service = new WhisperTranscriber(runner, new ToolLocator(root, root));
            var result = await service.TranscribeAsync(Path.Combine(root, "missing.mp4"), Path.Combine(root, "out"),
                Path.Combine(root, "whisper.exe"), Path.Combine(root, "model.bin"), "auto", CancellationToken.None);
            Assert.False(result.Success);
            Assert.Null(result.TextPath);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.TranscribeAsync("x", "out", "exe", "model", "auto", cts.Token));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class NeverRun : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => throw new InvalidOperationException("No process should start for invalid inputs.");
    }
}
