using VideoGrabber.Infrastructure.Browser;
using Xunit;

namespace VideoGrabber.Infrastructure.Tests;

// Audit-only acceptance tests: failures characterize the unchanged product snapshot.
// Coordinator copies this file to the isolated audit worktree after baseline.
public sealed class AuditBrowserRegressionTests
{
    private static readonly Uri Lesson = new("https://school.example/lesson");
    private static Uri Player(int part) => new($"https://api1.gcvh.ru/sign-player/?part={part}");
    private static Uri Master(int part) => new($"https://cdn.example/part-{part}/master.m3u8");

    private static HlsManifestInfo Manifest(params int[] heights) => new(
        true,
        heights.Select(h => new HlsVariant(new Uri($"https://cdn.example/{h}.m3u8"),
            h * 16 / 9, h, h * 1000L, 30, null)).ToArray(),
        [], false, null, false);

    private static MediaCandidate Part(int part, bool exactReferer) => new(
        Master(part),
        exactReferer ? Player(part) : Lesson,
        "HLS",
        HlsManifest: Manifest(720) with
        {
            // PART 1 and PART 2 deliberately have equal durations.
            DurationSeconds = part <= 2 ? 10d : part * 10d
        });

    private static BrowserPageMetadata SixSlots() => new(
        "SYNTHETIC LESSON",
        Enumerable.Range(1, 6).Select(i => $"PART {i}").ToArray(),
        Enumerable.Range(1, 6).Select(i => new BrowserPlayerSlot(i, $"PART {i}", Player(i))).ToArray());

    [Fact]
    public void Audit_A001_Unknown_binding_must_not_invent_part_numbers_from_reverse_arrival()
    {
        var candidates = Enumerable.Range(1, 6).Reverse().Select(i => Part(i, false)).ToArray();
        Assert.Equal(candidates.Single(c => c.Source == Master(1)).HlsManifest!.DurationSeconds,
            candidates.Single(c => c.Source == Master(2)).HlsManifest!.DurationSeconds);

        var bound = BrowserFrameBindingResolver.BindAll(candidates, [], SixSlots());

        Assert.Equal(candidates.Select(c => c.Source), bound.Select(c => c.Source));
        Assert.All(bound, c => Assert.Null(c.PageOrdinal));
        Assert.All(bound, c => Assert.Null(c.PageSectionTitle));
    }

