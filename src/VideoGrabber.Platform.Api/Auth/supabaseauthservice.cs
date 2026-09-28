using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public sealed class SupabaseAuthService(
    IHttpClientFactory clients,
    IConfiguration configuration,
    IIdentityAccountResolver identities,
    SessionStore sessions,
    TimeProvider clock)
{
    private const int MaxTokenLength = 16 * 1024;
    private readonly Uri _baseUri = ReadBaseUri(configuration);
    private readonly string _publishableKey = ReadPublishableKey(configuration);
    private readonly Uri _redirectUri = ReadRedirectUri(configuration);

    public async Task<SupabaseBrowserAuthConfig> ReadBrowserConfigAsync(
        CancellationToken cancellationToken)
    {
        var email = true;
        var google = false;
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(_baseUri, "/auth/v1/settings"));
            request.Headers.TryAddWithoutValidation("apikey", _publishableKey);
            using var response = await clients.CreateClient("SupabaseAuth")
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(
                    await response.Content.ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false));
                var root = json.RootElement;
                email = root.TryGetProperty("disable_signup", out var disabled)
                    ? disabled.ValueKind != JsonValueKind.True
                    : true;
                if (root.TryGetProperty("external", out var external)
                    && external.ValueKind == JsonValueKind.Object)
                {
                    google = external.TryGetProperty("google", out var googleElement)
                        && googleElement.ValueKind == JsonValueKind.True;
                    if (external.TryGetProperty("email", out var emailElement))
                        email = email && emailElement.ValueKind == JsonValueKind.True;
                }
            }
        }
        catch (HttpRequestException) { }
        catch (JsonException) { }

        return new SupabaseBrowserAuthConfig(
            _baseUri.GetLeftPart(UriPartial.Authority),
            _publishableKey,
            email,
            google,
            _redirectUri);
    }

    public async Task<ApiSession> ExchangeAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)
            || accessToken.Length > MaxTokenLength
            || accessToken.Any(char.IsWhiteSpace))
            throw new UnauthorizedAccessException("supabase_token_invalid");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(_baseUri, "/auth/v1/user"));
        request.Headers.TryAddWithoutValidation("apikey", _publishableKey);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await clients.CreateClient("SupabaseAuth")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("supabase_token_invalid");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false));
        var identity = ParseVerifiedIdentity(
            json.RootElement,
            _baseUri,
            clock.GetUtcNow());
        var profile = await identities.ResolveAsync(identity, cancellationToken)
            .ConfigureAwait(false);
        return await sessions.IssueAsync(profile.AccountId, cancellationToken)
            .ConfigureAwait(false);
    }

    public static VerifiedIdentity ParseVerifiedIdentity(
        JsonElement root,
        Uri baseUri,
        DateTimeOffset now)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("id", out var idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idElement.GetString(), out var userId))
            throw new UnauthorizedAccessException("supabase_user_invalid");

        var provider = string.Empty;
        if (root.TryGetProperty("app_metadata", out var appMetadata)
            && appMetadata.ValueKind == JsonValueKind.Object
            && appMetadata.TryGetProperty("provider", out var providerElement)
            && providerElement.ValueKind == JsonValueKind.String)
            provider = providerElement.GetString() ?? string.Empty;

        if (provider is not ("google" or "email"))
            throw new UnauthorizedAccessException("supabase_provider_not_allowed");

        string? email = null;
        if (root.TryGetProperty("email", out var emailElement)
            && emailElement.ValueKind == JsonValueKind.String)
            email = emailElement.GetString()?.Trim().ToLowerInvariant();

        var confirmed = false;
        foreach (var propertyName in new[] { "email_confirmed_at", "confirmed_at" })
        {
            if (root.TryGetProperty(propertyName, out var confirmedElement)
                && confirmedElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(confirmedElement.GetString()))
            {
                confirmed = true;
                break;
            }
        }
        if (!confirmed || string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("supabase_email_not_confirmed");

        var issuer = new Uri(baseUri, "/auth/v1/").AbsoluteUri;
        return new VerifiedIdentity(
            issuer,
            provider,
            userId.ToString("D"),
            email,
            now,
            "supabase_auth")
        {
            AllowProviderCoalescing = true
        };
    }

    private static Uri ReadBaseUri(IConfiguration configuration)
    {
        var raw = configuration["VG_SUPABASE_URL"]
            ?? throw new InvalidOperationException("VG_SUPABASE_URL is required.");
        if (!Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("VG_SUPABASE_URL must be an absolute HTTPS URL.");
        return uri;
    }

    private static string ReadPublishableKey(IConfiguration configuration)
    {
        var value = configuration["VG_SUPABASE_PUBLISHABLE_KEY"];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("VG_SUPABASE_PUBLISHABLE_KEY is required.");
        return value.Trim();
    }

    private static Uri ReadRedirectUri(IConfiguration configuration)
    {
        var raw = configuration["VG_WEB_AUTH_RETURN_URI"];
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(
                "VG_WEB_AUTH_RETURN_URI must be an exact HTTPS URI.");
        return uri;
    }
}
