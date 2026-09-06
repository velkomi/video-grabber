using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class YtDlpProgressParserTests
{
    [Theory]
    [InlineData(" 28.8%| 582.55KiB/s|00:12", 28.8, "582.55KiB/s", "00:12")]
    [InlineData("100.0%|529.13KiB/s|NA", 100.0, "529.13KiB/s", "NA")]
    public void TryParse_accepts_current_yt_dlp_progress_lines(
        string line,
        double expectedPercent,
        string expectedSpeed,
        string expectedEta)
    {
        var parsed = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(parsed);
        Assert.Equal(expectedPercent, progress.Percent);
        Assert.Equal("Загрузка", progress.Status);
        Assert.Equal(expectedSpeed, progress.Speed);
        Assert.Equal(expectedEta, progress.Eta);
    }

    [Fact]
    public void TryParse_rejects_non_progress_stage_lines()
    {
        Assert.False(YtDlpProgressParser.TryParse("[youtube] Downloading webpage", out _));
    }
}
