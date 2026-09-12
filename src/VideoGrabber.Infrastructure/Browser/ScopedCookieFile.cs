using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record BrowserCookie(string Domain, string Path, string Name, string Value, bool Secure, bool HttpOnly);

public sealed class ScopedCookieFile : IDisposable
{
    private readonly string _directory;
    public string Path { get; }
    private ScopedCookieFile(string directory) { _directory = directory; Path = System.IO.Path.Combine(directory, "session.txt"); }
    public static ScopedCookieFile Create(IReadOnlyList<Uri> sources, IEnumerable<BrowserCookie> cookies)
    {
        var directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGrabber", "sessions", "cookie-" + Guid.NewGuid().ToString("N"));
        var result = new ScopedCookieFile(directory);
        try
        {
            Directory.CreateDirectory(directory);
            // Restrict only our newly created secret directory; existing folder permissions are not changed.
            if (OperatingSystem.IsWindows())
            {
                var user = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException("Не определён пользователь Windows.");
                var acl = new DirectorySecurity();
                acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                acl.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(directory).SetAccessControl(acl);
            }
            var lines = new List<string> { "# Netscape HTTP Cookie File", "# Temporary, scoped, session-only; deleted after this download." };
            foreach (var cookie in cookies.Take(500).Where(c => sources.Any(u => Matches(c, u))))
            {
                if (new[] { cookie.Domain, cookie.Path, cookie.Name, cookie.Value }.Any(v => v.IndexOfAny(['\r', '\n', '\t']) >= 0)) continue;
                lines.Add(string.Join('\t', (cookie.HttpOnly ? "#HttpOnly_" : "") + cookie.Domain,
                    cookie.Domain.StartsWith('.') ? "TRUE" : "FALSE", cookie.Path,
                    cookie.Secure ? "TRUE" : "FALSE", "0", cookie.Name, cookie.Value));
            }
            File.WriteAllLines(result.Path, lines, new UTF8Encoding(false));
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public static bool Matches(BrowserCookie cookie, Uri source)
    {
        if (source.Scheme is not ("http" or "https") || cookie.Secure && source.Scheme != "https") return false;
        var domain = cookie.Domain.TrimStart('.');
        var hostMatches = source.IdnHost.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || cookie.Domain.StartsWith('.') && source.IdnHost.EndsWith('.' + domain, StringComparison.OrdinalIgnoreCase);
        var path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
        var requested = source.AbsolutePath;
        return domain.Length > 0 && hostMatches && (requested == path || requested.StartsWith(path, StringComparison.Ordinal)
            && (path.EndsWith('/') || requested.Length > path.Length && requested[path.Length] == '/'));
    }

    public void Dispose()
    {
        if (File.Exists(Path)) File.Delete(Path);
        if (Directory.Exists(_directory)) Directory.Delete(_directory);
    }
}
