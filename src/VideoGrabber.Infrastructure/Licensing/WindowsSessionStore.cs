using System.Security.Cryptography;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class WindowsSessionStore
{
    private readonly string _root;
    private readonly string _path;

    public WindowsSessionStore(string? root = null, string fileName = "session.bin")
    {
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("A simple file name is required.", nameof(fileName));
        _root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGrabber.Managed", "auth"));
        _path = Path.Combine(_root, fileName);
    }

    public async Task SaveAsync(byte[] secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Managed session protection requires Windows DPAPI.");
        Directory.CreateDirectory(_root);
        var protectedBytes = ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser);
        var staging = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(staging, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(staging, _path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            try { if (File.Exists(staging)) File.Delete(staging); } catch (IOException) { }
        }
    }

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Managed session protection requires Windows DPAPI.");
        if (!File.Exists(_path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        try { return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(protectedBytes); }
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Managed session protection requires Windows DPAPI.");
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}