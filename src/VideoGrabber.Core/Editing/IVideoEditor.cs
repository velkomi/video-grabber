namespace VideoGrabber.Core.Editing;

public interface IVideoEditor
{
    Task EditAsync(VideoEditRequest request, CancellationToken cancellationToken);
}

