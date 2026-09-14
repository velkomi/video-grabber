using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class BrowserUniqueBindingTests
{
    private static MediaCandidate Candidate(string id, Uri referer, string? frameId = null)
        => new(new Uri($"https://cdn.example/{id}.m3u8"), referer, "HLS", FrameId: frameId,
            HlsManifest: new HlsManifestInfo(true,
                [new HlsVariant(new Uri($"https://cdn.example/{id}-720.m3u8"), 1280, 720, 1_000_000, 30, null)],
                [], false, null, false));

    [Fact]
    public void BindAll_keeps_ordinals_unique_when_frame_fallback_collides_with_exact_dom_match()
    {
        var slot1 = new Uri("https://api1.gcvh.ru/sign-player/?id=one");
        var slot2 = new Uri("https://api2.gcvh.ru/sign-player/?id=two");
        var slot3 = new Uri("https://api3.gcvh.ru/sign-player/?id=three");
        var metadata = new BrowserPageMetadata("Day 1", ["Part 1", "Part 2", "Part 3"],
        [
            new BrowserPlayerSlot(1, "Part 1", slot1),
            new BrowserPlayerSlot(2, "Part 2", slot2),
            new BrowserPlayerSlot(3, "Part 3", slot3)
        ]);
        var frames = new[]
        {
            new DevToolsFrameInfo("root", new Uri("https://school.example/lesson"), 0),
            new DevToolsFrameInfo("f1", slot1, 1),
            new DevToolsFrameInfo("f2", slot2, 2),
            new DevToolsFrameInfo("f3", slot3, 3)
        };
        var exact2 = Candidate("exact2", slot2);
        var fallbackCollision = Candidate("fallback", new Uri("https://school.example/lesson"), "f2");
        var exact3 = Candidate("exact3", slot3);

        var bound = BrowserFrameBindingResolver.BindAll([exact2, fallbackCollision, exact3], frames, metadata);

        Assert.Equal([2, 1, 3], bound.Select(item => item.PageOrdinal!.Value).ToArray());
        Assert.Equal(3, bound.Select(item => item.PageOrdinal).Distinct().Count());
        Assert.Equal("Part 1", bound[1].PageSectionTitle);
    }
}

public sealed class BrowserUniqueBindingWiringTests
{
    [Fact]
    public void App_uses_global_unique_binding_and_keeps_russian_part_pattern_intact()
    {
        var root = FindRepoRoot();
        var devtools = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.DevTools.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Metadata.cs"));

        Assert.Contains("BrowserFrameBindingResolver.BindAll", devtools);
        Assert.Contains("BrowserFrameBindingResolver.BindAll", metadata);
        Assert.DoesNotContain("(?:?????|part)", metadata, StringComparison.Ordinal);
        Assert.Contains("\\u0427\\u0430\\u0441\\u0442\\u044c", metadata, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.App"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
