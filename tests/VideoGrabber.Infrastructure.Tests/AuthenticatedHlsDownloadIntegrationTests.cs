using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Browser;
using VideoGrabber.Infrastructure.Components;
using VideoGrabber.Infrastructure.Downloads;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

public sealed partial class AuthenticatedHlsDownloadIntegrationTests
{
    [MediaToolsFact]
    public async Task Direct_HLS_uses_cookie_referer_and_produces_decodable_file()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var root = Path.Combine(evidence, "hls-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mediaRoot = Path.Combine(root, "media"); Directory.CreateDirectory(mediaRoot);
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        var runner = new ProcessRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var playlist = Path.Combine(mediaRoot, "master.m3u8");
        var generated = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10",
             "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "3",
             "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac",
             "-f", "hls", "-hls_time", "1", "-hls_list_size", "0",
             "-hls_segment_filename", Path.Combine(mediaRoot, "seg%03d.ts"), playlist]), null, deadline.Token);
        Assert.True(generated.IsSuccess, generated.StandardError);
        Assert.True(File.Exists(playlist));

        await using var server = new HlsFixture(mediaRoot);
        using var anonymous = new HttpClient();
        using var denied = await anonymous.GetAsync(server.Playlist, deadline.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        DownloadResult result;
        string cookiePath;
        using (var cookies = ScopedCookieFile.Create([server.Playlist, server.Lesson],
            [new BrowserCookie("127.0.0.1", "/", "vg_session", "fixture-only", false, true)]))
        {
            cookiePath = cookies.Path;
            result = await new YtDlpDownloader(runner, tools).DownloadAsync(
                new DownloadRequest(server.Playlist, Path.Combine(root, "output"), "best",
                    CookiesFile: cookiePath, Referer: server.Lesson, UserAgent: "VideoGrabber-HLS-Test"),
                null, deadline.Token);
        }
        Assert.False(File.Exists(cookiePath));
        Assert.True(result.Success, result.Message + "\n" + result.Details);
        Assert.True(server.AuthorizedPlaylistRequests > 0);
        Assert.True(server.AuthorizedSegmentRequests > 0);
        Assert.True(File.Exists(result.OutputPath));
        var decode = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-v", "error", "-i", result.OutputPath!, "-f", "null", "-"]), null, deadline.Token);
        Assert.True(decode.IsSuccess, decode.StandardError);
    }

    private sealed class HlsFixture : IAsyncDisposable
    {
        private readonly string _root;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _task;
        public Uri Playlist { get; }
        public Uri Lesson { get; }
        public int AuthorizedPlaylistRequests;
        public int AuthorizedSegmentRequests;

        public HlsFixture(string root, string playlistFile = "master.m3u8")
        {
            _root = root;
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Playlist = new Uri($"http://127.0.0.1:{port}/{playlistFile}");
            Lesson = new Uri($"http://127.0.0.1:{port}/lesson");
            _task = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    await HandleAsync(client, _stop.Token);
                }
            }
            catch (Exception ex) when (_stop.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
        }
        private async Task HandleAsync(TcpClient client, CancellationToken token)
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            var request = await reader.ReadLineAsync(token) ?? "";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < 100; i++)
            {
                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrEmpty(line)) break;
                var colon = line.IndexOf(':');
                if (colon > 0) headers[line[..colon]] = line[(colon + 1)..].Trim();
            }
            var parts = request.Split(' ');
            var path = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Split('?')[0]) : "/";
            var allowed = headers.TryGetValue("Cookie", out var cookie) && cookie.Contains("vg_session=fixture-only", StringComparison.Ordinal)
                && headers.TryGetValue("Referer", out var referer) && referer == Lesson.AbsoluteUri;
            if (!allowed) { await WriteAsync(stream, "403 Forbidden", "text/plain", "Forbidden"u8.ToArray(), request, token); return; }

            var local = Path.Combine(_root, Path.GetFileName(path));
            if (!File.Exists(local)) { await WriteAsync(stream, "404 Not Found", "text/plain", "Missing"u8.ToArray(), request, token); return; }
            var body = await File.ReadAllBytesAsync(local, token);
            var type = path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? "application/vnd.apple.mpegurl" : "video/mp2t";
            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref AuthorizedPlaylistRequests);
            else Interlocked.Increment(ref AuthorizedSegmentRequests);
            await WriteAsync(stream, "200 OK", type, body, request, token);
        }
        private static async Task WriteAsync(NetworkStream stream, string status, string contentType, byte[] body, string request, CancellationToken token)
        {
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, token);
            if (!request.StartsWith("HEAD ", StringComparison.Ordinal)) await stream.WriteAsync(body, token);
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            await _task;
            _stop.Dispose();
        }
    }
}

