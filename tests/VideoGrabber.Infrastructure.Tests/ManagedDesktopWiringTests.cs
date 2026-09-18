namespace VideoGrabber.Infrastructure.Tests;

public sealed class ManagedDesktopWiringTests
{
    [Fact]
    public void Managed_shell_has_account_page_and_protected_session_wiring()
    {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.xaml.cs"));
        var managed = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Managed.cs"));
        var account = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.Account.cs"));

        Assert.Contains("BuildAccountPage()", account);
        Assert.Contains("SignInProviderAsync", account);
        Assert.Contains("LoadManagedAccountAsync", account);
        Assert.Contains("RevokeManagedDeviceAsync", account);
        Assert.Contains("LinkManagedProviderAsync", account);
        Assert.Contains("UnlinkManagedIdentityAsync", account);
        Assert.Contains("/v1/identities", account);
        Assert.Contains("LinkedProviders", account);
        Assert.Contains("ShowPage(\"account\")", shell);
        Assert.Contains("_accountPage", shell);
        Assert.Contains("WindowsSessionStore", managed);
        Assert.Contains("ManagedQueueStore", managed);
        Assert.Contains("InitializeManagedServices", managed);
        Assert.Contains("_managedAccessToken", managed);
    }

    [Fact]
    public void Managed_browser_queue_persists_mutations_and_has_explicit_revalidation()
    {
        var root = FindRepoRoot();
        var batch = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.App", "MainWindow.BatchDownload.cs"));

        Assert.Contains("PersistManagedQueueSnapshot", batch);
        Assert.Contains("TryRevalidateManagedQueueAsync", batch);
        Assert.Contains("needs_revalidation", batch);
        Assert.Contains("ManagedMediaIdentity", batch);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
