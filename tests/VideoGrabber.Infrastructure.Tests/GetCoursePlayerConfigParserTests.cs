using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class GetCoursePlayerConfigParserTests
{
    [Fact]
    public void Extracts_master_playlist_from_window_configs()
    {
        var player = new Uri("https://api3.gcvh.ru/sign-player/?json=secret");
        const string html = """
        <script>window.configs = {"gcFileId":77,"masterPlaylistUrl":"https://gc77.vhcdn.com/path/master.m3u8?token=abc","videoHash":"x"};</script>
        """;
        Assert.True(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(html, player, out var playlist));
        Assert.Equal("https://gc77.vhcdn.com/path/master.m3u8?token=abc", playlist!.AbsoluteUri);
    }

    [Fact]
    public void Resolves_relative_master_playlist_against_player()
    {
        var player = new Uri("https://api3.gcvh.ru/sign-player/?json=secret");
        const string html = "<script>window.configs={\"masterPlaylistUrl\":\"/hls/master.m3u8?x=1\"};</script>";
        Assert.True(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(html, player, out var playlist));
        Assert.Equal("https://api3.gcvh.ru/hls/master.m3u8?x=1", playlist!.AbsoluteUri);
    }
    [Theory]
    [InlineData("<html></html>")]
    [InlineData("<script>window.configs={broken};</script>")]
    [InlineData("<script>window.configs={\"masterPlaylistUrl\":\"javascript:alert(1)\"};</script>")]
    public void Rejects_missing_malformed_or_unsafe_config(string html)
        => Assert.False(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(
            html, new Uri("https://api3.gcvh.ru/sign-player/?json=x"), out _));

    [Fact]
    public void Extracts_master_playlist_from_nested_json()
    {
        var player = new Uri("https://api2.gcvh.ru/sign-player/?json=secret");
        const string body = """
        {"data":{"media":{"masterPlaylistUrl":"https://gc77.vhcdn.com/path/master.m3u8?token=abc"}}}
        """;
        Assert.True(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(body, player, out var playlist));
        Assert.Equal("gc77.vhcdn.com", playlist!.IdnHost);
    }

    [Fact]
    public void Extracts_escaped_playlist_url_from_javascript_body()
    {
        var player = new Uri("https://api2.gcvh.ru/sign-player/?json=secret");
        const string body = "<script>var cfg={masterPlaylistUrl:\"https:\\/\\/gc88.vhcdn.com\\/hls\\/master.m3u8?token=x\\u0026a=1\"};</script>";
        Assert.True(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(body, player, out var playlist));
        Assert.Equal("gc88.vhcdn.com", playlist!.IdnHost);
        Assert.Contains("a=1", playlist.Query);
    }
    [Fact]
    public void Extracts_extensionless_GetCourse_master_endpoint()
    {
        var player = new Uri("https://api2.gcvh.ru/sign-player/?json=secret");
        const string body = """
        <script>window.configs={"masterPlaylistUrl":"https:\/\/api1.gcvh.ru\/api\/playlist\/master\/abc\/def?jwt=secret"};</script>
        """;
        Assert.True(GetCoursePlayerConfigParser.TryExtractMasterPlaylist(body, player, out var playlist));
        Assert.Equal("api1.gcvh.ru", playlist!.IdnHost);
        Assert.Contains("/api/playlist/master/", playlist.AbsolutePath);
    }}
