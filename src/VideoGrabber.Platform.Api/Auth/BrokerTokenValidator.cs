using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Auth;

public sealed record BrokerUserIdentity(
    string Provider, string Subject, string? Email, bool EmailVerified);
public sealed record BrokerUserInfo(
    string BrokerSubject, IReadOnlyList<BrokerUserIdentity> Identities);

public interface IBrokerSigningKeySource
{
    Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(
        BrokerPartitionOptions partition, CancellationToken cancellationToken);
}

public interface IBrokerUserInfoSource
{
    Task<BrokerUserInfo> GetAsync(BrokerPartitionOptions partition, string token,
        CancellationToken cancellationToken);
}

public interface IBrokerBindingStore
{
    Task FreezeAsync(string provider, string brokerSubject, string providerSubject,
        CancellationToken cancellationToken);
}
public sealed class HttpBrokerSigningKeySource(
    IHttpClientFactory clients, TimeProvider clock) : IBrokerSigningKeySource
{
    private readonly ConcurrentDictionary<string, CachedKeys> _cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(
        BrokerPartitionOptions partition, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_cache.TryGetValue(partition.Provider, out var cached) && cached.ExpiresAt > now)
            return cached.Keys;

        var uri = new Uri(partition.Issuer, ".well-known/jwks.json");
        using var response = await clients.CreateClient(nameof(HttpBrokerSigningKeySource))
            .GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var set = new JsonWebKeySet(json);
        var keys = set.Keys.Cast<SecurityKey>().ToArray();
        if (keys.Length == 0) throw new UnauthorizedAccessException("Broker JWKS contains no usable keys.");
        _cache[partition.Provider] = new CachedKeys(keys, now.AddMinutes(5));
        return keys;
    }

    private sealed record CachedKeys(IReadOnlyCollection<SecurityKey> Keys, DateTimeOffset ExpiresAt);
}
public sealed class HttpBrokerUserInfoSource(IHttpClientFactory clients) : IBrokerUserInfoSource
{
    public async Task<BrokerUserInfo> GetAsync(BrokerPartitionOptions partition, string token,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(partition.Issuer, "auth/v1/user"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await clients.CreateClient(nameof(HttpBrokerUserInfoSource))
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Broker user-info request was rejected.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = json.RootElement;
        var brokerSubject = RequiredText(root, "id");
        if (!root.TryGetProperty("identities", out var identitiesElement)
            || identitiesElement.ValueKind != JsonValueKind.Array)
            throw new UnauthorizedAccessException("Broker identities are missing.");
        var identities = identitiesElement.EnumerateArray().Select(ParseIdentity).ToArray();
        return new BrokerUserInfo(brokerSubject, identities);
    }

    private static BrokerUserIdentity ParseIdentity(JsonElement item)
    {
        var provider = RequiredText(item, "provider");
        var data = item.TryGetProperty("identity_data", out var identityData) ? identityData : item;
        var subject = OptionalText(data, "sub") ?? OptionalText(item, "id")
            ?? throw new UnauthorizedAccessException("Broker provider subject is missing.");
        var email = OptionalText(data, "email");
        var verified = data.TryGetProperty("email_verified", out var flag)
            && flag.ValueKind is JsonValueKind.True;
        return new(provider, subject, email, verified);
    }
    private static string RequiredText(JsonElement element, string name)
        => OptionalText(element, name)
            ?? throw new UnauthorizedAccessException($"Broker field {name} is missing.");

    private static string? OptionalText(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text ? text : null;
}

public sealed class BrokerTokenValidator(
    IReadOnlyDictionary<string, BrokerPartitionOptions> partitions,
    IBrokerSigningKeySource keys,
    IBrokerUserInfoSource users,
    IBrokerBindingStore bindings,
    TimeProvider clock) : IBrokerTokenValidator
{
    public async Task<VerifiedIdentity> ValidateAsync(
        string token, string expectedProvider, string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (!partitions.TryGetValue(expectedProvider, out var partition))
            throw new UnauthorizedAccessException("Unknown broker partition.");
        var signingKeys = await keys.GetKeysAsync(partition, cancellationToken).ConfigureAwait(false);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = partition.Issuer.AbsoluteUri,
            ValidateAudience = true, ValidAudience = partition.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKeys = signingKeys,
            RequireSignedTokens = true, RequireExpirationTime = true,
            ValidateLifetime = false, ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out var validated);
            if (validated is not JwtSecurityToken jwt || jwt.Header.Alg != SecurityAlgorithms.RsaSha256)
                throw new UnauthorizedAccessException("Broker signing algorithm is not allowed.");
            var now = clock.GetUtcNow();
            if (jwt.ValidTo == DateTime.MinValue || new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero) <= now)
                throw new UnauthorizedAccessException("Broker token is expired.");
            if (jwt.ValidFrom != DateTime.MinValue
                && new DateTimeOffset(jwt.ValidFrom, TimeSpan.Zero) > now.AddSeconds(30))
                throw new UnauthorizedAccessException("Broker token is not active.");
            var brokerSubject = Required(principal, JwtRegisteredClaimNames.Sub);
            var provider = Required(principal, "provider");
            var nonce = Required(principal, "nonce");
            if (!provider.Equals(expectedProvider, StringComparison.Ordinal)
                || !nonce.Equals(expectedNonce, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Broker identity binding mismatch.");

            var user = await users.GetAsync(partition, token, cancellationToken).ConfigureAwait(false);
            if (!user.BrokerSubject.Equals(brokerSubject, StringComparison.Ordinal)
                || user.Identities.Count != 1)
                throw new UnauthorizedAccessException("Broker identity set is ambiguous.");
            var identity = user.Identities[0];
            if (!identity.Provider.Equals(partition.Provider, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(identity.Subject))
                throw new UnauthorizedAccessException("Broker provider identity is invalid.");
            await bindings.FreezeAsync(partition.Provider, brokerSubject, identity.Subject, cancellationToken)
                .ConfigureAwait(false);
            return new VerifiedIdentity(partition.Issuer.AbsoluteUri, partition.Provider, identity.Subject,
                identity.EmailVerified ? identity.Email : null, now, "broker_rs256_userinfo");
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or InvalidDataException)
        {
            throw new UnauthorizedAccessException("Broker token validation failed.", ex);
        }
    }

    private static string Required(System.Security.Claims.ClaimsPrincipal principal, string type)
        => principal.FindFirst(type)?.Value is { Length: > 0 } value
            ? value : throw new UnauthorizedAccessException($"Broker claim {type} is missing.");
}