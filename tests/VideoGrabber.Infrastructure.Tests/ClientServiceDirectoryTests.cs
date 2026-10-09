using System.Net;
using System.Security.Cryptography;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ClientServiceDirectoryTests
{
    [Fact]
    public async Task Running_client_stops_auth_after_its_directory_expires_and_resumes_same_origin_renewal()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        var selection = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        var calls = 0;
        using var client = new HttpClient(new ClientDirectoryRequestGate(f.Cache, selection, f.Bootstrap, new Handler(() => calls++), f.Clock)) { BaseAddress = selection.Services.ApiBase };
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-only");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("v1/me")).StatusCode);
        f.Clock.Now = f.Clock.Now.AddDays(3);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("v1/me")).StatusCode);
        Assert.Equal(1, calls);
        f.Cache.AcceptChecked(f.Sign(2));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("v1/me")).StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Approved_migration_does_not_retarget_running_client_and_restart_uses_new_origin()
    {
        using var f = new Fixture(); f.Cache.AcceptChecked(f.Sign(1));
        var old = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        f.Cache.AcceptChecked(f.Sign(2, "https://new.example.com/")); f.Cache.ApproveLatestServices();
        using var client = new HttpClient(new ClientDirectoryRequestGate(f.Cache, old, f.Bootstrap, new Handler(() => { }), f.Clock)) { BaseAddress = old.Services.ApiBase };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("v1/me")).StatusCode);
        Assert.Equal("old.example.com", client.BaseAddress.Host);
        var restarted = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        Assert.Equal("new.example.com", restarted.Services.ApiBase.Host);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("https://new.example.com/v1/me")).StatusCode);
    }

    [Fact]
    public void Invalid_environment_override_cannot_bypass_blocked_directory()
    {
        using var f = new Fixture(); f.Cache.AcceptChecked(f.Sign(1)); f.Clock.Now = f.Clock.Now.AddDays(3);
        Assert.True(ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, "invalid").BlockOnline);
        Assert.True(ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, "http://outside.example.com/").BlockOnline);
        Assert.False(ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, "http://127.0.0.1:9999/").BlockOnline);
    }

    [Fact]
    public async Task Corruption_blocks_running_client_before_another_secret_bearing_request()
    {
        using var f = new Fixture(); f.Cache.AcceptChecked(f.Sign(1));
        var selection = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        using var client = new HttpClient(new ClientDirectoryRequestGate(f.Cache, selection, f.Bootstrap, new Handler(() => throw new InvalidOperationException("Must not send")), f.Clock)) { BaseAddress = selection.Services.ApiBase };
        File.WriteAllText(f.Path, "{broken}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("v1/me")).StatusCode);
    }

    [Fact]
    public async Task First_migration_catalog_does_not_interrupt_a_running_bootstrap_client()
    {
        using var f = new Fixture();
        var running = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        using var client = new HttpClient(new ClientDirectoryRequestGate(f.Cache, running, f.Bootstrap, new Handler(() => { }), f.Clock)) { BaseAddress = running.Services.ApiBase };
        f.Cache.AcceptChecked(f.Sign(1, "https://new.example.com/")); f.Cache.ApproveLatestServices();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("v1/me")).StatusCode);
        Assert.Equal("old.example.com", client.BaseAddress.Host);
        Assert.Equal("new.example.com", ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null).Services.ApiBase.Host);
    }

    [Fact]
    public async Task Multiple_pending_approvals_keep_running_proof_until_its_expiry()
    {
        using var f = new Fixture(); f.Cache.AcceptChecked(f.Sign(1));
        var running = ClientServiceDirectory.Select(f.Cache.Read(), f.Bootstrap, null);
        using var client = new HttpClient(new ClientDirectoryRequestGate(f.Cache, running, f.Bootstrap, new Handler(() => { }), f.Clock)) { BaseAddress = running.Services.ApiBase };
        f.Cache.AcceptChecked(f.Sign(2, "https://new.example.com/")); f.Cache.ApproveLatestServices();
        f.Cache.AcceptChecked(f.Sign(3, "https://other.example.com/")); f.Cache.ApproveLatestServices();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("v1/me")).StatusCode);
        f.Clock.Now = f.Clock.Now.AddDays(3);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("v1/me")).StatusCode);
    }

    private sealed class Handler(Action action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { action(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); } }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now { get; set; } = new(2026,10,9,12,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Fixture : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        public Clock Clock { get; } = new();
        public string Path { get; } = System.IO.Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")!, "directory-gate", Guid.NewGuid().ToString("N"), "cache.json");
        public ClientServiceEndpoints Bootstrap { get; } = new(new Uri("https://old.example.com/"), new Uri("https://old.example.com/web/"));
        public ClientReleaseCache Cache { get; }
        public Fixture() { Cache = new(Path, new ClientReleaseVerifier(new Dictionary<string,string> { ["test"] = _key.ExportSubjectPublicKeyInfoPem() }, clock:Clock), Bootstrap, Clock); }
        public SignedClientRelease Sign(long sequence, string host="https://old.example.com/")
        {
            var a = new ClientUpdateArtifact("1.0.0", new Uri("https://updates.example.com/setup"), 1024, new string('a',64));
            return ClientReleaseSigner.Sign(new(1,"videograbber","preview",sequence,Clock.Now,Clock.Now.AddDays(2),"videograbber-main",new(new Uri(host),new Uri(host+"web/")),new("1.0.0",Clock.Now,"Обновление",a,null)),_key,"test",Clock);
        }
        public void Dispose() => _key.Dispose();
    }
}
