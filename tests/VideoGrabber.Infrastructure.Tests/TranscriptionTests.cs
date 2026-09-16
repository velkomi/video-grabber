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

public sealed class SrtValidatorTests
{
    [Fact]
    public void Valid_unicode_cues_pass_and_reversed_or_empty_fail()
    {
        Assert.True(SrtValidator.TryValidate(
            "1\n00:00:00,100 --> 00:00:01,000\nПривет, мир\n", 10, out _));
        Assert.False(SrtValidator.TryValidate(
            "1\n00:00:05,000 --> 00:00:01,000\nSpeech\n", 10, out _));
        Assert.False(SrtValidator.TryValidate("", 10, out _));
    }

    [Fact]
    public void Null_duration_disables_only_duration_bound()
    {
        var cue = "1\n00:00:09,000 --> 00:00:11,000\nLong speech\n";
        Assert.True(SrtValidator.TryValidate(cue, null, out _));
        Assert.False(SrtValidator.TryValidate(cue, 5, out _));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_nonnull_duration_is_rejected(double duration)
    {
        Assert.False(SrtValidator.TryValidate(
            "1\n00:00:00,100 --> 00:00:01,000\nSpeech\n", duration, out _));
    }

    [Fact]
    public void Overlap_decreasing_starts_and_empty_cue_text_are_rejected()
    {
        Assert.False(SrtValidator.TryValidate(
            "1\n00:00:00,000 --> 00:00:02,000\nA\n\n2\n00:00:01,900 --> 00:00:03,000\nB\n", 10, out _));
        Assert.False(SrtValidator.TryValidate(
            "1\n00:00:02,000 --> 00:00:03,000\nA\n\n2\n00:00:01,000 --> 00:00:02,000\nB\n", 10, out _));
        Assert.False(SrtValidator.TryValidate(
            "1\n00:00:00,000 --> 00:00:01,000\n   \n", 10, out _));
    }

    [Fact]
    public void Bom_crlf_and_multiline_unicode_are_accepted()
    {
        var text = "\uFEFF1\r\n00:00:00,000 --> 00:00:01,000\r\nПервая строка\r\n第二行\r\n\r\n"
            + "2\r\n00:00:01,000 --> 00:00:02,000\r\nمرحبا\r\n";
        Assert.True(SrtValidator.TryValidate(text, 2, out var error), error);
    }
}

public sealed class WhisperPromotionTests
{
    [Fact]
    public async Task Validated_pair_is_promoted_only_after_both_artifacts_pass()
    {
        var root = CreateRoot();
        try
        {
            var output = Path.Combine(root, "out");
            var runner = RunnerWriting(outputText: "Speech", outputSrt:
                "1\n00:00:00,000 --> 00:00:01,000\nSpeech\n");
            var result = await Service(root, runner, 10).TranscribeAsync(
                Path.Combine(root, "input.wav"), output, Path.Combine(root, "whisper.exe"),
                Path.Combine(root, "model.bin"), "en", CancellationToken.None);
            Assert.True(result.Success, result.Message);
            Assert.Equal("Speech", await File.ReadAllTextAsync(output + ".txt"));
            Assert.True(SrtValidator.TryValidate(await File.ReadAllTextAsync(output + ".srt"), 10, out _));
            Assert.Empty(Directory.GetDirectories(root, ".vg-asr-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Malformed_utf8_is_not_promoted_and_owned_artifacts_are_retained()
    {
        var root = CreateRoot();
        try
        {
            var output = Path.Combine(root, "out");
            var runner = RunnerWriting("Speech", null, invalidSrtUtf8: true);
            var result = await Service(root, runner, 10).TranscribeAsync(
                Path.Combine(root, "input.wav"), output, Path.Combine(root, "whisper.exe"),
                Path.Combine(root, "model.bin"), "en", CancellationToken.None);
            Assert.False(result.Success);
            Assert.False(File.Exists(output + ".txt"));
            Assert.False(File.Exists(output + ".srt"));
            Assert.Single(Directory.GetDirectories(root, ".vg-asr-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Promotion_race_never_overwrites_foreign_output()
    {
        var root = CreateRoot();
        try
        {
            var output = Path.Combine(root, "out");
            var runner = RunnerWriting("Speech",
                "1\n00:00:00,000 --> 00:00:01,000\nSpeech\n",
                beforeWhisperReturns: () => File.WriteAllText(output + ".srt", "foreign"));
            var result = await Service(root, runner, 10).TranscribeAsync(
                Path.Combine(root, "input.wav"), output, Path.Combine(root, "whisper.exe"),
                Path.Combine(root, "model.bin"), "en", CancellationToken.None);
            Assert.False(result.Success);
            Assert.False(File.Exists(output + ".txt"));
            Assert.Equal("foreign", File.ReadAllText(output + ".srt"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cancellation_after_whisper_artifacts_retains_owned_evidence()
    {
        var root = CreateRoot();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var output = Path.Combine(root, "out");
            var runner = RunnerWriting("Speech",
                "1\n00:00:00,000 --> 00:00:01,000\nSpeech\n",
                beforeWhisperReturns: () => cancellation.Cancel(), throwCancellation: true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(root, runner, 10).TranscribeAsync(
                Path.Combine(root, "input.wav"), output, Path.Combine(root, "whisper.exe"),
                Path.Combine(root, "model.bin"), "en", cancellation.Token));
            Assert.False(File.Exists(output + ".txt"));
            Assert.False(File.Exists(output + ".srt"));
            Assert.Single(Directory.GetDirectories(root, ".vg-asr-*"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Held_foreign_output_is_never_overwritten_or_deleted()
    {
        var root = CreateRoot();
        FileStream? foreign = null;
        try
        {
            var output = Path.Combine(root, "out");
            var runner = RunnerWriting("Speech",
                "1\n00:00:00,000 --> 00:00:01,000\nSpeech\n",
                beforeWhisperReturns: () => foreign = new FileStream(
                    output + ".srt", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
            var result = await Service(root, runner, 10).TranscribeAsync(
                Path.Combine(root, "input.wav"), output, Path.Combine(root, "whisper.exe"),
                Path.Combine(root, "model.bin"), "en", CancellationToken.None);
            Assert.False(result.Success);
            Assert.False(File.Exists(output + ".txt"));
            Assert.True(File.Exists(output + ".srt"));
            Assert.True(File.Exists(Path.Combine(root, "input.wav")));
            Assert.NotNull(foreign);
            foreign!.Dispose(); foreign = null;
            Assert.Equal(0, new FileInfo(output + ".srt").Length);
        }
        finally
        {
            foreign?.Dispose();
            Directory.Delete(root, true);
        }
    }
    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "VG-asr-promote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "input.wav"), [1]);
        File.WriteAllBytes(Path.Combine(root, "whisper.exe"), [1]);
        File.WriteAllBytes(Path.Combine(root, "model.bin"), [1]);
        return root;
    }

    private static WhisperTranscriber Service(string root, IProcessRunner runner, double duration)
        => new(runner, new ToolLocator(root, root), new FixedProbe(duration));

    private static IProcessRunner RunnerWriting(string outputText, string? outputSrt,
        bool invalidSrtUtf8 = false, Action? beforeWhisperReturns = null, bool throwCancellation = false)
        => new ScriptedRunner((spec, token) =>
        {
            var args = spec.Arguments.ToList();
            var outputIndex = args.IndexOf("-of");
            if (outputIndex < 0) return Task.FromResult(new ProcessResult(0, "", ""));
            var outputBase = args[outputIndex + 1];
            File.WriteAllText(outputBase + ".txt", outputText);
            if (invalidSrtUtf8) File.WriteAllBytes(outputBase + ".srt", [0xC3, 0x28]);
            else File.WriteAllText(outputBase + ".srt", outputSrt ?? string.Empty);
            beforeWhisperReturns?.Invoke();
            if (throwCancellation) throw new OperationCanceledException(token);
            return Task.FromResult(new ProcessResult(0, "", ""));
        });
    private sealed class ScriptedRunner(
        Func<ProcessSpec, CancellationToken, Task<ProcessResult>> run) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            ProcessSpec spec, Action<string>? onOutput, CancellationToken cancellationToken)
            => run(spec, cancellationToken);
    }

    private sealed class FixedProbe(double duration) : VideoGrabber.Core.Media.IMediaProbe
    {
        public Task<VideoGrabber.Core.Media.MediaProbeResult> ProbeAsync(
            string path, CancellationToken cancellationToken)
            => Task.FromResult(new VideoGrabber.Core.Media.MediaProbeResult(
                true, true, false, "pcm_s16le", DurationSeconds: duration));
    }
}