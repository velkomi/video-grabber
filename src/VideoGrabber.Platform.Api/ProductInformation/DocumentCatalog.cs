using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.ProductInformation;

public sealed class DocumentCatalog(string path)
{
    public string Snapshot(string id, string expectedHash)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 512 * 1024) throw new InvalidDataException("documents_unavailable");
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var raw = JsonSerializer.Serialize(json.RootElement.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("id").GetString() == id));
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant() != expectedHash) throw new ConsentConflictException();
        return raw;
    }
    public IReadOnlyList<ProductDocument> Read()
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 512 * 1024) throw new InvalidDataException("documents_unavailable");
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var documents = new List<ProductDocument>();
        foreach (var d in json.RootElement.GetProperty("documents").EnumerateArray())
        {
            var id = d.GetProperty("id").GetString()!;
            var version = d.GetProperty("version").GetString()!;
            var title = d.GetProperty("title").GetString()!;
            if (string.IsNullOrEmpty(id) || id.Length > 32 || id.Any(c => !char.IsAsciiLetterLower(c) && c != '_')
                || string.IsNullOrEmpty(version) || version.Length > 64 || string.IsNullOrWhiteSpace(title)
                || documents.Any(x => x.Id == id)) throw new InvalidDataException("documents_invalid");
            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(d))).ToLowerInvariant();
            documents.Add(new(id, title, version, hash, "/info/?document=" + id));
        }
        if (documents.Count is < 1 or > 32) throw new InvalidDataException("documents_invalid");
        return documents;
    }

    public ProductDocument Validate(ConsentRequest request)
    {
        if (request.IntentId == Guid.Empty || request.OperationId == Guid.Empty
            || request.Decision is not ("accepted" or "revoked")) throw new ArgumentException("invalid_consent");
        var expected = request.Purpose switch { "terms" or "course_rights" => "terms", "marketing" => "consent", _ => null };
        if (expected is null || expected != request.DocumentId) throw new ArgumentException("invalid_consent_purpose");
        var document = Read().SingleOrDefault(d => d.Id == request.DocumentId)
            ?? throw new ArgumentException("unknown_document");
        if (document.Version != request.Version || document.Sha256 != request.DocumentHash)
            throw new ConsentConflictException();
        return document;
    }
}
