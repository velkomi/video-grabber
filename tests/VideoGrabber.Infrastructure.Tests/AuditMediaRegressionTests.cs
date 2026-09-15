using System.Security.Cryptography;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Media;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.Infrastructure.Tests;

// Audit-only negative regressions. Fixtures are synthetic and intentionally retained.
public sealed class AuditMediaRegressionTests
{
    [Fact]
    public async Task Failed_download_must_preserve_concurrent_unrelated_media()
    {
        var root = FixtureRoot();
        var foreign = Path.Combine(root, "unrelated-user-video.mp4");
        byte[] sentinel = [13, 17, 19, 23];
        var runner = new StubRunner((_, _) =>
        {
            File.WriteAllBytes(foreign, sentinel);
            return new ProcessResult(1, "", "ERROR: network failed");
        });
        var result = await Downloader(root, runner, new(false, false, false, null)).DownloadAsync(
            Request(root), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.True(File.Exists(foreign), "Recovery deleted media that was never owned by the download.");
        Assert.Equal(sentinel, File.ReadAllBytes(foreign));
        Assert.Equal(SHA256.HashData(sentinel), SHA256.HashData(File.ReadAllBytes(foreign)));
    }

    [Fact]
    public async Task Failed_download_must_not_promote_preexisting_exact_basename()
    {
        var root = FixtureRoot();
        var foreign = Path.Combine(root, "Lesson.mp4");
        byte[] sentinel = [29, 31, 37];
        File.WriteAllBytes(foreign, sentinel);
        var runner = new StubRunner((_, _) => new ProcessResult(1, "", "ERROR: denied"));
        var result = await Downloader(root, runner, ValidVideo()).DownloadAsync(
            Request(root), null, CancellationToken.None);
        Assert.False(result.Success, "A failed download claimed an unchanged pre-existing video as its output.");
        Assert.True(File.Exists(foreign));
        Assert.Equal(sentinel, File.ReadAllBytes(foreign));
        Assert.Equal(SHA256.HashData(sentinel), SHA256.HashData(File.ReadAllBytes(foreign)));
    }

    [Fact]
    public async Task Cancel_must_preserve_preexisting_similarly_named_temp()
    {
        var root = FixtureRoot();
        var foreign = Path.Combine(root, "Lesson - downloading-other.temp.mp4");
        byte[] sentinel = [41, 43, 47];
        File.WriteAllBytes(foreign, sentinel);
        using var cancel = new CancellationTokenSource();
        var runner = new StubRunner((_, _) =>
        {
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloader(root, runner, ValidVideo()).DownloadAsync(Request(root), null, cancel.Token));
        Assert.True(File.Exists(foreign), "Cancel cleanup deleted a pre-existing foreign temp file.");
        Assert.Equal(sentinel, File.ReadAllBytes(foreign));
        Assert.Equal(SHA256.HashData(sentinel), SHA256.HashData(File.ReadAllBytes(foreign)));
    }

    [Fact]
    public async Task Cancel_after_track_100_must_not_promote_old_prefix_video()
    {
        var root = FixtureRoot();
        var foreign = Path.Combine(root, "Lesson - downloading-old.mp4");
        byte[] sentinel = [53, 59, 61];
        File.WriteAllBytes(foreign, sentinel);
        using var cancel = new CancellationTokenSource();
        var runner = new StubRunner((_, output) =>
        {
            output?.Invoke("videograbber:100|100|NA|NA|NA|1MiB/s|00:00");
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloader(root, runner, ValidVideo()).DownloadAsync(Request(root), null, cancel.Token));
        Assert.True(File.Exists(foreign), "Cancellation recovery promoted an old prefix-matching file.");
        Assert.Equal(SHA256.HashData(sentinel), SHA256.HashData(File.ReadAllBytes(foreign)));
    }

    [Fact]
    public async Task Failed_fragment_download_must_not_succeed_with_short_playable_partial()
    {
        var root = FixtureRoot();
        var runner = new StubRunner((spec, output) =>
        {
            var args = spec.Arguments.ToList();
            var path = args[args.IndexOf("-o") + 1].Replace("%(ext)s", "ts", StringComparison.Ordinal);
            File.WriteAllBytes(path, [67, 71, 73]);
            output?.Invoke("filepath:" + path);
            return new ProcessResult(1, "", "ERROR: unable to download fragment 2: HTTP Error 403");
        });
        var result = await Downloader(root, runner, ValidVideo(2)).DownloadAsync(
            Request(root), null, CancellationToken.None);
        Assert.False(result.Success, "Positive probe duration cannot repair a missing-fragment error.");
    }

    [Fact]
    public async Task Video_request_must_reject_audio_only_media()
    {
        var root = FixtureRoot();
        var runner = new StubRunner((spec, output) =>
        {
            var args = spec.Arguments.ToList();
            var path = args[args.IndexOf("-o") + 1].Replace("%(ext)s", "m4a", StringComparison.Ordinal);
            File.WriteAllBytes(path, [79, 83, 89]);
            output?.Invoke("filepath:" + path);
            return new ProcessResult(0, "", "");
        });
        var result = await Downloader(root, runner, new(true, true, false, "aac", DurationSeconds: 10)).DownloadAsync(
            Request(root), null, CancellationToken.None);
        Assert.False(result.Success, "Video result must contain a video stream.");
    }

    [Fact]
    public void Short_readable_video_does_not_satisfy_expected_duration()
    {
        var request = new DownloadRequest(new Uri("https://cdn.example/video"), "output", "best", ExpectedDurationSeconds: 60, ExpectedAudio: true);
        Assert.False(DownloadOutputContract.IsSatisfied(request, new(true, true, true, "aac", DurationSeconds: 2)));
        Assert.True(DownloadOutputContract.IsSatisfied(request, new(true, true, true, "aac", DurationSeconds: 59.5)));
        Assert.False(DownloadOutputContract.IsSatisfied(request, new(true, true, false, "aac", DurationSeconds: 60)));
    }

    [Fact]
    public async Task CancelAfterFirstTrack100MustNotSucceed()
    {
        var root = FixtureRoot();
        using var cancel = new CancellationTokenSource();
        string? partial = null;
        var runner = new StubRunner((spec, output) =>
        {
            var args = spec.Arguments.ToList();
            partial = args[args.IndexOf("-o") + 1].Replace("%(ext)s", "mp4", StringComparison.Ordinal);
            File.WriteAllBytes(partial, [107, 109, 113]);
            output?.Invoke("filepath:" + partial);
            output?.Invoke("videograbber:100|100|NA|NA|NA|1MiB/s|00:00");
            cancel.Cancel();
            return new ProcessResult(0, "", "");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloader(root, runner, ValidVideo()).DownloadAsync(Request(root), null, cancel.Token));
        Assert.True(File.Exists(partial));
        Assert.Equal(new byte[] { 107, 109, 113 }, File.ReadAllBytes(partial!));
        Assert.Empty(Directory.GetFiles(root, "*.mp4"));
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(null, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Requested_audio_is_a_tristate_contract(bool? expectedAudio, bool hasAudio, bool satisfied)
    {
        var request = Request("output") with { ExpectedAudio = expectedAudio };
        Assert.Equal(satisfied, DownloadOutputContract.IsSatisfied(request, new(true, hasAudio, true, hasAudio ? "aac" : null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Unknown_duration_does_not_invent_a_completeness_requirement(double? expectedDuration)
    {
        var request = Request("output") with { ExpectedDurationSeconds = expectedDuration };
        Assert.True(DownloadOutputContract.IsSatisfied(request, ValidVideo(0)));
        Assert.False(DownloadOutputContract.IsSatisfied(request, new(false, true, true, "aac")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1\n00:00:05,000 --> 00:00:01,000\nSynthetic speech\n")]
    public async Task Transcription_must_reject_empty_or_reversed_srt(string subtitles)
    {
        var root = FixtureRoot();
        var input = Path.Combine(root, "synthetic.wav");
        var executable = Path.Combine(root, "synthetic-whisper.exe");
        var model = Path.Combine(root, "synthetic-model.bin");
        File.WriteAllBytes(input, [97]);
        File.WriteAllBytes(executable, [101]);
        File.WriteAllBytes(model, [103]);
        var runner = new StubRunner((spec, _) =>
        {
            var args = spec.Arguments.ToList();
            var outputIndex = args.IndexOf("-of");
            if (outputIndex >= 0)
            {
                var temporaryBase = args[outputIndex + 1];
                File.WriteAllText(temporaryBase + ".txt", "Synthetic speech for audit.");
                File.WriteAllText(temporaryBase + ".srt", subtitles);
            }
            return new ProcessResult(0, "", "");
        });
        var result = await new WhisperTranscriber(runner, new ToolLocator(root, root),
            new StubProbe(new(true, true, false, "pcm_s16le", DurationSeconds: 10))).TranscribeAsync(
                input, Path.Combine(root, "output"), executable, model, "en", CancellationToken.None);
        Assert.False(result.Success, "Existence of SRT without valid increasing cues must not count as success.");
        Assert.False(File.Exists(Path.Combine(root, "output.srt")));
    }

    private static string FixtureRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-full-audit-B-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static DownloadRequest Request(string root) =>
        new(new Uri("https://audit.example/master.m3u8"), root, "720p", DirectManifest: true, SuggestedBaseName: "Lesson");
    private static MediaProbeResult ValidVideo(double duration = 60) =>
        new(true, true, true, "aac", DurationSeconds: duration);
    private static YtDlpDownloader Downloader(string root, IProcessRunner runner, MediaProbeResult media) =>
        new(runner, new ToolLocator(root, root), new StubProbe(media));
    private sealed class StubProbe(MediaProbeResult media) : IMediaProbe
    {
        public Task<MediaProbeResult> ProbeAsync(string path, CancellationToken cancellationToken) => Task.FromResult(media);
    }
    private sealed class StubRunner(Func<ProcessSpec, Action<string>?, ProcessResult> run) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken) =>
            Task.FromResult(run(spec, onOutput));
    }
}
