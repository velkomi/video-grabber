using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class DownloadWorkspaceResumeTests
{
    [Fact]
    public void Stable_resume_key_reuses_same_owned_workspace_and_discovers_partial()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-workspace-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string key = "0123456789abcdef01234567";
            var first = DownloadWorkspace.Create(
                root,
                requestedParent: null,
                resumeKey: key);
            var partial = Path.Combine(
                first.Root,
                "Lesson - downloading.mp4.part");
            File.WriteAllBytes(partial, [1, 2, 3, 4]);

            var second = DownloadWorkspace.Create(
                root,
                requestedParent: null,
                resumeKey: key);

            Assert.Equal(first.Root, second.Root);
            Assert.Contains(
                Path.GetFullPath(partial),
                second.OwnedFiles);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("short")]
    [InlineData("bad key with spaces")]
    [InlineData("../escape")]
    public void Invalid_resume_key_is_rejected(string key)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VG-workspace-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<ArgumentException>(
                () => DownloadWorkspace.Create(
                    root,
                    requestedParent: null,
                    resumeKey: key));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
