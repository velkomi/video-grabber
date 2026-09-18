namespace VideoGrabber.Platform.Contracts;

public static class PlatformProtocol
{
    public const int Current = 2;
    public const int MinimumSupported = 1;
    public const int LegacyApiVersion = 1;

    public static bool IsSupported(int version)
        => version is >= MinimumSupported and <= Current;

    public static bool TryParseSupported(
        string? raw,
        out int version)
    {
        version = 0;
        return int.TryParse(raw, out version)
            && IsSupported(version);
    }

    public static string[] LegacyWorkerOperations()
        => ["download"];

    public static string[] CurrentWorkerOperations(
        bool serverAsrAvailable)
        => serverAsrAvailable
            ? ["download", "mp3", "trim", "join", "transcribe"]
            : ["download", "mp3", "trim", "join"];
}
