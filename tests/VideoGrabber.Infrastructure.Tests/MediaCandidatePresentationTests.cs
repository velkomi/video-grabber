using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class MediaCandidatePresentationTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void Unconfirmed_duration_cannot_erase_valid_metadata_or_complete_revalidation(double value)
    {
        var previous = Master() with { HlsManifest = Master().HlsManifest! with { DurationSeconds = 42 } };
        var incoming = previous with { HlsManifest = previous.HlsManifest! with { DurationSeconds = value } };
        Assert.Equal(42d, MediaCandidateMerge.Merge(previous, incoming).HlsManifest!.DurationSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => MediaCandidateMerge.ConfirmDuration(previous, value));
    }

    [Fact]
    public void Different_sources_cannot_merge_metadata()
    {
        var previous = Master();
        Assert.Throws<ArgumentException>(() => MediaCandidateMerge.Merge(previous,
            previous with { Source = new Uri("https://cdn.example/another/master.m3u8") }));
    }

    [Fact]
    public void Successful_duration_revalidation_clears_only_duration_conflict()
    {
        var candidate = Master() with { FrameEvidenceConflicted = true, DurationNeedsRevalidation = true };
        var confirmed = MediaCandidateMerge.ConfirmDuration(candidate, 42);
        Assert.Equal(42d, confirmed.HlsManifest!.DurationSeconds);
        Assert.False(confirmed.DurationNeedsRevalidation);
        Assert.True(confirmed.FrameEvidenceConflicted);
        var duplicate = MediaCandidateMerge.Merge(confirmed, new(candidate.Source, candidate.Referer, "HLS"));
        Assert.Equal(42d, duplicate.HlsManifest!.DurationSeconds);
        Assert.False(duplicate.DurationNeedsRevalidation);
    }

    [Fact]
    public void Conflicting_finite_durations_become_unknown_and_sparse_duplicates_cannot_restore_them()
    {
        var previous = Master() with { HlsManifest = Master().HlsManifest! with { DurationSeconds = 42 } };
        var incoming = previous with { HlsManifest = previous.HlsManifest! with { DurationSeconds = 84 } };
        var merged = MediaCandidateMerge.Merge(previous, incoming);
        Assert.Null(merged.HlsManifest!.DurationSeconds);
        Assert.True(merged.DurationNeedsRevalidation);
        var sparse = new MediaCandidate(previous.Source, previous.Referer, "HLS");
        var replayed = MediaCandidateMerge.Merge(MediaCandidateMerge.Merge(merged, sparse), previous);
        Assert.Null(replayed.HlsManifest!.DurationSeconds);
        Assert.True(replayed.DurationNeedsRevalidation);
    }

    [Fact]
    public void Conflicting_frame_evidence_stays_unknown_after_sparse_duplicates_and_binding_refresh()
    {
        var player = new Uri("https://school.example/player4");
        var previous = Master() with { Referer = player, FrameId = "frame4", PageOrdinal = 4, PageSectionTitle = "PART 4" };
        var conflict = MediaCandidateMerge.Merge(previous, previous with { FrameId = "other-frame" });
        Assert.Null(conflict.FrameId);
        Assert.Null(conflict.PageOrdinal);
        Assert.Null(conflict.PageSectionTitle);
        Assert.True(conflict.FrameEvidenceConflicted);

        var sparse = new MediaCandidate(previous.Source, player, "HLS");
        var duplicate = MediaCandidateMerge.Merge(conflict, sparse);
        var replay = MediaCandidateMerge.Merge(duplicate, previous);
        var metadata = new BrowserPageMetadata("Lesson", ["PART 4"], [new(4, "PART 4", player)]);
        var rebound = BrowserFrameBindingResolver.Bind(replay, [new("frame4", player, 1)], metadata);
        Assert.Null(rebound.FrameId);
        Assert.Null(rebound.PageOrdinal);
        Assert.Null(rebound.PageSectionTitle);
        Assert.True(rebound.FrameEvidenceConflicted);
    }

    [Fact]
    public void Refreshed_manifest_preserves_duration_and_sparse_discovery_preserves_tracks()
    {
        var previous = Master() with { HlsManifest = Master().HlsManifest! with { DurationSeconds = 42 } };
        var incoming = previous with
        {
            HlsManifest = previous.HlsManifest! with
            {
                DurationSeconds = null,
                Variants = [new(new Uri("https://cdn.example/new720.m3u8"), 1280, 720, 2_000_000, 30, null)]
            }
        };
        var merged = MediaCandidateMerge.Merge(previous, incoming);
        Assert.Equal(42d, merged.HlsManifest!.DurationSeconds);
        Assert.Equal(new Uri("https://cdn.example/new720.m3u8"), Assert.Single(merged.HlsManifest.Variants).Uri);

        var sparseDiscovery = incoming with { HlsManifest = null };
        Assert.Equal(merged.HlsManifest, MediaCandidateMerge.Merge(merged, sparseDiscovery).HlsManifest);
    }

    [Theory]
    [InlineData("best")]
    [InlineData("720p")]
    public async Task Explicit_empty_master_replaces_tracks_and_cannot_download_previous_selection(string quality)
    {
        var previous = Master() with
        {
            HlsManifest = Master().HlsManifest! with
            {
                DurationSeconds = 42,
                AudioRenditions = [new(new("https://cdn.example/audio.m3u8"), "ru", "Russian", "audio", true)]
            }
        };
        Assert.True(HlsManifestParser.TryParse("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\n", previous.Source, out var emptyMaster));
        Assert.True(emptyMaster!.IsMaster);
        var incoming = new MediaCandidate(previous.Source, previous.Referer, "HLS", HlsManifest: emptyMaster);
        var merged = MediaCandidateMerge.Merge(previous, incoming);
        Assert.Empty(merged.HlsManifest!.Variants);
        Assert.Empty(merged.HlsManifest.AudioRenditions);
        Assert.Equal(42d, merged.HlsManifest.DurationSeconds);
        var sparseDuplicate = new MediaCandidate(previous.Source, previous.Referer, "HLS");
        merged = MediaCandidateMerge.Merge(merged, sparseDuplicate);
        Assert.Empty(merged.HlsManifest!.Variants);
        var plan = MediaDownloadPlanResolver.Resolve(merged, quality, false);
        Assert.False(plan.IsResolved);
        var verificationCalls = 0;
        var downloaderCalls = 0;
        var result = await HlsDownloadPolicy.RunVerifiedAsync(merged, plan,
            () => { verificationCalls++; return Task.FromResult(true); },
            () => { downloaderCalls++; return Task.FromResult(true); }, _ => false);
        Assert.False(result);
        Assert.Equal(0, verificationCalls);
        Assert.Equal(0, downloaderCalls);
    }

    [Fact]
    public void Sparse_discovery_preserves_confirmed_frame_and_duration()
    {
        var previous = Master() with
        {
            FrameId = "frame4", PageOrdinal = 4, PageSectionTitle = "PART 4",
            HlsManifest = Master().HlsManifest! with { DurationSeconds = 42 }
        };
        var sparse = new MediaCandidate(previous.Source, previous.Referer, "HLS");
        var merged = MediaCandidateMerge.Merge(previous, sparse);
        Assert.Equal("frame4", merged.FrameId);
        Assert.Equal(4, merged.PageOrdinal);
        Assert.Equal("PART 4", merged.PageSectionTitle);
        Assert.Equal(42d, merged.HlsManifest!.DurationSeconds);
        Assert.Equal(new[] { 360, 720 }, merged.HlsManifest.Variants.Select(variant => variant.Height!.Value));
    }

    private static MediaCandidate Master()
    {
        var info = new HlsManifestInfo(true,
        [
            new(new Uri("https://cdn.example/v360.m3u8"), 640, 360, 500_000, 30, null),
            new(new Uri("https://cdn.example/v720.m3u8"), 1280, 720, 1_500_000, 30, null)
        ], [], false, null, false);
        return new(new Uri("https://api.example/master?id=secret"), new Uri("https://school.example/lesson"), "HLS", HlsManifest: info);
    }

    [Fact]
    public void Unknown_master_label_uses_item_counter_and_available_qualities_without_claiming_a_part()
    {
        var metadata = new BrowserPageMetadata("День 1", ["Часть 1", "Часть 2"]);
        var label = MediaCandidatePresentation.DisplayName(Master(), 2, metadata);
        Assert.Contains("Видео 02", label);
        Assert.Contains("Привязка к части не подтверждена", label);
        Assert.DoesNotContain("Часть 2", label);
        Assert.Contains("360p", label);
        Assert.Contains("720p", label);
    }

    [Fact]
    public void Child_media_playlist_is_technical_when_referenced_by_known_master()
    {
        var child = new MediaCandidate(new Uri("https://cdn.example/v720.m3u8"), new Uri("https://school.example/lesson"), "HLS",
            HlsManifest: new HlsManifestInfo(false, [], [], false, null, false));
        Assert.True(MediaCandidatePresentation.IsTechnicalChild(child, [Master()]));
    }

    [Fact]
    public void Suggested_filename_is_ordered_human_readable_and_has_no_signed_url_material()
    {
        var metadata = new BrowserPageMetadata("День 1", ["Часть 1"]);
        var name = MediaCandidatePresentation.SuggestedBaseName(Master(), 1, "720p", metadata);
        Assert.DoesNotContain("01 - ", name, StringComparison.Ordinal);
        Assert.DoesNotContain("Часть 1", name);
        Assert.Contains("source-", name);
        Assert.Equal(name, MediaCandidatePresentation.SuggestedBaseName(Master(), 6, "720p", metadata));
        Assert.NotEqual(name, MediaCandidatePresentation.SuggestedBaseName(Master() with
            { Source = new Uri("https://api.example/master?id=another-secret") }, 1, "720p", metadata));
        Assert.Contains("720p", name);
        Assert.DoesNotContain("secret", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.example", name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(name, DownloadFileName.SanitizeBaseName(name));
    }
}
