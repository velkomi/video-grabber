using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;

namespace VideoGrabber.Infrastructure.Components;

public sealed record ComponentSetSnapshot
{
    public ComponentSetSnapshot(
        string generationDirectory,
        string ytDlp,
        string ffmpeg,
        string ffprobe,
        string deno,
        IReadOnlyDictionary<string, string> sha256)
    {
        GenerationDirectory = Path.GetFullPath(generationDirectory);
        YtDlp = Path.GetFullPath(ytDlp);
        Ffmpeg = Path.GetFullPath(ffmpeg);
        Ffprobe = Path.GetFullPath(ffprobe);
        Deno = Path.GetFullPath(deno);
        Sha256 = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(sha256, StringComparer.OrdinalIgnoreCase));
        Verify();
    }

    public string GenerationDirectory { get; }
    public string YtDlp { get; }
    public string Ffmpeg { get; }
    public string Ffprobe { get; }
    public string Deno { get; }
    public IReadOnlyDictionary<string, string> Sha256 { get; }

    public static ComponentSetSnapshot Load(string root)
    {
        var pointerPath = Path.Combine(root, "components-current.json");
        if (!File.Exists(pointerPath))
            throw new FileNotFoundException("Component pointer does not exist.", pointerPath);
        if (new FileInfo(pointerPath).Length is <= 0 or > 65536)
            throw new InvalidDataException("Component pointer size is invalid.");

        Pointer? pointer;
        try
        {
            pointer = JsonSerializer.Deserialize<Pointer>(File.ReadAllText(pointerPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Component pointer is malformed.", ex);
        }
        if (pointer is null || pointer.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(pointer.Generation) || pointer.Sha256 is null)
            throw new InvalidDataException("Component pointer is incomplete.");

        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.IsPathRooted(pointer.Generation))
            throw new InvalidDataException("Component generation must be relative.");
        var generation = Path.GetFullPath(Path.Combine(root, pointer.Generation.Replace('/', Path.DirectorySeparatorChar)));
        if (!generation.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Component generation escapes the tools root.");

        string Tool(string name) => Path.Combine(generation, name);
        return new ComponentSetSnapshot(generation,
            Tool("yt-dlp.exe"), Tool("ffmpeg.exe"), Tool("ffprobe.exe"), Tool("deno.exe"),
            pointer.Sha256);
    }

    public void Verify()
    {
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["yt-dlp.exe"] = YtDlp,
            ["ffmpeg.exe"] = Ffmpeg,
            ["ffprobe.exe"] = Ffprobe,
            ["deno.exe"] = Deno
        };
        foreach (var (name, path) in expected)
        {
            if (!IsWithinGeneration(path) || !File.Exists(path))
                throw new InvalidDataException("Component generation is incomplete: " + name);
            if (!Sha256.TryGetValue(name, out var expectedHash)
                || expectedHash.Length != 64 || expectedHash.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Trusted digest is missing for " + name);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Component digest mismatch: " + name);
        }
    }

    private bool IsWithinGeneration(string path)
    {
        var root = GenerationDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Pointer(int SchemaVersion, string Generation, Dictionary<string, string> Sha256);
}
