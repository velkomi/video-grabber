using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ClientUpdateTransportTests
{
    [Fact]
    public async Task Metadata_requests_are_anonymous_and_https_only_on_redirects()
    {
        var calls = new List<HttpRequestMessage>();
        using var f = new Fixture();
        var envelope = f.Sign();
        var handler = new Handler(r =>
        {
            calls.Add(r);
            if (calls.Count == 1) return Redirect("https://mirror.example.com/manifest");
            return Response(ClientReleaseSigner.SerializeEnvelope(envelope));
        });
        using var client = new ClientReleaseClient(handler);
        var result = await client.FetchAsync(new Uri("https://updates.example.com/manifest"), default);
        Assert.Equal(envelope.Payload, result.Payload);
        Assert.Equal(2, calls.Count);
        Assert.All(calls, r => { Assert.Null(r.Headers.Authorization); Assert.False(r.Headers.Contains("Cookie")); });
        using var unsafeClient = new ClientReleaseClient(new Handler(_ => Redirect("http://mirror.example.com/manifest")));
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => unsafeClient.FetchAsync(new Uri("https://updates.example.com/manifest"), default));
    }

    [Fact]
    public async Task Oversized_and_truncated_metadata_fail_before_parsing()
    {
        using var client = new ClientReleaseClient(new Handler(_ => Response(new byte[ClientReleaseVerifier.MaxEnvelopeBytes + 1])));
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => client.FetchAsync(new Uri("https://updates.example.com/manifest"), default));
        using var malformed = new ClientReleaseClient(new Handler(_ => Response("{"u8.ToArray())));
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => malformed.FetchAsync(new Uri("https://updates.example.com/manifest"), default));
    }

    [Fact]
    public async Task Installer_is_exposed_only_after_signed_size_and_hash_match_and_cached_file_is_rechecked()
    {
        using var f = new Fixture();
        var calls = 0;
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier, new Handler(_ => { calls++; return Response(f.Bytes); }));
        var path = await downloader.DownloadAsync(f.Sign(), false, null, default);
        Assert.Equal(f.Bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(1, calls);
        _ = await downloader.DownloadAsync(f.Sign(), false, null, default);
        Assert.Equal(1, calls);
        await File.WriteAllBytesAsync(path, new byte[f.Bytes.Length]);
        _ = await downloader.DownloadAsync(f.Sign(), false, null, default);
        Assert.Equal(2, calls);
        Assert.Equal(f.Bytes, await File.ReadAllBytesAsync(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Invalid_download_never_becomes_a_ready_installer(int kind)
    {
        using var f = new Fixture();
        var bytes = kind == 1 ? f.Bytes.Concat(new byte[1]).ToArray() : kind == -1 ? f.Bytes[..^1] : new byte[f.Bytes.Length];
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier, new Handler(_ => Response(bytes)));
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => downloader.DownloadAsync(f.Sign(), false, null, default));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.exe"));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.part"));
    }

    [Fact]
    public async Task Cancellation_does_not_expose_a_partial_installer()
    {
        using var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier, new Handler(_ => Response(f.Bytes)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(f.Sign(), false, null, cancellation.Token));
        Assert.False(System.IO.Directory.Exists(f.Directory) && System.IO.Directory.GetFiles(f.Directory, "*.exe").Length > 0);
    }

    [Fact]
    public async Task Tampered_manifest_does_not_start_a_download()
    {
        using var f = new Fixture(); var calls = 0;
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier, new Handler(_ => { calls++; return Response(f.Bytes); }));
        var tampered = f.Sign() with { Signature = Convert.ToBase64String(new byte[256]) };
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => downloader.DownloadAsync(tampered, false, null, default));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Stalled_response_bodies_are_bounded_and_do_not_leave_an_installer()
    {
        using var f = new Fixture();
        var timeout = TimeSpan.FromMilliseconds(80);
        using var outer = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var client = new ClientReleaseClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) }), timeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchAsync(new Uri("https://updates.example.com/manifest"), outer.Token));
        Assert.False(outer.IsCancellationRequested, "Metadata must stop by its internal body deadline, without caller cancellation");
        using var outerDownload = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier, new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) }), timeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(f.Sign(), false, null, outerDownload.Token));
        Assert.False(outerDownload.IsCancellationRequested, "Installer must stop by its internal idle deadline, without caller cancellation");
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.exe"));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.part"));
    }

    [Fact]
    public async Task Mid_stream_failure_does_not_expose_a_partial_installer()
    {
        using var f = new Fixture();
        using var downloader = new VerifiedInstallerDownloader(f.Directory, f.Verifier,
            new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream()) }));
        await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(f.Sign(), false, null, default));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.exe"));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory, "*.part"));
    }

    [Fact]
    public async Task Redirect_loops_are_bounded()
    {
        var calls = 0;
        using var client = new ClientReleaseClient(new Handler(_ => { calls++; return Redirect("https://updates.example.com/next"); }));
        await Assert.ThrowsAsync<ClientReleaseRejectedException>(() => client.FetchAsync(new Uri("https://updates.example.com/manifest"), default));
        Assert.Equal(4, calls);
    }

    private class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class InterruptedStream : StalledStream
    {
        private bool _started;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_started) throw new IOException("Synthetic transfer interruption");
            _started = true; buffer.Span[0] = (byte)'M'; buffer.Span[1] = (byte)'Z';
            return ValueTask.FromResult(2);
        }
    }

    private static HttpResponseMessage Redirect(string url)
    { var r = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect); r.Headers.Location = new Uri(url); return r; }
    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(handle(request)); } }
    private sealed class Fixture : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        public byte[] Bytes { get; } = new byte[1024];
        public string Directory { get; } = Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")!, "update-downloads", Guid.NewGuid().ToString("N"));
        public ClientReleaseVerifier Verifier { get; }
        public Fixture() { Bytes[0] = (byte)'M'; Bytes[1] = (byte)'Z'; Verifier = new(new Dictionary<string, string> { ["test"] = _key.ExportSubjectPublicKeyInfoPem() }); }
        public SignedClientRelease Sign()
        {
            var now = DateTimeOffset.UtcNow;
            var artifact = new ClientUpdateArtifact("1.0.0", new Uri("https://updates.example.com/setup"), Bytes.Length, Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant());
            return ClientReleaseSigner.Sign(new(1, "videograbber", "preview", 1, now, now.AddDays(1), "videograbber-main", new(new Uri("https://api.example.com/"), new Uri("https://site.example.com/web/")), new("1.0.0", now, "Обновление", artifact, null)), _key, "test");
        }
        public void Dispose() => _key.Dispose();
    }
}
