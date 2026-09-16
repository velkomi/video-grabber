using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserSessionTests
{
    [Fact]
    public void Nested_programmatic_cleanup_only_suppresses_selector_events_not_explicit_invalidation()
    {
        var session = new BrowserSessionLifetime();
        session.RunProgrammaticSelectionCleanup(() =>
        {
            Assert.False(session.OnSelectionChanged());
            session.RunProgrammaticSelectionCleanup(() => Assert.False(session.OnSelectionChanged()));
            Assert.False(session.OnSelectionChanged());
            Assert.Equal(0, session.Epoch);
            session.Invalidate();
            Assert.Equal(1, session.Epoch);
            Assert.False(session.OnSelectionChanged());
        });
        Assert.True(session.OnSelectionChanged());
        Assert.Equal(2, session.Epoch);
        Assert.Throws<IOException>(() => { session.RunProgrammaticSelectionCleanup(() => throw new IOException("cleanup failed")); });
        Assert.True(session.OnSelectionChanged());
        Assert.Equal(3, session.Epoch);
    }

    [Fact]
    public void App_navigation_and_session_handlers_use_production_queue_lifetime_and_saved_preparation_context()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src", "VideoGrabber.App"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string file) => File.ReadAllText(Path.Combine(root.FullName, "src", "VideoGrabber.App", file));
        var discovery = Read("MainWindow.DevTools.cs");
        var browser = Read("MainWindow.Browser.cs");
        var batch = Read("MainWindow.BatchDownload.cs");
        var preparation = Read("BrowserDownloadPreparation.cs");
        Assert.DoesNotContain("_browserDownloadQueue.Clear()", discovery);
        Assert.Contains("OnNavigation(_browserSessionEpoch)", discovery);
        Assert.Contains("OnSessionChanged(_browserSessionEpoch)", browser);
        Assert.Contains("_cookiesBox.SelectionChanged", browser);
        Assert.Contains("_browserSession.OnSelectionChanged()", browser);
        Assert.Contains("_browserSession.Invalidate()", browser);
        Assert.Contains("RunProgrammaticSelectionCleanup", Read("MainWindow.Download.cs"));
        Assert.Contains("CookieSelection = entry.Context.CookieSelection", batch);
        Assert.Contains("entry.Context?.Metadata", batch);
        Assert.Contains("service.RunQueuedAsync", Read("MainWindow.Download.cs"));
        Assert.Contains("queueContext is null", preparation);
        Assert.Contains("queueContext?.Metadata ?? window._browserMetadata", preparation);
        Assert.Contains("queueContext?.Page ?? _browserPageUri", preparation);
    }

    [Theory]
    [InlineData("https://cdn.example.test/video.mp4?sig=private", "", "MP4")]
    [InlineData("https://cdn.example.test/stream?id=1", "application/vnd.apple.mpegurl", "HLS")]
    [InlineData("https://cdn.example.test/master.mpd", "", "DASH")]
    [InlineData("https://player02.getcourse.ru/sign-player/?json=example", "text/html", "GetCourse")]
    public void Finds_media_by_url_or_mime(string url, string mime, string kind)
    {
        Assert.True(MediaCandidate.TryCreate(url, mime, new Uri("https://school.example.test/lesson"), out var candidate));
        Assert.Equal(kind, candidate!.Kind);
        Assert.DoesNotContain("private", candidate.DisplayName);
    }

    [Theory]
    [InlineData("blob:https://example.test/id")]
    [InlineData("file:///C:/secret.mp4")]
    [InlineData("https://127.0.0.1/file.mp4")]
    [InlineData("https://example.test/image.jpg")]
    public void Rejects_non_media_or_unsafe_candidates(string url)
    {
        Assert.False(MediaCandidate.TryCreate(url, "image/jpeg", new Uri("https://school.example.test"), out _));
    }

    [Fact]
    public void Scoped_cookies_match_host_path_secure_and_are_deleted()
    {
        var uri = new Uri("https://school.example.test/lesson/view");
        BrowserCookie[] cookies = [
            new("school.example.test", "/lesson", "allowed", "yes", true, true),
            new(".example.test", "/", "parent", "yes", true, false),
            new("other.example.test", "/", "unrelated", "no", false, false),
            new("school.example.test", "/lessons", "wrongPath", "no", false, false),
            new("school.example.test", "/", "inject", "bad\nline", false, false)];
        string path;
        using (var file = ScopedCookieFile.Create([uri], cookies))
        {
            path = file.Path;
            var text = File.ReadAllText(path);
            Assert.Contains("allowed", text);
            Assert.Contains("parent", text);
            Assert.DoesNotContain("unrelated", text);
            Assert.DoesNotContain("wrongPath", text);
            Assert.DoesNotContain("bad", text);
            Assert.Contains("#HttpOnly_", text);
        }
        Assert.False(File.Exists(path));
        Assert.False(ScopedCookieFile.Matches(cookies[0], new Uri("http://school.example.test/lesson/view")));
        Assert.False(ScopedCookieFile.Matches(cookies[0], new Uri("https://evil.school.example.test/lesson/view")));
    }
    [Fact]
    public void Scoped_cookie_file_can_include_lesson_player_and_cdn_domains()
    {
        var sources = new[]
        {
            new Uri("https://iglyrazuma.ru/pl/teach/control/lesson/view?id=1"),
            new Uri("https://api2.gcvh.ru/sign-player/?json=x"),
            new Uri("https://gc77.vhcdn.com/master.m3u8?token=x")
        };
        BrowserCookie[] cookies =
        [
            new("iglyrazuma.ru", "/", "PHPSESSID5", "lesson", true, true),
            new("api2.gcvh.ru", "/", "player", "yes", true, false),
            new("gc77.vhcdn.com", "/", "cdn", "yes", true, false),
            new("unrelated.example", "/", "nope", "no", true, false)
        ];
        using var file = ScopedCookieFile.Create(sources, cookies);
        var text = File.ReadAllText(file.Path);
        Assert.Contains("PHPSESSID5", text);
        Assert.Contains("player", text);
        Assert.Contains("cdn", text);
        Assert.DoesNotContain("nope", text);
    }

    [Fact]
    public void Candidate_display_can_include_safe_hls_details_without_url_query()
    {
        var candidate = new MediaCandidate(
            new Uri("https://cdn.example/master.m3u8?token=secret"),
            new Uri("https://school.example/lesson"),
            "HLS",
            "master [360p, 720p; audio: ru]");
        Assert.Equal("HLS master [360p, 720p; audio: ru] — cdn.example", candidate.DisplayName);
        Assert.DoesNotContain("secret", candidate.DisplayName);
    }

}
