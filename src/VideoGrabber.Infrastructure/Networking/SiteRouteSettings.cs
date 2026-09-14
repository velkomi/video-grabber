using System.Net;
using System.Text.Json;
namespace VideoGrabber.Infrastructure.Networking;

public sealed record SiteRouteRule(string Host, string AdapterId, bool IncludeSubdomains = false);
public sealed class SiteRouteSettings
{
    public IReadOnlyList<SiteRouteRule> Rules { get; }
    public SiteRouteSettings(IEnumerable<SiteRouteRule> rules)
    {
        var list = rules.Select(r => new SiteRouteRule(NormalizeHost(r.Host), r.AdapterId.Trim(), r.IncludeSubdomains)).ToArray();
        if (list.Length > 64 || list.Any(r => string.IsNullOrEmpty(r.AdapterId) || r.AdapterId.Length > 256 || r.AdapterId.Any(char.IsControl)))
            throw new ArgumentException("Некорректный адаптер или превышен лимит 64 правил.");
        if (list.Select(r => r.Host).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Length)
            throw new ArgumentException("Для этого домена уже есть правило.");
        if (list.Any(r => r.IncludeSubdomains)) throw new ArgumentException("Only exact host rules are supported; add each subdomain explicitly.");
        Rules = Array.AsReadOnly(list);
    }
    public SiteRouteRule? Find(string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        return Rules.Where(r => host == r.Host)
            .OrderByDescending(r => r.Host.Length).FirstOrDefault();
    }
    public static string NormalizeHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) || value.Contains('*'))
            throw new ArgumentException("Введите один домен или ссылку на сайт. Маски * не допускаются.");
        if (!Uri.TryCreate(value.Contains("://", StringComparison.Ordinal) ? value.Trim() : "https://" + value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) || uri.Port is not (80 or 443))
            throw new ArgumentException("Нужен HTTP/HTTPS-сайт без пароля в ссылке и нестандартного порта.");
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        var labels = host.Split('.');
        if (host.Length > 253 || labels.Length < 2 || IPAddress.TryParse(host, out _) || labels.Any(l => l.Length is < 1 or > 63
            || l.StartsWith('-') || l.EndsWith('-') || l.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))))
            throw new ArgumentException("Нужно полное имя сайта, не IP-адрес и не локальный узел.");
        return host;
    }
    private sealed record Stored(int Version, SiteRouteRule[] Rules);
    public static SiteRouteSettings Load(string path)
    {
        if (!File.Exists(path)) return new([]);
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Файл правил слишком большой.");
        var data = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
        if (data is null || data.Version != 1 || data.Rules is null)
            throw new InvalidDataException("Правила повреждены. Подключение заблокировано до исправления настроек.");
        return new(data.Rules);
    }
    public void Save(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Stored(1, Rules.ToArray()), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
