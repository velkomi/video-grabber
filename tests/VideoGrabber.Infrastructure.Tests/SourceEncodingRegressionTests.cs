namespace VideoGrabber.Infrastructure.Tests;

public sealed class SourceEncodingRegressionTests
{
    [Fact]
    public void Source_has_no_corrupted_placeholder_text()
    {
        var root = FindRepoRoot();
        var bad = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(x => x.line.Contains("????", StringComparison.Ordinal)
                || x.line.Contains('\uFFFD'))
            .Select(x => $"{Path.GetRelativePath(root, x.path)}:{x.index + 1}: {x.line.Trim()}")
            .ToArray();

        Assert.True(bad.Length == 0, string.Join(Environment.NewLine, bad));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "VideoGrabber.App")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
