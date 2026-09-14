using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadQueueTests
{
    private static MediaCandidate Candidate(string id) => new(
        new Uri("https://cdn.example/" + id + ".m3u8"), new Uri("https://school.example/lesson"), "HLS");

    [Fact]
    public void Queue_keeps_per_video_quality_and_deduplicates_by_source()
    {
        var queue = new BrowserDownloadQueue();
        var video = Candidate("one");
        queue.AddOrUpdate(video, 1, "360p");
        queue.AddOrUpdate(video, 1, "720p");
        Assert.Single(queue.Items);
        Assert.Equal("720p", queue.Items[0].Quality);
    }

    [Fact]
    public void Queue_can_reorder_remove_success_and_keep_failed_items_for_resume()
    {
        var queue = new BrowserDownloadQueue();
        var one = Candidate("one"); var two = Candidate("two"); var three = Candidate("three");
        queue.AddOrUpdate(one, 1, "360p"); queue.AddOrUpdate(two, 2, "480p"); queue.AddOrUpdate(three, 3, "720p");
        Assert.True(queue.Move(2, -1));
        Assert.Equal(three.Source, queue.Items[1].Candidate.Source);
        Assert.True(queue.RemoveBySource(one.Source));
        Assert.Equal([three.Source, two.Source], queue.Items.Select(item => item.Candidate.Source));
    }
}