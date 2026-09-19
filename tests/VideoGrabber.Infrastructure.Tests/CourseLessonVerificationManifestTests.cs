using VideoGrabber.Infrastructure.Browser;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class CourseLessonVerificationManifestTests
{
    [Fact]
    public async Task Manifest_roundtrips_expected_video_and_asset_counts()
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-lesson-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = new CourseLessonVerificationManifest(
                CourseLessonVerificationManifestStore.CurrentSchemaVersion,
                "https://academy.example/teach/control/lesson/view/id/123",
                21,
                4,
                "480p",
                DateTimeOffset.UtcNow);
            await CourseLessonVerificationManifestStore.SaveAtomicAsync(root, manifest, CancellationToken.None);

            Assert.True(CourseLessonVerificationManifestStore.TryLoad(root, out var loaded));
            Assert.NotNull(loaded);
            Assert.Equal(21, loaded!.ExpectedVideoCount);
            Assert.Equal(4, loaded.ExpectedAssetCount);
            Assert.Equal("480p", loaded.Quality);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Missing_manifest_is_not_treated_as_verified()
    {
        var root = Path.Combine(Path.GetTempPath(), "vg-lesson-manifest-missing-" + Guid.NewGuid().ToString("N"));
        Assert.False(CourseLessonVerificationManifestStore.TryLoad(root, out var loaded));
        Assert.Null(loaded);
    }
}
