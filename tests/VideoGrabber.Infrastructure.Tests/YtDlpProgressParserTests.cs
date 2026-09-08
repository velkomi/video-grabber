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

    [Theory]
    [InlineData("videograbber:1048576|4194304|NA|NA|NA|512.00KiB/s|00:06", 25.0)]
    [InlineData("videograbber:524288|NA|1048576|NA|NA|256.00KiB/s|00:02", 50.0)]
    public void TryParse_calculates_percent_from_downloaded_bytes(
        string line,
        double expectedPercent)
    {
        var parsed = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(parsed);
        Assert.Equal(expectedPercent, progress.Percent);
        Assert.Equal("Загрузка", progress.Status);
    }

    [Fact]
    public void TryParse_calculates_hls_percent_from_fragment_numbers()
    {
        var parsed = YtDlpProgressParser.TryParse(
            "videograbber:24097088|NA|NA|668|688|1.25MiB/s|00:15",
            out var progress);

        Assert.True(parsed);
        Assert.Equal(97.1, progress.Percent!.Value, precision: 1);
        Assert.Equal("1.25MiB/s", progress.Speed);
        Assert.Equal("00:15", progress.Eta);
    }

    [Fact]
    public void TryParse_rejects_non_progress_stage_lines()
    {
        Assert.False(YtDlpProgressParser.TryParse("[youtube] Downloading webpage", out _));
    }
}
