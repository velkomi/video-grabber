using System.Net;
using System.Net.Sockets;
using VideoGrabber.Infrastructure.Networking;
namespace VideoGrabber.Infrastructure.Tests;
public sealed class SiteProxyDisposalTests
{
    [Fact]
    public async Task Dispose_closes_established_upstream_before_returning()
    {
        var upstream = new ObservedStream();
        using var proxy = new SiteRouteProxy((_, _, _) => Task.FromResult<Stream>(upstream));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 5, 1, 0 });
        var greeting = new byte[2]; await stream.ReadExactlyAsync(greeting);
        await stream.WriteAsync(new byte[] { 5, 1, 0, 3, 11, 101, 120, 97, 109, 112, 108, 101, 46, 99, 111, 109, 1, 187 });
        var reply = new byte[10]; await stream.ReadExactlyAsync(reply);
        await upstream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { proxy.Dispose(); Assert.True(upstream.Closed, "Upstream must be closed synchronously, not by a future continuation."); }
        finally { upstream.Finish.TrySetResult(0); }
    }
    private sealed class ObservedStream : Stream
    {
        public bool Closed;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Started.TrySetResult(); return new(Finish.Task); }
        protected override void Dispose(bool disposing) { Closed = true; Finish.TrySetResult(0); base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
