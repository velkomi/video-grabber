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

public sealed class AuthenticatedDownloadIntegrationTests
{
    [MediaToolsFact]
    public async Task Download_requires_scoped_session_and_referer_then_verifies_the_actual_file()
    {
        var evidence = Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE") ?? Path.GetTempPath();
        var root = Path.Combine(evidence, "auth-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tools = new ToolLocator(Environment.GetEnvironmentVariable("VIDEOGRABBER_INTEGRATION_TOOLS"));
        var runner = new ProcessRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var source = Path.Combine(root, "source.mp4");
        var generated = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10",
             "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "2", "-c:v", "mpeg4", "-c:a", "aac", source]),
             null, deadline.Token);
        Assert.True(generated.IsSuccess, generated.StandardError);
        await using var server = new AuthFixture(await File.ReadAllBytesAsync(source, deadline.Token));
        using var egress = ManagedEgressFixture.RegisterListener("authenticated-download", server.Media);
        using var client = new HttpClient();
        using var anonymous = await client.GetAsync(server.Media, deadline.Token);
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        string cookiePath;
        var progress = new ProgressRecorder();
        DownloadResult result;
        using (var cookies = ScopedCookieFile.Create([server.Media],
            [new BrowserCookie("127.0.0.1", "/", "vg_session", "fixture-only", false, true)]))
        {
            cookiePath = cookies.Path;
            result = await new YtDlpDownloader(runner, tools, egressRegistry: egress.Registry).DownloadAsync(
                egress.Apply(new DownloadRequest(server.Media, Path.Combine(root, "output"), "best",
                    CookiesFile: cookiePath, Referer: server.Lesson)), progress, deadline.Token);
        }
        Assert.False(File.Exists(cookiePath));
        Assert.True(result.Success, result.Message);
        Assert.True(server.AuthorizedRequests > 0);
        Assert.True(progress.PercentUpdates > 0);
        Assert.True(File.Exists(result.OutputPath));
        var decode = await runner.RunAsync(new ProcessSpec(tools.Ffmpeg, ["-v", "error", "-i", result.OutputPath!, "-f", "null", "-"]), null, deadline.Token);
        Assert.True(decode.IsSuccess, decode.StandardError);
        await File.WriteAllTextAsync(Path.Combine(root, "RESULT.txt"),
            $"PASS\nanonymous=403\nauthorizedRequests={server.AuthorizedRequests}\nprogressUpdates={progress.PercentUpdates}\ncookieFileDeleted=True\nfullDecode=True\noutput={result.OutputPath}\n", deadline.Token);
    }

    private sealed class ProgressRecorder : IProgress<DownloadProgress>
    {
        public int PercentUpdates;
        public void Report(DownloadProgress value) { if (value.Percent.HasValue) Interlocked.Increment(ref PercentUpdates); }
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _task;
        public Uri Media { get; }
        public Uri Lesson { get; }
        public int AuthorizedRequests;
        public AuthFixture(byte[] media)
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Media = new Uri($"http://127.0.0.1:{port}/protected.mp4");
            Lesson = new Uri($"http://127.0.0.1:{port}/lesson");
            _task = ServeAsync(media);
        }
        private async Task ServeAsync(byte[] media)
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(_stop.Token);
                    await using var stream = connection.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                    var request = await reader.ReadLineAsync(_stop.Token) ?? "";
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < 100; i++)
                    {
                        var line = await reader.ReadLineAsync(_stop.Token);
                        if (string.IsNullOrEmpty(line)) break;
                        if (line.Length > 8192) throw new InvalidDataException("Oversized fixture header");
                        var colon = line.IndexOf(':');
                        if (colon > 0) headers[line[..colon]] = line[(colon + 1)..].Trim();
                    }
                    var allowed = request.Contains(" /protected.mp4 ", StringComparison.Ordinal)
                        && headers.TryGetValue("Cookie", out var cookie) && cookie.Contains("vg_session=fixture-only", StringComparison.Ordinal)
                        && headers.TryGetValue("Referer", out var referer) && referer == Lesson.AbsoluteUri;
                    if (allowed) Interlocked.Increment(ref AuthorizedRequests);
                    var body = allowed ? media : "Forbidden"u8.ToArray();
                    var status = allowed ? "200 OK" : "403 Forbidden";
                    var type = allowed ? "video/mp4" : "text/plain";
                    var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head, _stop.Token);
                    if (!request.StartsWith("HEAD ", StringComparison.Ordinal)) await stream.WriteAsync(body, _stop.Token);
                }
            }
            catch (Exception ex) when (_stop.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
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
