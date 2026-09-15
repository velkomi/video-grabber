using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserSelectionRegressionTests
{
    [Fact]
    public void Removed_source_uses_first_sources_own_quality_and_empty_list_uses_best()
    {
        var qualities = new Dictionary<string, string>
        {
            [Part(4).Source.AbsoluteUri] = "720p",
            [Part(2).Source.AbsoluteUri] = "360p"
        };
        var remaining = MediaCandidateSelectionReducer.Reduce([Part(2), Part(1)], Part(4).Source, qualities);
        Assert.Equal(Part(2).Source, remaining.SelectedSource);
        Assert.Equal("360p", remaining.SelectedQuality);
        var unsaved = MediaCandidateSelectionReducer.Reduce([Part(1)], Part(4).Source, qualities);
        Assert.Equal("best", unsaved.SelectedQuality);
        var empty = MediaCandidateSelectionReducer.Reduce([], remaining.SelectedSource, qualities);
        Assert.Null(empty.SelectedSource);
        Assert.Equal("best", empty.SelectedQuality);
        Assert.Empty(empty.Candidates);
    }

    [Fact]
    public void Selection_uses_exact_absolute_source_and_copies_input_order()
    {
        var first = Part(4) with { Source = new Uri("https://cdn.example/master.m3u8#one") };
        var second = first with { Source = new Uri("https://cdn.example/master.m3u8#two") };
        var input = new List<MediaCandidate> { first, second };
        var result = MediaCandidateSelectionReducer.Reduce(input, second.Source,
            new Dictionary<string, string> { [second.Source.AbsoluteUri] = "1440p" });
        input.Clear();
        Assert.Equal(second.Source.AbsoluteUri, result.SelectedSource!.AbsoluteUri);
        Assert.Equal("1440p", result.SelectedQuality);
        Assert.Equal(new[] { first, second }, result.Candidates);
    }

    [Fact]
    public void Cdp_then_duplicate_WebResource_keeps_part4_quality_and_download_plan()
    {
        const string body = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=500000,RESOLUTION=640x360\n360.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=1500000,RESOLUTION=1280x720\n720.m3u8";
        var part4 = Part(4);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            requestId = "request4", frameId = "frame4",
            response = new { url = part4.Source.AbsoluteUri, mimeType = "application/vnd.apple.mpegurl", status = 200 }
        });
        Assert.True(DevToolsHlsSnifferParser.TryParseHlsResponse(json, out var cdp));
        Assert.True(HlsManifestParser.TryParse(body, cdp!.Source, out var info));
        var confirmed = part4 with { FrameId = cdp.FrameId, HlsManifest = info! with { DurationSeconds = 42 } };
        Assert.True(HlsResponseCandidateResolver.TryResolve(body, part4.Source, part4.Referer, out var webResource, out var blocked));
        Assert.False(blocked);
        Assert.Null(webResource!.FrameId);
        Assert.Null(webResource.HlsManifest!.DurationSeconds);
        var merged = MediaCandidateMerge.Merge(MediaCandidateMerge.Merge(confirmed, webResource), webResource);
        var qualities = new Dictionary<string, string> { [part4.Source.AbsoluteUri] = "720p" };
        var selected = MediaCandidateSelectionReducer.Reduce([Part(6), merged, Part(2)], part4.Source, qualities);
        var candidate = Assert.Single(selected.Candidates, item => item.Source == selected.SelectedSource);
        var queue = new BrowserDownloadQueue();
        queue.AddOrUpdate(candidate, candidate.PageOrdinal!.Value, selected.SelectedQuality);
        var queued = Assert.Single(queue.Items);
        var plan = MediaDownloadPlanResolver.Resolve(queued.Candidate, queued.Quality, false);

        Assert.Equal(part4.Source, queued.Candidate.Source);
        Assert.Equal(4, queued.Ordinal);
        Assert.Equal("frame4", queued.Candidate.FrameId);
        Assert.Equal(42d, queued.Candidate.HlsManifest!.DurationSeconds);
        Assert.Equal("720p", queued.Quality);
        Assert.Equal(new Uri("https://cdn.example/part4/720.m3u8"), plan.Source);
    }

    private static MediaCandidate Part(int part) => new(
        new Uri($"https://cdn.example/part{part}/master.m3u8"),
        new Uri("https://school.example/lesson"), "HLS",
        FrameId: $"frame{part}", PageOrdinal: part, PageSectionTitle: $"PART {part}");

    [Fact]
    public void Part4_and_720p_survive_reverse_arrivals_and_late_metadata()
    {
        var part4 = Part(4);
        var qualities = new Dictionary<string, string> { [part4.Source.AbsoluteUri] = "720p" };
        var initial = MediaCandidateSelectionReducer.Reduce([part4], null, qualities);
        var reversed = Enumerable.Range(1, 6).Reverse().Select(Part).ToArray();
        var reordered = MediaCandidateSelectionReducer.Reduce(reversed, initial.SelectedSource, qualities);
        Assert.Equal(part4.Source, reordered.SelectedSource);
        Assert.Equal("720p", reordered.SelectedQuality);

        var metadataUpdated = reversed.OrderBy(candidate => candidate.PageOrdinal)
            .Select(candidate => candidate with { Details = "late metadata" }).ToArray();
        var refreshed = MediaCandidateSelectionReducer.Reduce(metadataUpdated, reordered.SelectedSource, qualities);
        Assert.Equal(part4.Source, refreshed.SelectedSource);
        Assert.Equal("720p", refreshed.SelectedQuality);
        Assert.Equal("late metadata", refreshed.Candidates.Single(candidate => candidate.Source == refreshed.SelectedSource).Details);
    }
}
