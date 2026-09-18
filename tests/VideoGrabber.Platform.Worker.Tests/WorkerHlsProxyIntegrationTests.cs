using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Worker;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class WorkerHlsProxyIntegrationTests
{
    [Fact]
    public async Task Six_segment_HLS_download_uses_proxy_and_full_decode_verifies_output()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var runner = new BoundedProcessRunner();
            var tools = new WorkerToolLocator(
                RequireTool("VG_WORKER_YTDLP", OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp"),
                RequireTool("VG_WORKER_FFMPEG", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"),
                RequireTool("VG_WORKER_FFPROBE", OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe"));
            var fixture = Path.Combine(root, "fixture");
            Directory.CreateDirectory(fixture);
            var playlist = Path.Combine(fixture, "master.m3u8");
            var generated = await runner.RunAsync(new WorkerProcessSpec(
                tools.Ffmpeg,
                ["-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=25",
                 "-t","6","-c:v","libx264","-threads","1","-filter_threads","1","-pix_fmt","yuv420p",
                 "-g","25","-keyint_min","25","-sc_threshold","0",
                 "-f","hls","-hls_time","1","-hls_playlist_type","vod",
                 "-hls_segment_filename",Path.Combine(fixture,"segment%02d.ts"),
                 "-y",playlist],
                root, TimeSpan.FromMinutes(2)), CancellationToken.None);
            Assert.True(generated.Success, generated.StandardError);
            Assert.Equal(6, Directory.GetFiles(fixture, "segment*.ts").Length);

            await using var proxy = new StaticProxy(fixture);
            var work = new CreateJob(
                Guid.NewGuid(), new string('a',64), "download", "server_worker",
                null, "src_hls_fixture", "180p", [], null, null);
            var lease = new AttemptLease(
                Guid.NewGuid(), Guid.NewGuid(), 1,
                DateTimeOffset.UtcNow.AddMinutes(1), "capability", work);
            var executor = new MediaJobExecutor(
                runner, tools, new ArtifactVerifier(runner, tools),
                new StaticSourceResolver(new Uri("http://media.example.test/master.m3u8")),
                Path.Combine(root, "jobs"), proxy.Uri);

            var receipt = await executor.ExecuteAsync(lease, CancellationToken.None);

            Assert.Equal(64, receipt.Sha256.Length);
            Assert.True(receipt.Bytes > 0);
            Assert.Contains(proxy.Paths, path => path.EndsWith("/master.m3u8", StringComparison.Ordinal));
            for (var i = 0; i < 6; i++)
                Assert.Contains(proxy.Paths,
                    path => path.EndsWith($"/segment{i:00}.ts", StringComparison.Ordinal));
        }
        finally { SafeDelete(root); }
    }

    private sealed class StaticSourceResolver(Uri source) : IWorkerSourceResolver
    {
        public Task<WorkerResolvedSource> ResolveAsync(
            string sourceId, string quality, CancellationToken cancellationToken)
            => Task.FromResult(new WorkerResolvedSource(
                source, "best", 320, 180, "video/mp4"));
    }

    private sealed class StaticProxy : IAsyncDisposable
    {
        private readonly string _root;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        public ConcurrentBag<string> Paths { get; } = [];
        public Uri Uri { get; }

        public StaticProxy(string root)
        {
            _root = Path.GetFullPath(root);
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Uri = new Uri($"http://127.0.0.1:{port}/");
            _loop = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
                catch (OperationCanceledException) { break; }
                _ = HandleAsync(client);
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 8192, leaveOpen: true))
            {
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine)) return;
                string? line;
                do { line = await reader.ReadLineAsync(); } while (!string.IsNullOrEmpty(line));
                var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0] != "GET") { await ReplyAsync(stream, 400, [], "text/plain"); return; }
                var target = parts[1];
                var uri = Uri.TryCreate(target, UriKind.Absolute, out var absolute)
                    ? absolute
                    : new Uri("http://media.example.test" + target);
                Paths.Add(uri.AbsolutePath);
                var name = Path.GetFileName(uri.AbsolutePath);
                var file = Path.GetFullPath(Path.Combine(_root, name));
                if (!file.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(file))
                {
                    await ReplyAsync(stream, 404, [], "text/plain");
                    return;
                }
                var bytes = await File.ReadAllBytesAsync(file);
                var type = name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                    ? "application/vnd.apple.mpegurl" : "video/mp2t";
                await ReplyAsync(stream, 200, bytes, type);
            }
        }

        private static async Task ReplyAsync(
            NetworkStream stream, int status, byte[] body, string contentType)
        {
            var reason = status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request";
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header);
            if (body.Length > 0) await stream.WriteAsync(body);
            await stream.FlushAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            try { await _loop; } catch (OperationCanceledException) { }
            _stop.Dispose();
        }
    }

    private static string RequireTool(string variable, string fileName)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception) { }
        }
        throw new Xunit.Sdk.XunitException(
            $"Required integration tool {fileName} is missing; set {variable}. This test must not skip.");
    }

    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "vg-worker-hls-" + Guid.NewGuid().ToString("N"));

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
