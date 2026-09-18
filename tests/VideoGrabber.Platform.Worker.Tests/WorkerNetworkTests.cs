using System.Net;
using VideoGrabber.Platform.Api.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class WorkerNetworkTests
{
    [Theory]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("http://169.254.1.1/private")]
    [InlineData("http://[::1]/private")]
    [InlineData("http://10.0.0.1/private")]
    [InlineData("http://172.16.0.1/private")]
    [InlineData("http://192.168.1.1/private")]
    [InlineData("http://100.64.0.1/private")]
    [InlineData("http://[::ffff:127.0.0.1]/private")]
    public async Task Server_source_policy_rejects_private_targets(string url)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new EgressProxy().ValidatePublicTargetAsync(new Uri(url), CancellationToken.None));
    }

    [Fact]
    public async Task Userinfo_is_rejected_before_DNS()
    {
        var dns = new RecordingDns(IPAddress.Parse("93.184.216.34"));
        var proxy = new EgressProxy(dns);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ValidatePublicTargetAsync(
                new Uri("https://user:secret@example.test/media"), CancellationToken.None));
        Assert.Equal(0, dns.Calls);
    }

    [Fact]
    public async Task DNS_answer_set_fails_closed_if_any_answer_is_private()
    {
        var proxy = new EgressProxy(new RecordingDns(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("10.0.0.8")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            proxy.ValidatePublicTargetAsync(
                new Uri("https://rebind.example.test/video"), CancellationToken.None));
    }

    [Fact]
    public async Task Public_target_returns_only_validated_addresses()
    {
        var publicIp = IPAddress.Parse("93.184.216.34");
        var result = await new EgressProxy(new RecordingDns(publicIp))
            .ValidatePublicTargetAsync(
                new Uri("https://example.test/video"), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal(publicIp, result[0]);
    }

    private sealed class RecordingDns(params IPAddress[] answers) : IDnsResolver
    {
        public int Calls { get; private set; }

        public Task<IPAddress[]> ResolveAsync(
            string host,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answers);
        }
    }
}
