using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using VideoGrabber.Core.ClientUpdates;

namespace VideoGrabber.ClientReleasePublisher;

public static class ReleasePublisher
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length == 0 || args[0] is not ("generate-key" or "sign"))
                return await UsageAsync(error);
            var required = args[0] == "generate-key"
                ? new[] { "--private-key", "--public-key", "--key-id" }
                : new[] { "--input", "--private-key", "--key-id", "--output" };
            var options = ParseOptions(args, required);
            if (options is null || !Regex.IsMatch(options["--key-id"], "\\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\\z"))
                return await UsageAsync(error);
            if (args[0] == "generate-key")
                await GenerateKeyAsync(options, output);
            else
                await SignAsync(options, output);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or CryptographicException or InvalidOperationException or ClientReleaseRejectedException)
        {
            await error.WriteLineAsync("publisher_failed: check the draft, protected key and unused output paths.");
            return 1;
        }
    }

    private static Dictionary<string, string>? ParseOptions(string[] args, string[] required)
    {
        if (args.Length != 1 + required.Length * 2)
            return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
            if (!required.Contains(args[index], StringComparer.Ordinal) || string.IsNullOrWhiteSpace(args[index + 1])
                || !result.TryAdd(args[index], args[index + 1]))
                return null;
        return result;
    }

    private static async Task<int> UsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("generate-key --private-key PATH --public-key PATH --key-id ID");
        await error.WriteLineAsync("sign --input DRAFT.json --private-key PATH --key-id ID --output envelope.json");
        return 2;
    }

    private static async Task GenerateKeyAsync(Dictionary<string, string> options, TextWriter output)
    {
        var privatePath = Path.GetFullPath(options["--private-key"]);
        var publicPath = Path.GetFullPath(options["--public-key"]);
        EnsureDistinctUnusedOutputs(privatePath, publicPath);
        using var key = RSA.Create(3072);
        await WriteAtomicAsync(privatePath, Encoding.UTF8.GetBytes(key.ExportPkcs8PrivateKeyPem()), true);
        await WriteAtomicAsync(publicPath, Encoding.UTF8.GetBytes(key.ExportSubjectPublicKeyInfoPem()), false);
        await output.WriteLineAsync($"Generated key id: {options["--key-id"]}; private and public files saved.");
    }

    private static async Task SignAsync(Dictionary<string, string> options, TextWriter output)
    {
        var inputPath = Path.GetFullPath(options["--input"]);
        var privatePath = Path.GetFullPath(options["--private-key"]);
        var destination = Path.GetFullPath(options["--output"]);
        EnsureDistinctUnusedOutputs(destination);
        // Validate public draft before touching the protected private key.
        var manifest = ClientReleaseSigner.DeserializePayload(await ReadBoundedAsync(inputPath, ClientReleaseVerifier.MaxPayloadBytes));
        ClientReleaseVerifier.ValidateManifest(manifest, "videograbber-main", "preview", DateTimeOffset.UtcNow);
        RequireProtectedPrivateKey(privatePath);
        using var key = RSA.Create();
        key.ImportFromPem(Encoding.UTF8.GetString(await ReadBoundedAsync(privatePath, 32 * 1024)));
        var envelope = ClientReleaseSigner.Sign(manifest, key, options["--key-id"]);
        await WriteAtomicAsync(destination, ClientReleaseSigner.SerializeEnvelope(envelope), false);
        await output.WriteLineAsync($"Signed sequence {manifest.Sequence}, version {manifest.Release.Version}, key id {options["--key-id"]}.");
    }

    private static void RequireProtectedPrivateKey(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            var sharedPermissions = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & sharedPermissions) != 0)
                throw new UnauthorizedAccessException();
            return;
        }
        var owner = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
        var security = new FileInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        if (!PrivateKeyAccessPolicy.Allows(owner, (SecurityIdentifier?)security.GetOwner(typeof(SecurityIdentifier)),
            security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()))
            throw new UnauthorizedAccessException();
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, int maximumBytes)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximumBytes)
            throw new IOException("Input size is invalid.");
        using var contents = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer)) != 0)
        {
            if (contents.Length + read > maximumBytes)
                throw new IOException("Input is too large.");
            contents.Write(buffer, 0, read);
        }
        return contents.ToArray();
    }

    private static void EnsureDistinctUnusedOutputs(params string[] paths)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (paths.Distinct(comparison).Count() != paths.Length
            || paths.Any(path => File.Exists(path) || Directory.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path))))
            throw new IOException("Output paths must be distinct, unused and in existing directories.");
    }

    private static async Task WriteAtomicAsync(string destination, byte[] bytes, bool protectPrivate)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (protectPrivate && !OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(temporary, options))
            {
                if (protectPrivate && OperatingSystem.IsWindows())
                {
                    var owner = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
                    var security = new FileSecurity();
                    security.SetOwner(owner);
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
                    new FileInfo(temporary).SetAccessControl(security);
                }
                await stream.WriteAsync(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
