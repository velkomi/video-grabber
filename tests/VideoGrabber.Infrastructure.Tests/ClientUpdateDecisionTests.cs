using VideoGrabber.Infrastructure.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ClientUpdateDecisionTests
{
    [Fact]
    public void Newer_numeric_preview_notifies_without_service_change()
    {
        var manifest = Manifest("0.1.10-preview.10-rc.1");
        var state = ClientUpdateDecision.Evaluate("0.1.10-preview.9-rc.1+commit", manifest.Services, manifest, DateTimeOffset.UtcNow);
        Assert.True(state.UpdateAvailable);
        Assert.False(state.ServicesChanged);
    }

    [Fact]
    public void Older_release_never_suggests_automatic_downgrade_but_host_migration_notifies()
    {
        var manifest = Manifest("0.1.10-preview.9-rc.1");
        var state = ClientUpdateDecision.Evaluate("0.1.10-preview.10-rc.1", new(new Uri("https://previous.example.com/"), new Uri("https://previous.example.com/web/")), manifest, DateTimeOffset.UtcNow);
        Assert.False(state.UpdateAvailable);
        Assert.True(state.ServicesChanged);
    }

    [Fact]
    public void Expired_catalog_cannot_offer_new_origins_or_packages()
    {
        var manifest = Manifest("1.0.0") with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var state = ClientUpdateDecision.Evaluate("0.1.0", new(new Uri("https://previous.example.com/"), new Uri("https://previous.example.com/web/")), manifest, DateTimeOffset.UtcNow);
        Assert.False(state.UpdateAvailable);
        Assert.False(state.ServicesChanged);
    }

    private static ClientReleaseManifest Manifest(string version)
    {
        var now = DateTimeOffset.UtcNow;
        return new(1, "videograbber", "preview", 1, now, now.AddDays(1), "videograbber-main",
            new(new Uri("https://api.example.com/"), new Uri("https://site.example.com/web/")),
            new(version, now, "Обновление", new(version, new Uri("https://updates.example.com/setup"), 1024, new string('a', 64)), null));
    }
}
