using System.Security.Cryptography;
using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ClientUpdateCacheTests
{
    [Fact]
    public void Same_site_catalog_is_active_and_migration_waits_for_approval()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        Assert.Equal(1, f.Cache.Read().Active!.Sequence);
        var move = f.Sign(2, "https://new.example.com/");
        f.Cache.AcceptChecked(move);
        Assert.Equal("old.example.com", f.Cache.Read().Active!.Services.ApiBase.Host);
        Assert.Equal("new.example.com", f.Cache.Read().Latest!.Services.ApiBase.Host);
        f.Cache.ApproveLatestServices();
        Assert.Equal("new.example.com", f.Cache.Read().Active!.Services.ApiBase.Host);
        f.Cache.RestorePreviousServices();
        Assert.Equal("old.example.com", f.Cache.Read().Active!.Services.ApiBase.Host);
        Assert.Equal(2, f.Cache.Read().HighWater);
    }

    [Fact]
    public void Rejects_replay_and_same_sequence_substitution_without_changing_active()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(2));
        Assert.Throws<ClientReleaseRejectedException>(() => f.Cache.AcceptChecked(f.Sign(1)));
        Assert.Throws<ClientReleaseRejectedException>(() => f.Cache.AcceptChecked(f.Sign(2, "https://new.example.com/")));
        Assert.Equal(2, f.Cache.Read().Active!.Sequence);
        // Re-signing identical payload is harmless although RSA-PSS signature differs.
        f.Cache.AcceptChecked(f.Sign(2));
        Assert.Equal(2, f.Cache.Read().HighWater);
    }

    [Fact]
    public void Expired_active_directory_blocks_online_use_and_keeps_signed_history()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        f.Clock.Now = f.Clock.Now.AddDays(3);
        var state = f.Cache.Read();
        Assert.True(state.ActiveBlocked);
        Assert.Equal(1, state.HighWater);
        Assert.NotNull(state.Active);
        f.Cache.AcceptChecked(f.Sign(2));
        Assert.False(f.Cache.Read().ActiveBlocked);
    }

    [Fact]
    public void Corrupt_cache_does_not_authorize_a_fallback_host()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        File.WriteAllText(f.Path, "{invalid}");
        Assert.True(f.Cache.Read().ActiveBlocked);
        Assert.Null(f.Cache.Read().Active);
        Assert.True(File.Exists(f.Path));
    }

    [Fact]
    public void Corrupt_cache_can_recover_verified_metadata_but_requires_explicit_endpoint_approval()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(2));
        File.WriteAllText(f.Path, "{invalid}");
        Assert.Throws<ClientReleaseRejectedException>(() => f.Cache.AcceptChecked(f.Sign(1)));
        f.Cache.AcceptChecked(f.Sign(3, "https://new.example.com/"));
        Assert.True(f.Cache.Read().ActiveBlocked);
        Assert.Null(f.Cache.Read().Active);
        Assert.Equal(3, f.Cache.Read().HighWater);
        f.Cache.ApproveLatestServices();
        Assert.False(f.Cache.Read().ActiveBlocked);
        Assert.Equal("new.example.com", f.Cache.Read().Active!.Services.ApiBase.Host);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("previous")]
    [InlineData("requiresApproval")]
    public void Missing_cache_wrapper_fields_are_corruption_not_bootstrap_authority(string field)
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        var data = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f.Path))!.AsObject();
        data.Remove(field);
        File.WriteAllText(f.Path, data.ToJsonString());
        Assert.True(f.Cache.Read().ActiveBlocked);
        Assert.Null(f.Cache.Read().Active);
    }

    [Fact]
    public void Equal_sequence_different_payload_between_cache_slots_is_rejected()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        var data = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f.Path))!.AsObject();
        data["active"] = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(f.Sign(1, "https://other.example.com/"), ClientReleaseSigner.JsonOptions));
        File.WriteAllText(f.Path, data.ToJsonString());
        Assert.True(f.Cache.Read().ActiveBlocked);
    }

    [Fact]
    public void Duplicate_cache_fields_are_rejected()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        var raw = File.ReadAllText(f.Path);
        File.WriteAllText(f.Path, raw[..^1] + ",\"active\":null}");
        Assert.True(f.Cache.Read().ActiveBlocked);
    }

    [Fact]
    public void Latest_only_pending_directory_keeps_bootstrap_until_approved()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1, "https://new.example.com/"));
        Assert.Null(f.Cache.Read().Active);
        Assert.False(f.Cache.Read().ActiveBlocked);
        f.Cache.ApproveLatestServices();
        Assert.Equal("new.example.com", f.Cache.Read().Active!.Services.ApiBase.Host);
    }

    [Fact]
    public void Expired_pending_migration_cannot_be_approved()
    {
        using var f = new Fixture();
        f.Cache.AcceptChecked(f.Sign(1));
        f.Cache.AcceptChecked(f.Sign(2, "https://new.example.com/"));
        f.Clock.Now = f.Clock.Now.AddDays(3);
        Assert.Throws<ClientReleaseRejectedException>(() => f.Cache.ApproveLatestServices());
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        public Clock Clock { get; } = new();
        public string Path { get; } = System.IO.Path.Combine(Environment.GetEnvironmentVariable("VIDEOGRABBER_EVIDENCE")!, "updates", Guid.NewGuid().ToString("N"), "cache.json");
        public ClientReleaseCache Cache { get; }
        public Fixture()
        {
            var verifier = new ClientReleaseVerifier(new Dictionary<string, string> { ["test"] = _key.ExportSubjectPublicKeyInfoPem() }, clock: Clock);
            Cache = new ClientReleaseCache(Path, verifier, new ClientServiceEndpoints(new Uri("https://old.example.com/"), new Uri("https://old.example.com/web/")), Clock);
        }
        public SignedClientRelease Sign(long sequence, string host = "https://old.example.com/")
        {
            var version = "1.0.0";
            var artifact = new ClientUpdateArtifact(version, new Uri("https://updates.example.com/setup"), 1024, new string('a', 64));
            var manifest = new ClientReleaseManifest(1, "videograbber", "preview", sequence, Clock.Now, Clock.Now.AddDays(2), "videograbber-main", new(new Uri(host), new Uri(host + "web/")), new(version, Clock.Now, "Обновление", artifact, null));
            return ClientReleaseSigner.Sign(manifest, _key, "test", Clock);
        }
        public void Dispose() => _key.Dispose();
    }
}
