namespace VideoGrabber.Infrastructure.Tests;

public sealed class ReleaseConfigurationTests
{
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null &&
               !File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx")))
            current = current.Parent;
        return current?.FullName ??
            throw new DirectoryNotFoundException(
                "Repository root is required for packaging contract tests.");
    }

    [Fact]
    public void Release_script_forwards_requested_version_and_source_sha()
    {
        var script = File.ReadAllText(
            Path.Combine(Root(), "scripts", "Build-Release.ps1"));
        Assert.Contains("-p:Version=$Version", script);
        Assert.Contains("sourceCommit = $SourceCommit", script);
        Assert.Contains("release-files.sha256", script);
    }

    [Fact]
    public void Release_script_includes_current_workflow_guide()
    {
        var script = File.ReadAllText(
            Path.Combine(Root(), "scripts", "Build-Release.ps1"));
        Assert.Contains("docs\\MEDIA_WORKFLOWS.md", script);
        Assert.True(File.Exists(
            Path.Combine(Root(), "docs", "MEDIA_WORKFLOWS.md")));
    }

    [Fact]
    public void Release_script_separates_all_named_targets()
    {
        var script = File.ReadAllText(
            Path.Combine(Root(), "scripts", "Build-Release.ps1"));
        Assert.Contains(
            "[ValidateSet('Local', 'Managed', 'Api', 'Worker')]",
            script);
        Assert.Contains("VideoGrabber.Local-$Runtime", script);
        Assert.Contains("VideoGrabber.Managed-$Runtime", script);
        Assert.Contains("VideoGrabber.Platform.Api-$Runtime", script);
        Assert.Contains("VideoGrabber.Platform.Worker-$Runtime", script);
        Assert.Contains("-p:VideoGrabberEdition=Local", script);
        Assert.Contains("-p:VideoGrabberEdition=Managed", script);
    }
}
