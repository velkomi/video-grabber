using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadQueueTests
{
    [Fact]
    public void Queue_captures_page_titles_slots_quality_and_order_without_shared_collections()
    {
        var titles = new List<string> { "Part A" };
        var slots = new List<BrowserPlayerSlot> { new(4, "Part A", new("https://school.example/a")) };
        var candidate = Candidate("one") with { PageOrdinal = 4, PageSectionTitle = "Part A" };
        var context = new BrowserQueueContext(candidate.Referer, 1, new("Lesson A", titles, slots));
        var queue = new BrowserDownloadQueue();
        var selections = new Dictionary<string, string> { [candidate.Source.AbsoluteUri] = "720p" };
        queue.AddOrUpdate(candidate, 4, selections[candidate.Source.AbsoluteUri], context);
        queue.AddOrUpdate(Candidate("two"), 2, "360p", context);
        selections[candidate.Source.AbsoluteUri] = "1080p";
        titles[0] = "Page B";
        slots.Clear();
        var saved = queue.Items[0];
        Assert.Equal(context, saved.Context);
        Assert.Equal("720p", saved.Quality);
        Assert.Equal(4, saved.Ordinal);
        Assert.Equal("Part A", Assert.Single(saved.Context!.Metadata.SectionTitles));
        Assert.Equal("Part A", Assert.Single(saved.Context.Metadata.PlayerSlots).Title);
        Assert.Equal("Lesson A", saved.Context.Metadata.PageTitle);
        Assert.False(saved.Context.Metadata.SectionTitles is List<string>);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)saved.Context.Metadata.SectionTitles)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<BrowserPlayerSlot>)saved.Context.Metadata.PlayerSlots).Clear());
        Assert.Equal(new[] { candidate.Source, Candidate("two").Source }, queue.Items.Select(x => x.Candidate.Source));
        Assert.False(queue.Items is List<BrowserDownloadQueueItem>);
    }

    [Fact]
    public void Queue_copies_mutable_manifest_lists()
    {
        var variants = new List<HlsVariant> { new(new("https://cdn.example/720.m3u8"), 1280, 720, null, null, null) };
        var audio = new List<HlsAudioRendition> { new(new("https://cdn.example/audio.m3u8"), "ru", "Audio", null, true) };
        var candidate = Candidate("one") with { HlsManifest = new(true, variants, audio, false, null, false) };
        var queue = new BrowserDownloadQueue();
        queue.AddOrUpdate(candidate, 4, "720p");
        variants.Clear();
        audio.Clear();
        var saved = Assert.Single(queue.Items);
        Assert.Single(saved.Candidate.HlsManifest!.Variants);
        Assert.Single(saved.Candidate.HlsManifest.AudioRenditions);
        Assert.Throws<NotSupportedException>(() => ((IList<HlsVariant>)saved.Candidate.HlsManifest.Variants).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HlsAudioRendition>)saved.Candidate.HlsManifest.AudioRenditions).Clear());
    }

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
