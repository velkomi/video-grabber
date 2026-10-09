using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace VideoGrabber.ClientReleasePublisher.Tests;

public sealed class PrivateKeyAccessPolicyTests
{
    [Theory]
    [InlineData(262144)] // ChangePermissions
    [InlineData(524288)] // TakeOwnership
    [InlineData(4)] // AppendData
    [InlineData(65536)] // Delete
    [InlineData(1)] // ReadData
    public void Foreign_allow_rights_cannot_admit_signing(int rights)
    {
        if (!OperatingSystem.IsWindows()) return;
        var current = new SecurityIdentifier("S-1-5-21-1-2-3-1001");
        var foreign = new SecurityIdentifier("S-1-5-21-1-2-3-1002");
        Assert.False(PrivateKeyAccessPolicy.Allows(current, current,
            [new FileSystemAccessRule(foreign, (FileSystemRights)rights, AccessControlType.Allow)]));
    }

    [Fact]
    public void Foreign_owner_cannot_admit_signing_even_with_current_user_only_Dacl()
    {
        if (!OperatingSystem.IsWindows()) return;
        var current = new SecurityIdentifier("S-1-5-21-1-2-3-1001");
        var foreign = new SecurityIdentifier("S-1-5-21-1-2-3-1002");
        Assert.False(PrivateKeyAccessPolicy.Allows(current, foreign,
            [new FileSystemAccessRule(current, FileSystemRights.FullControl, AccessControlType.Allow)]));
    }

    [Fact]
    public void Current_owner_and_system_are_accepted()
    {
        if (!OperatingSystem.IsWindows()) return;
        var current = new SecurityIdentifier("S-1-5-21-1-2-3-1001");
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        Assert.True(PrivateKeyAccessPolicy.Allows(current, current,
            [new FileSystemAccessRule(current, FileSystemRights.FullControl, AccessControlType.Allow),
             new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow)]));
    }
}