public sealed partial class AuthenticatedHlsDownloadIntegrationTests
{
    [MediaToolsFact]
    public async Task Split_track_HLS_merges_video_and_audio_into_one_decodable_file()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var root = Path.Combine(evidence, "hls-split-" + Guid.NewGuid().ToString("N"));
        var mediaRoot = Path.Combine(root, "media"); Directory.CreateDirectory(mediaRoot);
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        var runner = new ProcessRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var video = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10", "-t", "3",
             "-an", "-c:v", "libx264", "-preset", "ultrafast", "-f", "hls", "-hls_time", "1", "-hls_list_size", "0",
             "-hls_segment_filename", Path.Combine(mediaRoot, "video%03d.ts"), Path.Combine(mediaRoot, "video.m3u8")]), null, deadline.Token);
        Assert.True(video.IsSuccess, video.StandardError);
        var audio = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "3",
             "-vn", "-c:a", "aac", "-f", "hls", "-hls_time", "1", "-hls_list_size", "0",
             "-hls_segment_filename", Path.Combine(mediaRoot, "audio%03d.ts"), Path.Combine(mediaRoot, "audio.m3u8")]), null, deadline.Token);
        Assert.True(audio.IsSuccess, audio.StandardError);
        var master = Path.Combine(mediaRoot, "master-split.m3u8");
        await File.WriteAllTextAsync(master, """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="audio",NAME="Russian",LANGUAGE="ru",DEFAULT=YES,AUTOSELECT=YES,URI="audio.m3u8"
            #EXT-X-STREAM-INF:BANDWIDTH=500000,RESOLUTION=160x120,FRAME-RATE=10,AUDIO="audio"
            video.m3u8
            """, deadline.Token);

        await using var server = new HlsFixture(mediaRoot, "master-split.m3u8");
        var manifestText = await File.ReadAllTextAsync(master, deadline.Token);
        Assert.True(HlsManifestParser.TryParse(manifestText, new Uri("https://cdn.example/master-split.m3u8"), out var manifest));
        Assert.Single(manifest!.AudioRenditions);
        Assert.Equal("audio", Assert.Single(manifest.Variants).AudioGroupId);
        var plan = HlsTrackSelector.Select(manifest, "best");
        Assert.NotNull(plan?.Audio);
        var videoUri = new Uri(server.Playlist, "video.m3u8");
        var audioUri = new Uri(server.Playlist, "audio.m3u8");
        DownloadResult result;
        using (var cookies = ScopedCookieFile.Create([server.Playlist, server.Lesson, videoUri, audioUri],
            [new BrowserCookie("127.0.0.1", "/", "vg_session", "fixture-only", false, true)]))
        {
            result = await new YtDlpDownloader(runner, tools).DownloadAsync(
                new DownloadRequest(server.Playlist, Path.Combine(root, "output"), "best",
                    CookiesFile: cookies.Path, Referer: server.Lesson, UserAgent: "VideoGrabber-Split-HLS-Test",
                    HlsVideoSource: videoUri, HlsAudioSource: audioUri),
                null, deadline.Token);
        }
        Assert.True(result.Success, result.Message + "\n" + result.Details);
        Assert.NotNull(result.OutputPath);
        var probe = await new VideoGrabber.Infrastructure.Media.FfprobeMediaProbe(runner, tools)
            .ProbeAsync(result.OutputPath!, deadline.Token);
        Assert.True(probe.IsValid, probe.Error);
        Assert.True(probe.HasVideo, "Merged split-track output must contain video.");
        Assert.True(probe.HasAudio, "Merged split-track output must contain audio.");
        Assert.True(server.AuthorizedPlaylistRequests >= 3, "Master, video and audio playlists must all be authorized.");
        Assert.True(server.AuthorizedSegmentRequests > 0);
        var decode = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-v", "error", "-i", result.OutputPath!, "-f", "null", "-"]), null, deadline.Token);
        Assert.True(decode.IsSuccess, decode.StandardError);
    }
}

public sealed partial class AuthenticatedHlsDownloadIntegrationTests
{
    [MediaToolsFact]
    public async Task Split_track_HLS_audio_only_uses_audio_rendition_and_outputs_mp3()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var root = Path.Combine(evidence, "hls-audio-only-" + Guid.NewGuid().ToString("N"));
        var mediaRoot = Path.Combine(root, "media"); Directory.CreateDirectory(mediaRoot);
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        var runner = new ProcessRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var audio = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "2",
             "-vn", "-c:a", "aac", "-f", "hls", "-hls_time", "1", "-hls_list_size", "0",
             "-hls_segment_filename", Path.Combine(mediaRoot, "audio%03d.ts"), Path.Combine(mediaRoot, "audio.m3u8")]), null, deadline.Token);
        Assert.True(audio.IsSuccess, audio.StandardError);
        await using var server = new HlsFixture(mediaRoot, "audio.m3u8");
        var missingVideo = new Uri(server.Playlist, "missing-video.m3u8");
        using var cookies = ScopedCookieFile.Create([server.Playlist, server.Lesson],
            [new BrowserCookie("127.0.0.1", "/", "vg_session", "fixture-only", false, true)]);
        var result = await new YtDlpDownloader(runner, tools).DownloadAsync(
            new DownloadRequest(server.Playlist, Path.Combine(root, "output"), "best", AudioOnly: true,
                CookiesFile: cookies.Path, Referer: server.Lesson, HlsVideoSource: missingVideo, HlsAudioSource: server.Playlist),
            null, deadline.Token);
        Assert.True(result.Success, result.Message + "\n" + result.Details);
        var probe = await new VideoGrabber.Infrastructure.Media.FfprobeMediaProbe(runner, tools).ProbeAsync(result.OutputPath!, deadline.Token);
        Assert.True(probe.IsValid, probe.Error);
        Assert.True(probe.HasAudio);
        Assert.False(probe.HasVideo);
        Assert.Equal("mp3", probe.AudioCodec);
    }
}
