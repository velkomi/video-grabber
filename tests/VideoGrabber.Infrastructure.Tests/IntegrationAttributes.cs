namespace VideoGrabber.Infrastructure.Tests;

public sealed class MediaToolsFactAttribute : FactAttribute
{
    public MediaToolsFactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.Combine(path, "ffmpeg.exe")) || !File.Exists(Path.Combine(path, "ffprobe.exe")))
            Skip = "Actual FFmpeg tools are not configured; this is not a passing integration test.";
    }
}

public sealed class WhisperToolsFactAttribute : FactAttribute
{
    public WhisperToolsFactAttribute()
    {
        foreach (var name in new[] { "VIDEOGRABBER_WHISPER_EXE", "VIDEOGRABBER_WHISPER_MODEL", "VIDEOGRABBER_WHISPER_SAMPLE" })
            if (!File.Exists(Environment.GetEnvironmentVariable(name))) { Skip = "Whisper executable, model or fixture is not configured."; break; }
    }
}
