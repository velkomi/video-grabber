using System.Text.Json;
using VideoGrabber.Core.ClientUpdates;

namespace VideoGrabber.Platform.Api.ClientUpdates;

public static class ClientReleaseEndpoints
{
    private const int MaximumEnvelopeBytes = ClientReleaseVerifier.MaxEnvelopeBytes;

    public static IEndpointRouteBuilder MapClientReleaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/client-release/preview", CatalogAsync).AllowAnonymous();
        endpoints.MapGet("/download/windows/releases/{version}/setup", Installer).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> CatalogAsync(HttpContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var path = configuration["VG_CLIENT_RELEASE_MANIFEST_PATH"]
            ?? "/var/lib/videograbber/downloads/client-release.json";
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaximumEnvelopeBytes)
                return UnavailableCatalog();
            using var contents = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (contents.Length + read > MaximumEnvelopeBytes)
                    return UnavailableCatalog();
                contents.Write(buffer, 0, read);
            }
            var bytes = contents.ToArray();
            if (!IsEnvelope(bytes))
                return UnavailableCatalog();
            return Results.Bytes(bytes, "application/json");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound(new { code = "client_release_unavailable" });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or FormatException or ArgumentException or ClientReleaseRejectedException)
        {
            return UnavailableCatalog();
        }
    }

    private static bool IsEnvelope(byte[] bytes)
    {
        var envelope = ClientReleaseSigner.DeserializeEnvelope(bytes);
        var payloadBytes = Convert.FromBase64String(envelope.Payload);
        var signatureBytes = Convert.FromBase64String(envelope.Signature);
        if (payloadBytes.Length is <= 0 or > ClientReleaseVerifier.MaxPayloadBytes || signatureBytes.Length is < 256 or > 1024)
            return false;
        using var document = JsonDocument.Parse(payloadBytes);
        return document.RootElement.ValueKind == JsonValueKind.Object;
    }

    private static IResult Installer(string version, HttpContext context, IConfiguration configuration)
    {
        if (version.Length > 128 || version.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '+')))
            return MissingInstaller();
        try
        {
            _ = ClientReleaseVersion.Parse(version);
            var root = Path.GetFullPath(configuration["VG_WINDOWS_RELEASES_PATH"]
                ?? "/var/lib/videograbber/downloads/releases");
            var versionDirectory = Path.GetFullPath(Path.Combine(root, version));
            var path = Path.GetFullPath(Path.Combine(versionDirectory, "VideoGrabber-Setup.exe"));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison)
                || !File.Exists(path) || HasLink(root) || HasLink(versionDirectory) || HasLink(path))
                return MissingInstaller();
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(path, "application/vnd.microsoft.portable-executable",
                $"VideoGrabber-Setup-{version}.exe", enableRangeProcessing: true);
        }
        catch (Exception exception) when (exception is ClientReleaseRejectedException or FormatException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return MissingInstaller();
        }
    }

    private static bool HasLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static IResult UnavailableCatalog() => Results.Json(new { code = "client_release_invalid" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    private static IResult MissingInstaller() => Results.NotFound(new { code = "windows_release_unavailable" });
}
