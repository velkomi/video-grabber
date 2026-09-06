namespace VideoGrabber.Core.Downloads;

public interface IVideoDownloader
{
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken);
}

