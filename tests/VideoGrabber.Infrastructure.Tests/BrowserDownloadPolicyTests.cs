using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserDownloadPolicyTests
{
    [Theory]
    [InlineData("best", true)]
    [InlineData("360p", false)]
    [InlineData("480p", false)]
    [InlineData("invalid", false)]
    [InlineData("1080p", false)]
    public void Unavailable_master_selection_never_verifies_even_with_master_cached(string quality, bool empty)
    {
        var source = new Uri("https://cdn.example/master.m3u8");
        var info = new HlsManifestInfo(true,
            empty ? [] : [new HlsVariant(new("https://cdn.example/720.m3u8"), 1280, 720, 1, 30, null)],
            [], false, null, false);
        var candidate = new MediaCandidate(source, new("https://school.example/lesson"), "HLS", HlsManifest: info);
        var plan = MediaDownloadPlanResolver.Resolve(candidate, quality, false);
        Assert.False(plan.IsResolved);
        Assert.False(string.IsNullOrWhiteSpace(plan.Error));
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string> { source.AbsoluteUri, "https://cdn.example/720.m3u8" }));
    }

    [Theory]
    [InlineData("best", true)]
    [InlineData("360p", false)]
    [InlineData("1080p", false)]
    public async Task Unresolved_selection_never_calls_verification_or_downloader(string quality, bool empty)
    {
        var candidate = new MediaCandidate(new("https://cdn.example/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true,
                empty ? [] : [new HlsVariant(new("https://cdn.example/720.m3u8"), 1280, 720, 1, 30, null)],
                [], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, quality, false);
        var verificationCalls = 0;
        var downloaderCalls = 0;
        string? rejection = null;
        var outcome = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => { verificationCalls++; return Task.FromResult(true); },
            () => { downloaderCalls++; return Task.FromResult("downloaded"); },
            error => { rejection = error; return "rejected"; });
        Assert.Equal("rejected", outcome);
        Assert.Equal(0, verificationCalls);
        Assert.Equal(0, downloaderCalls);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Fact]
    public async Task Explicit_unresolved_plan_is_rejected_even_for_non_hls()
    {
        var candidate = new MediaCandidate(new("https://cdn.example/video.mp4"), new("https://school.example/lesson"), "MP4");
        var plan = new MediaDownloadPlan(candidate.Source, null, null, false, false, "Недоступно");
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>()));
        var result = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => throw new InvalidOperationException("verification must not run"),
            () => Task.FromResult("downloaded"), error => error ?? "rejected");
        Assert.Equal("Недоступно", result);
    }

    [Fact]
    public async Task Ordinary_direct_media_does_not_need_hls_verification()
    {
        var candidate = new MediaCandidate(new("https://cdn.example/video.mp4"), new("https://school.example/lesson"), "MP4");
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        var outcome = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => Task.FromResult(false), () => Task.FromResult("downloaded"), _ => "rejected");
        Assert.Equal("downloaded", outcome);
    }

    [Fact]
    public void Single_leaf_plan_records_resolution_and_requires_that_leaf()
    {
        var candidate = new MediaCandidate(new("https://cdn.example/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true, [new HlsVariant(new("https://cdn.example/720.m3u8"), 1280, 720, 1, 30, null)], [], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "720p", false);
        Assert.True(plan.IsResolved);
        Assert.True(plan.ResolvedHlsLeaf);
        Assert.Equal("https://cdn.example/720.m3u8", plan.Source.AbsoluteUri);
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>()));
        Assert.True(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string> { "https://cdn.example/720.m3u8" }));
    }

    [Fact]
    public void Master_without_any_selected_leaf_is_not_verified()
    {
        var candidate = new MediaCandidate(new("https://cdn.example/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true, [], [], false, null, false));
        var plan = new MediaDownloadPlan(candidate.Source, null, null, true);
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string> { candidate.Source.AbsoluteUri }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gate_preserves_cancellation_from_verification_and_download(bool cancelDuringDownload)
    {
        var candidate = new MediaCandidate(new("https://cdn.example/media.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(false, [], [], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        var rejectionCalls = 0;
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => cancelDuringDownload ? Task.FromResult(true) : Task.FromCanceled<bool>(cancellation.Token),
            () => { calls++; return Task.FromCanceled<bool>(cancellation.Token); },
            _ => { rejectionCalls++; return false; }));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(cancelDuringDownload ? 1 : 0, calls);
        Assert.Equal(0, rejectionCalls);
    }

    [Fact]
    public async Task Parsed_direct_clear_hls_downloads_without_master_leaf_cache()
    {
        var source = new Uri("https://cdn.example/media.m3u8");
        Assert.True(HlsManifestParser.TryParse("#EXTM3U\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n", source, out var info));
        var candidate = new MediaCandidate(source, new("https://school.example/lesson"), "HLS", HlsManifest: info);
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "best", false);
        var result = await HlsDownloadPolicy.RunVerifiedAsync(candidate, plan,
            () => Task.FromResult(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan, new HashSet<string>())),
            () => Task.FromResult("downloaded"), _ => "rejected");
        Assert.Equal("downloaded", result);
    }

    [Fact]
    public void Audio_only_requires_the_selected_audio_leaf()
    {
        var candidate = new MediaCandidate(new("https://cdn.example/master.m3u8"), new("https://school.example/lesson"), "HLS",
            HlsManifest: new(true, [new HlsVariant(new("https://cdn.example/720.m3u8"), 1280, 720, 1, 30, "aud")],
                [new HlsAudioRendition(new("https://cdn.example/audio.m3u8"), "ru", "Russian", "aud", true)], false, null, false));
        var plan = MediaDownloadPlanResolver.Resolve(candidate, "720p", true);
        Assert.True(plan.ResolvedHlsLeaf);
        Assert.False(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string> { "https://cdn.example/720.m3u8" }));
        Assert.True(HlsDownloadPolicy.AreSelectedTracksVerified(candidate, plan,
            new HashSet<string> { "https://cdn.example/audio.m3u8" }));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("chrome", false)]
    [InlineData("embedded", true)]
    public void Embedded_session_is_used_only_after_explicit_selection(string? selection, bool expected)
        => Assert.Equal(expected, BrowserDownloadSessionPolicy.UseEmbeddedSession(selection));

    [Fact]
    public void Hls_download_plan_uses_current_quality_and_audio_choice()
    {
        var info = new HlsManifestInfo(true,
        [
            new HlsVariant(new Uri("https://cdn.example/360.m3u8"), 640, 360, 800000, 30, "aud"),
            new HlsVariant(new Uri("https://cdn.example/720.m3u8"), 1280, 720, 1500000, 30, "aud"),
            new HlsVariant(new Uri("https://cdn.example/1080.m3u8"), 1920, 1080, 3000000, 30, "aud")
        ],
        [new HlsAudioRendition(new Uri("https://cdn.example/ru.m3u8"), "ru", "Russian", "aud", true)],
        false, null, false);
        var candidate = new MediaCandidate(new Uri("https://cdn.example/master.m3u8"), new Uri("https://school.example/lesson"), "HLS", "master", HlsManifest: info);

        var video = MediaDownloadPlanResolver.Resolve(candidate, "720p", audioOnly: false);
        Assert.Equal("https://cdn.example/720.m3u8", video.HlsVideoSource!.AbsoluteUri);
        Assert.Equal("https://cdn.example/ru.m3u8", video.HlsAudioSource!.AbsoluteUri);

        var audio = MediaDownloadPlanResolver.Resolve(candidate, "1080p", audioOnly: true);
        Assert.Equal("https://cdn.example/ru.m3u8", audio.Source.AbsoluteUri);
        Assert.Null(audio.HlsVideoSource);
        Assert.Null(audio.HlsAudioSource);
    }
}
