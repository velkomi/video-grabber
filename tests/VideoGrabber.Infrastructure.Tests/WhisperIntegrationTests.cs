using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Processes;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class WhisperIntegrationTests
{
    [WhisperToolsFact]
    public async Task Real_whisper_generates_meaningful_text_and_timed_subtitles()
    {
        var root = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var outputBase = Path.Combine(root, "whisper-jfk-" + Guid.NewGuid().ToString("N"));
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var result = await new WhisperTranscriber(new ProcessRunner(), tools).TranscribeAsync(
            Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_SAMPLE")!, outputBase,
            Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_EXE")!, Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_MODEL")!,
            "en", timeout.Token);
        Assert.True(result.Success, result.Message);
        var text = await File.ReadAllTextAsync(result.TextPath!, timeout.Token);
        Assert.Contains("country", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(text.Trim().Length > 40);
        var subtitles = await File.ReadAllTextAsync(result.SubtitlesPath!, timeout.Token);
        Assert.Contains(" --> ", subtitles);
        Assert.Matches(@"\d{2}:\d{2}:\d{2},\d{3}", subtitles);
        Assert.DoesNotContain(Directory.GetDirectories(root), d => Path.GetFileName(d).StartsWith(".vg-asr-", StringComparison.Ordinal));
        var again = await new WhisperTranscriber(new ProcessRunner(), tools).TranscribeAsync(
            Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_SAMPLE")!, outputBase,
            Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_EXE")!, Environment.GetEnvironmentVariable("VIDEOGRABBER_WHISPER_MODEL")!,
            "en", timeout.Token);
        Assert.False(again.Success);
        Assert.Equal(text, await File.ReadAllTextAsync(result.TextPath!, timeout.Token));
    }
}
