using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class HlsManifestParserTests
{
    [Fact]
    public void Parses_master_variants_audio_and_relative_urls()
    {
        const string manifest = """
        #EXTM3U
        #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="Russian",LANGUAGE="ru",DEFAULT=YES,AUTOSELECT=YES,URI="audio/ru.m3u8"
        #EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,FRAME-RATE=30,AUDIO="audio"
        low/video.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=2400000,RESOLUTION=1280x720,FRAME-RATE=60,AUDIO="audio"
        high/video.m3u8
        """;

        Assert.True(HlsManifestParser.TryParse(manifest, new Uri("https://cdn.example/master.m3u8?sig=secret"), out var info));
        Assert.True(info!.IsMaster);
        Assert.Equal(2, info.Variants.Count);
        Assert.Equal(360, info.Variants[0].Height);
        Assert.Equal(720, info.Variants[1].Height);
        Assert.Equal("https://cdn.example/high/video.m3u8", info.Variants[1].Uri.AbsoluteUri);
        Assert.Single(info.AudioRenditions);
        Assert.Equal("ru", info.AudioRenditions[0].Language);
        Assert.Equal("https://cdn.example/audio/ru.m3u8", info.AudioRenditions[0].Uri.AbsoluteUri);
        Assert.Contains("360p", info.SafeSummary);
        Assert.Contains("720p", info.SafeSummary);
        Assert.Contains("audio: ru", info.SafeSummary);
        Assert.DoesNotContain("secret", info.SafeSummary);
    }

    [Fact]
    public void Parses_media_playlist_and_aes128_without_exposing_key_url()
    {
        const string manifest = """
        #EXTM3U
        #EXT-X-TARGETDURATION:6
        #EXT-X-KEY:METHOD=AES-128,URI="https://keys.example/key.bin?token=secret"
        #EXTINF:6.0,
        seg-1.ts
        #EXT-X-ENDLIST
        """;
        Assert.True(HlsManifestParser.TryParse(manifest, new Uri("https://cdn.example/level.m3u8"), out var info));
        Assert.False(info!.IsMaster);
        Assert.True(info.IsEncrypted);
        Assert.False(info.UsesDrmLikeEncryption);
        Assert.Equal("AES-128", info.EncryptionMethod);
        Assert.DoesNotContain("keys.example", info.SafeSummary);
        Assert.DoesNotContain("secret", info.SafeSummary);
    }

    [Fact]
    public void Flags_sample_aes_as_drm_like()
    {
        const string manifest = "#EXTM3U\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"skd://license\"\n#EXTINF:5,\nseg.ts\n";
        Assert.True(HlsManifestParser.TryParse(manifest, new Uri("https://cdn.example/level.m3u8"), out var info));
        Assert.True(info!.UsesDrmLikeEncryption);
        Assert.Contains("SAMPLE-AES", info.SafeSummary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a playlist")]
    [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=1\nvideo.m3u8")]
    public void Rejects_non_hls(string text)
        => Assert.False(HlsManifestParser.TryParse(text, new Uri("https://cdn.example/a.m3u8"), out _));
}
