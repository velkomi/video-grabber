using System.Security.Cryptography;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class SplitHlsCleanupTests
{
    [Fact]
    public async Task Merge_failure_does_not_leave_partial_final_mp4_in_user_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-split-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = await new YtDlpDownloader(new MergeFailRunner(), new ToolLocator(root, root)).DownloadAsync(
                new DownloadRequest(new Uri("https://cdn.example/master.m3u8"), root, "best",
                    HlsVideoSource: new Uri("https://cdn.example/video.m3u8"),
                    HlsAudioSource: new Uri("https://cdn.example/audio.m3u8")), null, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Empty(Directory.GetFiles(root, "*.mp4", SearchOption.TopDirectoryOnly));
            var job = Assert.Single(Directory.GetDirectories(root, ".vg-job-*"));
            Assert.Contains(job, result.Details);
            foreach (var track in new[] { "video.mp4", "audio.mp4" })
                Assert.Equal(SHA256.HashData(new byte[] { 1, 2, 3 }), SHA256.HashData(File.ReadAllBytes(Path.Combine(job, track))));
            Assert.Equal(SHA256.HashData(new byte[] { 9, 9, 9 }), SHA256.HashData(File.ReadAllBytes(Path.Combine(job, "merged.mp4"))));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class MergeFailRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
        {
            if (spec.FileName.Contains("yt-dlp", StringComparison.OrdinalIgnoreCase))
            {
                var index = spec.Arguments.ToList().IndexOf("-o");
                var output = spec.Arguments[index + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllBytes(output, [1, 2, 3]);
                onOutput?.Invoke("filepath:" + output);
                return Task.FromResult(new ProcessResult(0, "", ""));
            }
            var partial = spec.Arguments[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
            File.WriteAllBytes(partial, [9, 9, 9]);
            return Task.FromResult(new ProcessResult(1, "", "synthetic merge failure"));
        }
    }
}
