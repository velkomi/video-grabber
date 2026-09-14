using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber.Infrastructure.Networking;
namespace VideoGrabber.Infrastructure.Tests;
public sealed class SiteProxyTests
{
    [Theory]
    [InlineData("127.0.0.1",443)][InlineData("169.254.169.254",80)]
    [InlineData("example.com",22)][InlineData("example.com",25)]
    public async Task Refuses_private_destinations_and_nonweb_ports(string host,int port)
    {
        var called=false;
        using var proxy=new SiteRouteProxy((_,_,_)=>{called=true;throw new InvalidOperationException();});
        using var client=await OpenAsync(proxy.Port);
        var stream=client.GetStream(); await stream.WriteAsync(Request(host,port));
        var reply=new byte[10]; await stream.ReadExactlyAsync(reply).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(0,reply[1]); Assert.False(called);
    }
    [Fact]
    public async Task Rejects_unsupported_authentication_without_starting_outbound_connection()
    {
        using var proxy=new SiteRouteProxy((_,_,_)=>throw new InvalidOperationException());
        using var client=new TcpClient(); await client.ConnectAsync(IPAddress.Loopback,proxy.Port);
        var stream=client.GetStream(); await stream.WriteAsync(new byte[]{5,1,2});
        var reply=new byte[2];await stream.ReadExactlyAsync(reply).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new byte[]{5,255},reply);
    }
    [Fact]
    public async Task Relays_bytes_without_decrypting_and_closes_tunnel_on_disposal()
    {
        using var upstream=new TcpListener(IPAddress.Loopback,0);upstream.Start();
        var accepting=upstream.AcceptTcpClientAsync();
        using var proxy=new SiteRouteProxy(async(host,port,ct)=>{
            Assert.Equal("example.com",host);Assert.Equal(443,port);
            var socket=new Socket(SocketType.Stream,ProtocolType.Tcp);
            try{await socket.ConnectAsync((IPEndPoint)upstream.LocalEndpoint,ct);return new NetworkStream(socket,true);}
            catch{socket.Dispose();throw;}
        });
        using var client=await OpenAsync(proxy.Port);var stream=client.GetStream();
        await stream.WriteAsync(Request("example.com",443));var reply=new byte[10];
        await stream.ReadExactlyAsync(reply).AsTask().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(0,reply[1]);
        using var server=await accepting.WaitAsync(TimeSpan.FromSeconds(5));var remote=server.GetStream();
        byte[] payload=[22,3,3,0,4,255,0,128,1];await stream.WriteAsync(payload);
        var received=new byte[payload.Length];await remote.ReadExactlyAsync(received).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(payload,received);await remote.WriteAsync(payload);
        await stream.ReadExactlyAsync(received).AsTask().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(payload,received);
        proxy.Dispose(); var one=new byte[1];
        try {Assert.Equal(0,await stream.ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));}
        catch(IOException){}
    }
    private static async Task<TcpClient> OpenAsync(int port)
    {
        var c=new TcpClient();await c.ConnectAsync(IPAddress.Loopback,port);
        await c.GetStream().WriteAsync(new byte[]{5,1,0});var b=new byte[2];
        await c.GetStream().ReadExactlyAsync(b).AsTask().WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(new byte[]{5,0},b);return c;
    }
    private static byte[] Request(string host,int port)
    {
        var bytes=Encoding.ASCII.GetBytes(host);
        return [5,1,0,3,(byte)bytes.Length,..bytes,(byte)(port>>8),(byte)port];
    }
}
