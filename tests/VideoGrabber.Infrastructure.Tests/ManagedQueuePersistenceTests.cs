using VideoGrabber.Core.Licensing;
using VideoGrabber.Infrastructure.Licensing;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ManagedQueuePersistenceTests
{
    [Fact]
    public async Task Restored_queue_requires_revalidation_and_keeps_intent_and_order()
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root);
            var account = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
            await store.SaveAsync(new SavedQueue(1, account,
            [
                new(first, "https://school.example/lesson", "part-2", 2, "720p", "video", "pending"),
                new(second, "https://school.example/lesson", "part-3", 3, "best", "audio", "running")
            ]), CancellationToken.None);

            var restored = await store.LoadAsync(account, CancellationToken.None);

            Assert.Equal([first, second], restored.Items.Select(x => x.IntentId).ToArray());
            Assert.All(restored.Items, x => Assert.Equal("needs_revalidation", x.State));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Foreign_account_never_loads_another_accounts_queue()
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var a = Guid.NewGuid(); var b = Guid.NewGuid();
            await store.SaveAsync(new SavedQueue(1, a,
                [new(Guid.NewGuid(), "https://school.example/lesson", "part-1", 1, "best", "video", "pending")]),
                CancellationToken.None);
            var foreign = await store.LoadAsync(b, CancellationToken.None);
            Assert.Empty(foreign.Items);
            Assert.Equal(b, foreign.AccountId);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Serialized_queue_strips_query_fragment_and_never_keeps_sensitive_url_material()
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var account = Guid.NewGuid();
            await store.SaveAsync(new SavedQueue(1, account,
                [new(Guid.NewGuid(), "https://school.example/lesson?token=SENTINEL_SECRET#player", "part-1", 1, "best", "video", "pending")]),
                CancellationToken.None);
            var text = string.Join("\n", Directory.EnumerateFiles(root).Select(File.ReadAllText));
            Assert.DoesNotContain("SENTINEL_SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("?token=", text, StringComparison.Ordinal);
            Assert.DoesNotContain("#player", text, StringComparison.Ordinal);
            var restored = await store.LoadAsync(account, CancellationToken.None);
            Assert.Equal("https://school.example/lesson", restored.Items.Single().PageOriginPath);
        }
        finally { SafeDelete(root); }
    }

    [Theory]
    [InlineData("http://school.example/lesson")]
    [InlineData("https://user:pass@school.example/lesson")]
    [InlineData("/local/path")]
    public async Task Unsafe_page_origin_is_rejected(string page)
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var account = Guid.NewGuid();
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new SavedQueue(1, account,
                [new(Guid.NewGuid(), page, "part-1", 1, "best", "video", "pending")]), CancellationToken.None));
        }
        finally { SafeDelete(root); }
    }

    [Theory]
    [InlineData("unknown", "video", "best")]
    [InlineData("pending", "exe", "best")]
    [InlineData("pending", "video", "abcdefghijklmnopqrstuvwxyz123456789")]
    public async Task Invalid_item_contract_is_rejected(string state, string outputMode, string quality)
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var account = Guid.NewGuid();
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new SavedQueue(1, account,
                [new(Guid.NewGuid(), "https://school.example/lesson", "part-1", 1, quality, outputMode, state)]), CancellationToken.None));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Interrupted_staging_file_does_not_replace_last_good_snapshot()
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var account = Guid.NewGuid(); var intent = Guid.NewGuid();
            await store.SaveAsync(new SavedQueue(1, account,
                [new(intent, "https://school.example/lesson", "part-1", 1, "best", "video", "pending")]), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root, "interrupted.tmp"), "{broken");
            var restored = await store.LoadAsync(account, CancellationToken.None);
            Assert.Equal(intent, restored.Items.Single().IntentId);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Malformed_snapshot_is_quarantined_without_deleting_it()
    {
        var root = TempRoot();
        try
        {
            var store = new ManagedQueueStore(root); var account = Guid.NewGuid();
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, $"queue-{account:N}.json"), "{broken-json");
            var restored = await store.LoadAsync(account, CancellationToken.None);
            Assert.Empty(restored.Items);
            Assert.DoesNotContain(Directory.EnumerateFiles(root), x => Path.GetFileName(x) == $"queue-{account:N}.json");
            Assert.Contains(Directory.EnumerateFiles(root), x => Path.GetFileName(x).StartsWith($"queue-{account:N}.corrupt-", StringComparison.Ordinal));
        }
        finally { SafeDelete(root); }
    }

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "vg-queue-test-" + Guid.NewGuid().ToString("N"));
    private static void SafeDelete(string root) { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
}