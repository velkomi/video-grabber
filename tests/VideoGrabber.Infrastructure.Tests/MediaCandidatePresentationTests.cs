using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class MediaCandidatePresentationTests
{
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
