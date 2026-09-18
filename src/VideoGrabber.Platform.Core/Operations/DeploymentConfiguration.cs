using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoGrabber.Platform.Core.Operations;

public sealed record StageConfiguration(
    string EnvironmentName,
    string ApiImage,
    string WorkerImage,
    string PostgresImage,
    string CaddyImage,
    string BotApiImage,
    string PublicHttpsHost,
    string DatabaseBind,
    string BotApiBind,
    string AdminBind,
    string[] AllowedOrigins,
    string AuthIssuer,
    bool AuthDevelopmentIssuerEnabled,
    string SigningKeyFile,
    bool PaymentsLiveEnabled,
    string? LiveCatalogPath,
    string TelegramBotApiBaseUri,
    string[] CallbackUrls);

public static partial class DeploymentConfiguration
{
    [GeneratedRegex(@"^.+@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedImageRegex();

    [GeneratedRegex(@"^vg-stage-[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex StageNameRegex();

    public static bool IsPinnedImage(string? image)
        => !string.IsNullOrWhiteSpace(image)
           && PinnedImageRegex().IsMatch(image);

    public static bool ValidateStageName(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && StageNameRegex().IsMatch(name);

    public static IReadOnlyList<string> Validate(StageConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = new SortedSet<string>(StringComparer.Ordinal);

        if (!ValidateStageName(config.EnvironmentName))
            errors.Add(nameof(config.EnvironmentName));

        foreach (var pair in new[]
        {
            (nameof(config.ApiImage), config.ApiImage),
            (nameof(config.WorkerImage), config.WorkerImage),
            (nameof(config.PostgresImage), config.PostgresImage),
            (nameof(config.CaddyImage), config.CaddyImage),
            (nameof(config.BotApiImage), config.BotApiImage)
        })
            if (!IsPinnedImage(pair.Item2)) errors.Add(pair.Item1);

        if (!IsLoopbackOrPrivateBind(config.DatabaseBind))
            errors.Add(nameof(config.DatabaseBind));
        if (!IsLoopbackOrPrivateBind(config.BotApiBind))
            errors.Add(nameof(config.BotApiBind));
        if (!IsLoopbackOrPrivateBind(config.AdminBind))
            errors.Add(nameof(config.AdminBind));

        if (config.AllowedOrigins is null
            || config.AllowedOrigins.Length == 0
            || config.AllowedOrigins.Any(x =>
                string.IsNullOrWhiteSpace(x)
                || x.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(x, UriKind.Absolute, out var origin)
                || origin.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(origin.UserInfo)))
            errors.Add(nameof(config.AllowedOrigins));

        if (!Uri.TryCreate(config.AuthIssuer, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(issuer.UserInfo))
            errors.Add(nameof(config.AuthIssuer));
        if (config.AuthDevelopmentIssuerEnabled)
            errors.Add(nameof(config.AuthDevelopmentIssuerEnabled));

        if (string.IsNullOrWhiteSpace(config.SigningKeyFile)
            || !Path.IsPathFullyQualified(config.SigningKeyFile))
            errors.Add(nameof(config.SigningKeyFile));

        if (config.PaymentsLiveEnabled
            && string.IsNullOrWhiteSpace(config.LiveCatalogPath))
            errors.Add(nameof(config.LiveCatalogPath));

        if (!Uri.TryCreate(config.TelegramBotApiBaseUri, UriKind.Absolute, out var botApi)
            || botApi.Scheme is not ("http" or "https")
            || IsPublicHost(botApi.Host))
            errors.Add(nameof(config.TelegramBotApiBaseUri));

        if (config.CallbackUrls is null
            || config.CallbackUrls.Length == 0
            || config.CallbackUrls.Any(url =>
                !Uri.TryCreate(url, UriKind.Absolute, out var callback)
                || callback.Scheme != Uri.UriSchemeHttps
                || !string.Equals(
                    callback.Host,
                    config.PublicHttpsHost,
                    StringComparison.OrdinalIgnoreCase)))
            errors.Add(nameof(config.CallbackUrls));

        if (string.IsNullOrWhiteSpace(config.PublicHttpsHost)
            || config.PublicHttpsHost.Contains('*', StringComparison.Ordinal)
            || config.PublicHttpsHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || config.PublicHttpsHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || config.PublicHttpsHost.Contains("prod", StringComparison.OrdinalIgnoreCase))
            errors.Add(nameof(config.PublicHttpsHost));

        return errors.ToArray();
    }

    public static StageConfiguration Load(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException("Stage config was not found.", full);
        var json = File.ReadAllText(full);
        return JsonSerializer.Deserialize<StageConfiguration>(
                   json,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true,
                       AllowTrailingCommas = false,
                       ReadCommentHandling = JsonCommentHandling.Disallow,
                       MaxDepth = 16
                   })
               ?? throw new InvalidDataException("Stage config is invalid.");
    }

    private static bool IsLoopbackOrPrivateBind(string? bind)
    {
        if (string.IsNullOrWhiteSpace(bind)) return false;
        var host = bind.Trim();
        if (host is "127.0.0.1" or "::1" or "localhost") return true;
        if (host.StartsWith("10.", StringComparison.Ordinal)) return true;
        if (host.StartsWith("192.168.", StringComparison.Ordinal)) return true;
        if (host.StartsWith("172.", StringComparison.Ordinal)
            && host.Split('.').Length >= 2
            && int.TryParse(host.Split('.')[1], out var second)
            && second is >= 16 and <= 31)
            return true;
        return false;
    }

    private static bool IsPublicHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host == "127.0.0.1"
            || host == "::1")
            return false;
        if (host.StartsWith("10.", StringComparison.Ordinal)
            || host.StartsWith("192.168.", StringComparison.Ordinal))
            return false;
        if (host.StartsWith("172.", StringComparison.Ordinal)
            && host.Split('.').Length >= 2
            && int.TryParse(host.Split('.')[1], out var second)
            && second is >= 16 and <= 31)
            return false;
        return true;
    }
}
