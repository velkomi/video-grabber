namespace VideoGrabber.Core.Editing;

public enum VideoEditMode
{
    FastTrim,
    Join
}

public sealed record VideoEditRequest(
    VideoEditMode Mode,
    IReadOnlyList<string> Inputs,
    string OutputPath,
    TimeSpan? Start = null,
    TimeSpan? Duration = null);

