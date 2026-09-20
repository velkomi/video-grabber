using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using VideoGrabber.Core.Security;
using VideoGrabber.Infrastructure.Downloads;

namespace VideoGrabber.Infrastructure.Browser;

public sealed record CourseLessonAsset(
    Uri Source,
    string Kind,
    string? SuggestedName);

public sealed record CourseLessonSnapshot(
    string Title,
    string Text,
    string Html,
    IReadOnlyList<CourseLessonAsset> Assets);

public static class CourseLessonArchive
{
    private sealed record AssetPayload(
        string? url,
        string? kind,
        string? name);

    private sealed record SnapshotPayload(
        string? title,
        string? text,
        string? html,
        AssetPayload[]? assets);

    public static bool TryParseWebViewResult(
        string json,
        Uri page,
        out CourseLessonSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        SnapshotPayload? payload;
        try
        {
            var source = json;
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind == JsonValueKind.String)
                    source = document.RootElement.GetString() ?? string.Empty;
                else if (document.RootElement.ValueKind == JsonValueKind.Null)
                    return false;
            }
            payload = JsonSerializer.Deserialize<SnapshotPayload>(source);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null) return false;

        var title = CleanText(payload.title, "Урок", 240);
        var text = (payload.text ?? string.Empty)
            .Replace("\0", string.Empty);
        var html = payload.html ?? string.Empty;
        if (text.Length > 2_000_000
            || html.Length > 8_000_000)
            return false;

        var assets = new List<CourseLessonAsset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in (payload.assets ?? []).Take(500))
        {
            if (!UrlPolicy.TryValidate(
                    item.url,
                    out var uri,
                    out _)
                || uri is null)
                continue;
            if (!seen.Add(uri.AbsoluteUri))
                continue;
            var kind = string.Equals(
                item.kind,
                "image",
                StringComparison.OrdinalIgnoreCase)
                ? "image" : "file";
            assets.Add(new(
                uri,
                kind,
                CleanOptional(item.name, 160)));
        }

        snapshot = new(
            title,
            text,
            html,
            assets.AsReadOnly());
        return true;
    }

    public static string LessonFolder(
        int ordinal,
        string? title)
        => GetCourseCourseStructure.OrderedFolder(
            ordinal,
            title,
            "Урок");

    public static string AssetFileName(
        Uri source,
        string? suggestedName,
        string fallback,
        string? contentDispositionFileName = null)
    {
        contentDispositionFileName = RepairFileNameEncoding(
            contentDispositionFileName);
        suggestedName = RepairFileNameEncoding(suggestedName);
        var candidate = CleanOptional(
            contentDispositionFileName,
            150)
            ?? CleanOptional(suggestedName, 150)
            ?? CleanOptional(
                Uri.UnescapeDataString(
                    Path.GetFileName(source.AbsolutePath)),
                150)
            ?? fallback;

        var extension = SafeExtension(
            contentDispositionFileName)
            ?? SafeExtension(source.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(extension)
            && !candidate.EndsWith(
                extension,
                StringComparison.OrdinalIgnoreCase))
            candidate += extension;

        return DownloadFileName.SanitizeBaseName(
            candidate,
            180);
    }

    public static string AssetFileNameForOccurrence(
        string fileName,
        int occurrence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = fileName;
            extension = string.Empty;
        }

        var suffix = occurrence <= 1
            ? string.Empty
            : $" ({occurrence})";
        var maxStemLength = Math.Max(
            1,
            180 - extension.Length - suffix.Length);
        stem = DownloadFileName.SanitizeBaseName(
            stem,
            maxStemLength);
        return stem + suffix + extension;
    }

    public static void WriteDocx(
        string path,
        string title,
        Uri source,
        string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                Path.GetFullPath(path))!);

        var temp = path + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {

            using (var file = new FileStream(
                       temp,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var archive = new ZipArchive(
                       file,
                       ZipArchiveMode.Create,
                       leaveOpen: false))
            {
                WriteEntry(
                    archive,
                    "[Content_Types].xml",
                    ContentTypesXml());
                WriteEntry(
                    archive,
                    "_rels/.rels",
                    RootRelationshipsXml());
                WriteDocumentXml(
                    archive,
                    title,
                    source,
                    text);
            }

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }

        catch
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch { }
            throw;
        }
    }

    private static void WriteDocumentXml(
        ZipArchive archive,
        string title,
        Uri source,
        string text)
    {
        var entry = archive.CreateEntry(
            "word/document.xml",
            CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                CloseOutput = false
            });

        const string w =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        writer.WriteStartDocument();
        writer.WriteStartElement("w", "document", w);
        writer.WriteStartElement("w", "body", w);

        WriteParagraph(
            writer,
            w,
            CleanText(title, "Урок", 240),
            bold: true,
            sizeHalfPoints: 32);
        WriteParagraph(
            writer,
            w,
            "Источник: " + source.AbsoluteUri,
            bold: false,
            sizeHalfPoints: 18);

        foreach (var line in NormalizeLines(text).Take(50_000))
            WriteParagraph(
                writer,
                w,
                line,
                bold: false,
                sizeHalfPoints: 22);

        writer.WriteStartElement("w", "sectPr", w);
        writer.WriteStartElement("w", "pgSz", w);
        writer.WriteAttributeString("w", "w", w, "11906");
        writer.WriteAttributeString("w", "h", w, "16838");
        writer.WriteEndElement();
        writer.WriteStartElement("w", "pgMar", w);
        writer.WriteAttributeString("w", "top", w, "1134");
        writer.WriteAttributeString("w", "right", w, "1134");
        writer.WriteAttributeString("w", "bottom", w, "1134");
        writer.WriteAttributeString("w", "left", w, "1134");
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteParagraph(
        XmlWriter writer,
        string w,
        string value,
        bool bold,
        int sizeHalfPoints)
    {

        writer.WriteStartElement("w", "p", w);
        writer.WriteStartElement("w", "r", w);
        writer.WriteStartElement("w", "rPr", w);
        if (bold)
        {
            writer.WriteStartElement("w", "b", w);
            writer.WriteEndElement();
        }
        writer.WriteStartElement("w", "sz", w);
        writer.WriteAttributeString(
            "w", "val", w, sizeHalfPoints.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("w", "t", w);
        if (value.StartsWith(' ')
            || value.EndsWith(' '))
            writer.WriteAttributeString(
                "xml", "space", null, "preserve");
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static IEnumerable<string> NormalizeLines(
        string text)
    {
        var normalized = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            yield return line;
        }
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string content)
    {
        var entry = archive.CreateEntry(
            name,
            CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false),
            leaveOpen: false);
        writer.Write(content);
    }

    private static string ContentTypesXml()
        => """
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
             <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
             <Default Extension="xml" ContentType="application/xml"/>
             <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
           </Types>
           """;

    private static string RootRelationshipsXml()
        => """
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
             <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
           </Relationships>
           """;

    private static string? RepairFileNameEncoding(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        value = value.Trim().Trim('"');

        var marker = value.IndexOf("''", StringComparison.Ordinal);
        if (marker > 0 && marker + 2 < value.Length)
        {
            try { value = Uri.UnescapeDataString(value[(marker + 2)..]); }
            catch (UriFormatException) { }
        }
        else if (value.Contains('%'))
        {
            try { value = Uri.UnescapeDataString(value); }
            catch (UriFormatException) { }
        }

        if (!value.Contains('Ð') && !value.Contains('Ñ')) return value;
        try
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            var repaired = new UTF8Encoding(false, true).GetString(bytes);
            var before = value.Count(ch => ch is >= '\u0400' and <= '\u04ff');
            var after = repaired.Count(ch => ch is >= '\u0400' and <= '\u04ff');
            return after > before ? repaired : value;
        }
        catch (DecoderFallbackException) { return value; }
    }

    private static string CleanText(
        string? value,
        string fallback,
        int maxLength)
    {
        var clean = CleanOptional(
            value,
            maxLength);
        return string.IsNullOrWhiteSpace(clean)
            ? fallback
            : clean;
    }

    private static string? CleanOptional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value
            .Replace('\0', ' ')
            .Trim();
        if (text.Length > maxLength)
            text = text[..maxLength];
        return string.IsNullOrWhiteSpace(text)
            ? null
            : text;
    }

    private static string? SafeExtension(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var extension = Path.GetExtension(
            value.Split('?', '#')[0]);
        if (extension.Length is < 2 or > 12)
            return null;
        return extension.All(
            c => c == '.'
                || char.IsAsciiLetterOrDigit(c))
            ? extension.ToLowerInvariant()
            : null;
    }
}
