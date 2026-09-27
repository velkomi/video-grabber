using VideoGrabber.Infrastructure.Transcription;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class WhisperModelCatalogTests
{
    [Fact]
    public void Profiles_are_unique_https_and_have_integrity_metadata()
    {
        Assert.Equal(3, WhisperModelCatalog.Profiles.Count);
        Assert.Equal(
            WhisperModelCatalog.Profiles.Count,
            WhisperModelCatalog.Profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var profile in WhisperModelCatalog.Profiles)
        {
            Assert.StartsWith("https://", profile.SourcePageUrl, StringComparison.Ordinal);
            Assert.StartsWith("https://", profile.DownloadUrl, StringComparison.Ordinal);
            Assert.True(profile.ExpectedBytes > 50 * 1024 * 1024);
            Assert.Matches("^[0-9a-f]{64}$", profile.ExpectedSha256);
            Assert.EndsWith(".bin", profile.FileName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("base", 147951465)]
    [InlineData("small-q5_1", 190085487)]
    [InlineData("medium-q5_0", 539212467)]
    public void Profiles_pin_expected_file_sizes(string id, long expectedBytes)
    {
        Assert.Equal(expectedBytes, WhisperModelCatalog.Get(id).ExpectedBytes);
    }

    [Fact]
    public void Unknown_profile_falls_back_to_bundled_base()
    {
        var profile = WhisperModelCatalog.Get("does-not-exist");
        Assert.Equal("base", profile.Id);
        Assert.True(profile.Bundled);
    }
}
