namespace VideoGrabber.Infrastructure.Tests;

public sealed class ReleaseConfigurationTests
{
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root is required for packaging contract tests.");
    }

    [Fact]
    public void Release_script_forwards_requested_version_to_publisher()
    {
        var script = File.ReadAllLines(Path.Combine(Root(), "scripts", "Build-Release.ps1"));
        var publish = Assert.Single(script, line => line.Contains("& $DotNet publish", StringComparison.Ordinal));
        Assert.Contains("-p:Version=$Version", publish);
    }

    [Fact]
    public void Release_script_includes_current_workflow_guide()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "scripts", "Build-Release.ps1"));
        Assert.Contains("docs\\MEDIA_WORKFLOWS.md", script);
        Assert.True(File.Exists(Path.Combine(Root(), "docs", "MEDIA_WORKFLOWS.md")));
    }
}
