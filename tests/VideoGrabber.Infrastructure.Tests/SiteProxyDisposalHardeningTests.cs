using System.Net;
using System.Net.Sockets;
using VideoGrabber.Infrastructure.Networking;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class SiteProxyDisposalHardeningTests
{
    [Fact]
    public async Task Dispose_closes_upstream_even_when_handler_cannot_observe_cancellation()
    {
        var upstream = new BlockingReadStream();
        using var proxy = new SiteRouteProxy((_, _, _) => Task.FromResult<Stream>(upstream));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 5, 1, 0 });
        var greeting = new byte[2]; await stream.ReadExactlyAsync(greeting);
        await stream.WriteAsync(new byte[] { 5, 1, 0, 3, 11, 101, 120, 97, 109, 112, 108, 101, 46, 99, 111, 109, 1, 187 });
        var reply = new byte[10]; await stream.ReadExactlyAsync(reply);
        Assert.True(upstream.Started.Wait(TimeSpan.FromSeconds(5)));
        try { proxy.Dispose(); Assert.True(upstream.Closed); }
        finally { upstream.Release.Set(); }
    }

    private sealed class BlockingReadStream : Stream
    {
        public ManualResetEventSlim Started { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public volatile bool Closed;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Started.Set(); Release.Wait(); return ValueTask.FromResult(0); }
        protected override void Dispose(bool disposing) { Closed = true; Release.Set(); base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}

public sealed partial class SiteProxyDisposalSourceTests
{
    [Fact]
    public void Proxy_handler_treats_disposed_tunnels_as_expected_shutdown_state()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.Infrastructure")))
            current = current.Parent;
        Assert.NotNull(current);
        var source = File.ReadAllText(Path.Combine(current!.FullName, "src", "VideoGrabber.Infrastructure", "Networking", "SiteRouteProxy.cs"));
        Assert.Contains("ObjectDisposedException", source);
        Assert.Contains("_stopping.IsCancellationRequested", source);
    }
}