using VideoGrabber.Platform.Worker;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Worker.Tests;

public sealed class ArtifactRetentionTests
{
    [Fact]
    public async Task Expired_tombstoned_owned_artifact_is_deleted_only_once()
    {
        var root = TempRoot();
        var nested = Path.Combine(root, "account", "job", "attempt");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "result.mp4");
        await File.WriteAllBytesAsync(file, [1,2,3,4]);
        var now = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
        var worker = new ArtifactRetentionWorker(new FixedTimeProvider(now));
        try
        {
            var candidate = new RetentionCandidate(
                Guid.NewGuid(), root, file, now.AddMinutes(-1),
                Tombstoned: true, ActiveDeliveryOrReaderLease: false);

            var first = Assert.Single(await worker.CleanupAsync(
                [candidate], CancellationToken.None));
            Assert.True(first.Deleted);
            Assert.False(File.Exists(file));

            var second = Assert.Single(await worker.CleanupAsync(
                [candidate], CancellationToken.None));
            Assert.False(second.Deleted);
            Assert.Equal("already_missing", second.Reason);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Active_delivery_or_reader_blocks_cleanup()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "result.mp4");
        await File.WriteAllBytesAsync(file, [1,2,3]);
        var now = DateTimeOffset.UtcNow;
        try
        {
            var result = Assert.Single(await new ArtifactRetentionWorker(
                    new FixedTimeProvider(now))
                .CleanupAsync(
                    [new RetentionCandidate(
                        Guid.NewGuid(), root, file, now.AddHours(-1),
                        Tombstoned: true,
                        ActiveDeliveryOrReaderLease: true)],
                    CancellationToken.None));
            Assert.False(result.Deleted);
            Assert.Equal("active_delivery_or_reader", result.Reason);
            Assert.True(File.Exists(file));
        }
        finally { SafeDelete(root); }
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("traversal")]
    [InlineData("reparse")]
    public void Unsafe_paths_are_rejected(string mode)
    {
        var root = Path.GetFullPath(TempRoot());
        var path = mode switch
        {
            "foreign" => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(root)!,
                "foreign-" + Guid.NewGuid().ToString("N"),
                "result.mp4")),
            "traversal" => Path.GetFullPath(Path.Combine(
                root, "..", "outside", "result.mp4")),
            _ => Path.Combine(root, "link.mp4")
        };
        var attributes = mode == "reparse"
            ? FileAttributes.ReparsePoint
            : FileAttributes.Normal;

        Assert.False(ArtifactRetentionWorker.IsSafeOwnedPath(
            root, path, attributes));
    }

    [Fact]
    public async Task Retention_not_expired_does_not_delete()
    {
        var root = TempRoot();
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "future.mp4");
        await File.WriteAllBytesAsync(file, [1,2,3]);
        var now = DateTimeOffset.UtcNow;
        try
        {
            var result = Assert.Single(await new ArtifactRetentionWorker(
                    new FixedTimeProvider(now))
                .CleanupAsync(
                    [new RetentionCandidate(
                        Guid.NewGuid(), root, file, now.AddHours(1),
                        Tombstoned: true,
                        ActiveDeliveryOrReaderLease: false)],
                    CancellationToken.None));
            Assert.False(result.Deleted);
            Assert.Equal("retention_not_expired", result.Reason);
            Assert.True(File.Exists(file));
        }
        finally { SafeDelete(root); }
    }

    private static string TempRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "vg-retention-" + Guid.NewGuid().ToString("N"));

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
