using System.Collections.Frozen;

namespace VideoGrabber.Infrastructure.Downloads;

/// <summary>An initially empty directory whose files belong to one download attempt.</summary>
public sealed class DownloadWorkspace
{
    private readonly HashSet<string> ownedFiles = new(StringComparer.OrdinalIgnoreCase);

    private DownloadWorkspace(string outputDirectory, string root)
    {
        OutputDirectory = outputDirectory;
        Root = root;
    }

    internal string OutputDirectory { get; }
    public string Root { get; }
    public IReadOnlySet<string> OwnedFiles => ownedFiles.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static DownloadWorkspace Create(string outputDirectory, string? requestedParent)
    {
        var output = Path.GetFullPath(outputDirectory);
        var parent = string.IsNullOrWhiteSpace(requestedParent) ? output : Path.GetFullPath(requestedParent);
        EnsureContained(output, parent);
        RejectReparseComponents(output);
        RejectReparseComponents(parent);
        Directory.CreateDirectory(parent);
        RejectReparseComponents(parent);
        string root;
        do { root = Path.Combine(parent, ".vg-job-" + Guid.NewGuid().ToString("N")); }
        while (Path.Exists(root));
        Directory.CreateDirectory(root);
        RejectReparseComponents(root);
        return new DownloadWorkspace(output, root);
    }

    public void RegisterCreatedFile(string path)
    {
        var fullPath = ValidateOwnedPath(path);
        if (!File.Exists(fullPath)) throw new InvalidOperationException("Only an existing job file can be registered.");
        ownedFiles.Add(fullPath);
    }

    public bool Owns(string path)
    {
        try { return ownedFiles.Contains(ValidateOwnedPath(path)); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        { return false; }
    }

    internal string ValidateOwnedPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        EnsureContained(Root, fullPath);
        RejectReparseComponents(fullPath);
        return fullPath;
    }

    internal void ValidateDestination(string path)
    {
        EnsureContained(OutputDirectory, Path.GetFullPath(path));
        RejectReparseComponents(path);
        RejectReparseComponents(Root);
    }

    internal void DiscoverCreatedFiles()
    {
        var pending = new Stack<string>();
        pending.Push(Root);
        while (pending.TryPop(out var directory))
        {
            ValidateOwnedPath(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ValidateOwnedPath(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else RegisterCreatedFile(entry);
            }
        }
    }

    internal void CleanupVerifiedIntermediates()
    {
        foreach (var path in OwnedFiles)
        {
            try { if (Owns(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        // Never recursively delete: an unregistered file or new directory must survive.
        try { ValidateOwnedPath(Root); Directory.Delete(Root, recursive: false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    }

    private static void EnsureContained(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Output is outside the owned job directory.");
    }

    private static void RejectReparseComponents(string path)
    {
        var current = Path.GetFullPath(path);
        while (current is not null)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Reparse points are not allowed in download paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
    }
}
