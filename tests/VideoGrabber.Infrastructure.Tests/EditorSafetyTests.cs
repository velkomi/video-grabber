using VideoGrabber.Core.Editing;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Editing;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class EditorSafetyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Editor_rejects_overwrite_and_negative_start_before_launch(bool overwrite)
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "source.mp4");
        File.WriteAllText(input, "keep");
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => new FfmpegVideoEditor(new NeverRun(), new ToolLocator(root, root)).EditAsync(
                new VideoEditRequest(VideoEditMode.FastTrim, [input], overwrite ? input : Path.Combine(root, "out.mp4"),
                    TimeSpan.FromSeconds(overwrite ? 0 : -1), TimeSpan.FromSeconds(1)), CancellationToken.None));
            Assert.Equal("keep", File.ReadAllText(input));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class NeverRun : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Unsafe request reached the process runner.");
    }
}
