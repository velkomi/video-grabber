using VideoGrabber.Core.Processes;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Editing;
using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.Infrastructure.Components;

public sealed record ComponentServices(
    ToolLocator Tools,
    YtDlpDownloader Downloader,
    FfmpegVideoEditor Editor,
    WhisperTranscriber Transcriber);

public static class ComponentServiceFactory
{
    public static ComponentServices Create(
        ComponentSetSnapshot snapshot,
        IProcessRunner runner,
        IManagedEgressSessionRegistry? egressRegistry = null)
        => Create(new ToolLocator(snapshot), runner, egressRegistry);

    public static ComponentServices Create(
        ToolLocator tools,
        IProcessRunner runner,
        IManagedEgressSessionRegistry? egressRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(runner);
        return new ComponentServices(
            tools,
            new YtDlpDownloader(runner, tools, egressRegistry: egressRegistry),
            new FfmpegVideoEditor(runner, tools),
            new WhisperTranscriber(runner, tools));
    }
}