    [Fact]
    public void Audit_A001_Frame_urls_bind_six_parts_independently_of_frame_and_arrival_order()
    {
        var order = new[] { 3, 1, 6, 2, 5, 4 };
        var frames = order.Select((part, index) => new DevToolsFrameInfo($"f{part}", Player(part), index)).ToArray();
        var candidates = order.Reverse().Select(part => Part(part, false) with { FrameId = $"f{part}" }).ToArray();

        var bound = BrowserFrameBindingResolver.BindAll(candidates, frames, SixSlots());

        foreach (var part in order)
        {
            var candidate = Assert.Single(bound, c => c.Source == Master(part));
            Assert.Equal(part, candidate.PageOrdinal);
            Assert.Equal($"PART {part}", candidate.PageSectionTitle);
            Assert.Equal(candidate, BrowserFrameBindingResolver.Bind(candidates.Single(c => c.Source == Master(part)), frames, SixSlots()));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("absent")]
    public void Audit_A001_Missing_frame_evidence_clears_obsolete_part_bindings(string? frameId)
    {
        var candidates = Enumerable.Range(1, 6).Reverse().Select(part => Part(part, false) with
            { FrameId = frameId, PageOrdinal = part, PageSectionTitle = $"PART {part}" }).ToArray();
        var frames = Enumerable.Range(1, 6).Select(part => new DevToolsFrameInfo($"f{part}", Player(part), 7 - part)).ToArray();

        Assert.All(BrowserFrameBindingResolver.BindAll(candidates, frames, SixSlots()), Unknown);
        Assert.All(candidates.Select(c => BrowserFrameBindingResolver.Bind(c, frames, SixSlots())), Unknown);
    }

    [Fact]
    public void Audit_A001_Conflicting_frame_id_or_referer_evidence_is_unknown()
    {
        var duplicateId = new[] { new DevToolsFrameInfo("same", Player(1), 1), new DevToolsFrameInfo("same", Player(2), 2) };
        var candidate = Part(1, false) with { FrameId = "same" };
        Unknown(BrowserFrameBindingResolver.Bind(candidate, duplicateId, SixSlots()));
        Assert.All(BrowserFrameBindingResolver.BindAll([candidate], duplicateId, SixSlots()), Unknown);

        var conflicting = Part(1, true) with { FrameId = "f2" };
        var frames = new[] { new DevToolsFrameInfo("f2", Player(2), 1) };
        Unknown(BrowserFrameBindingResolver.Bind(conflicting, frames, SixSlots()));
        Assert.All(BrowserFrameBindingResolver.BindAll([conflicting], frames, SixSlots()), Unknown);
    }

    [Fact]
    public void Audit_A001_Frame_position_without_full_dom_url_match_is_unknown()
    {
        var candidate = Part(1, false) with { FrameId = "f1" };
        var frames = new[] { new DevToolsFrameInfo("f1", new Uri(Player(1).AbsoluteUri + "&other=1"), 0) };
        Unknown(BrowserFrameBindingResolver.Bind(candidate, frames, SixSlots()));
        Assert.All(BrowserFrameBindingResolver.BindAll([candidate], frames, SixSlots()), Unknown);
    }

    private static void Unknown(MediaCandidate candidate)
    {
        Assert.Null(candidate.PageOrdinal);
        Assert.Null(candidate.PageSectionTitle);
    }

    [Fact]
    public void Audit_Control_Exact_full_player_referer_binds_six_reverse_arrivals_to_correct_parts()
    {
        var candidates = Enumerable.Range(1, 6).Reverse().Select(i => Part(i, true)).ToArray();

        var bound = BrowserFrameBindingResolver.BindAll(candidates, [], SixSlots());

        for (var part = 1; part <= 6; part++)
        {
            var candidate = Assert.Single(bound, c => c.Source == Master(part));
            Assert.Equal(part, candidate.PageOrdinal);
            Assert.Equal($"PART {part}", candidate.PageSectionTitle);
        }
    }

    [Fact]
    public void Audit_A003_Explicit_1440p_selects_1440_and_not_2160()
    {
        var selected = HlsTrackSelector.Select(Manifest(720, 1440, 2160), "1440p");

        Assert.NotNull(selected);
        Assert.Equal(1440, selected!.Video.Height);
        Assert.Equal(new Uri("https://cdn.example/1440.m3u8"), selected.Video.Uri);
    }

    [Fact]
    public void Audit_A003_Queue_preserves_explicit_available_1440p()
    {
        var queue = new BrowserDownloadQueue();
        var candidate = Part(1, true) with { HlsManifest = Manifest(1440, 2160) };

        queue.AddOrUpdate(candidate, 1, "1440p");

        Assert.Equal("1440p", Assert.Single(queue.Items).Quality);
    }

    [Theory]
    [InlineData("Infinity")]
    [InlineData("1e309")]
    [InlineData("1e20")]
    public void Audit_A008_Extinf_cannot_publish_nonfinite_or_TimeSpan_overflow_duration(string duration)
    {
        var body = "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXTINF:" + duration +
                   ",\nsegment.ts\n#EXT-X-ENDLIST\n";

        var parsed = HlsManifestParser.TryParse(body, new Uri("https://cdn.example/media.m3u8"), out var info);

        // Reject the malformed playlist or preserve it with an unknown safe duration.
        Assert.True(!parsed || info?.DurationSeconds is null ||
                    (double.IsFinite(info.DurationSeconds.Value) &&
                     info.DurationSeconds.Value > 0 &&
                     info.DurationSeconds.Value < TimeSpan.MaxValue.TotalSeconds),
            "A parsed EXTINF must not poison presentation with a nonfinite or excessive duration.");
    }

    [Fact]
    public void Audit_A009_Live_window_is_not_the_total_media_duration()
    {
        const string body = "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:600\n" +
                            "#EXTINF:10,\na.ts\n#EXTINF:10,\nb.ts\n#EXTINF:10,\nc.ts\n";

        Assert.True(HlsManifestParser.TryParse(body, new Uri("https://cdn.example/live.m3u8"), out var info));

        Assert.NotNull(info);
        Assert.Null(info!.DurationSeconds);
    }

    [Fact]
    public void Audit_Control_Vod_endlist_has_fractional_total_duration()
    {
        const string body = "#EXTM3U\n#EXT-X-TARGETDURATION:11\n#EXTINF:10.5,\na.ts\n" +
                            "#EXTINF:10.25,\nb.ts\n#EXT-X-ENDLIST\n";

        Assert.True(HlsManifestParser.TryParse(body, new Uri("https://cdn.example/vod.m3u8"), out var info));

        Assert.Equal(20.75, info!.DurationSeconds);
    }

    [Fact]
    public void Audit_A014_No_usable_selected_track_must_not_pass_Hls_verification()
    {
        var candidate = Part(1, true) with { HlsManifest = Manifest(720) };
        // A previously saved cap can be unavailable after a refreshed manifest.
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "360p", audioOnly: false);
        Assert.Equal(candidate.Source, plan.Source);
        Assert.Null(plan.HlsVideoSource);
        Assert.Null(plan.HlsAudioSource);

        var verified = HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(verified, "An empty selected-track set does not prove an uninspected master safe.");
    }
}
