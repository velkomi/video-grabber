namespace VideoGrabber.Platform.Api.Auth;

public sealed record BrokerPartitionOptions(
    string Provider,
    Uri Issuer,
    string Audience,
    Uri CallbackUri,
    string ProviderKey);

public static class BrokerPartitionConfiguration
{
    public static IReadOnlyDictionary<string, BrokerPartitionOptions> Load(
        IConfiguration configuration)
    {
        var partitions = new Dictionary<string, BrokerPartitionOptions>(
            StringComparer.OrdinalIgnoreCase);
        var issuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in configuration.GetSection("BrokerPartitions").GetChildren())
        {
            var provider = section["Provider"] ?? section.Key;
            var issuer = RequireUri(section["Issuer"], "Issuer");
            if (!issuers.Add(issuer.AbsoluteUri))
                throw new InvalidOperationException("Duplicate broker issuer configuration.");
            var audience = section["Audience"]
                ?? throw new InvalidOperationException("Broker audience is required.");
            var callback = RequireUri(section["CallbackUri"], "CallbackUri");
            var providerKey = section["ProviderKey"]
                ?? throw new InvalidOperationException("Provider key is required.");
            if (!partitions.TryAdd(provider, new(
                    provider, issuer, audience, callback, providerKey)))
                throw new InvalidOperationException("Duplicate broker provider configuration.");
        }
        return partitions;
    }

    private static Uri RequireUri(string? value, string name)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : throw new InvalidOperationException($"Broker {name} must be an absolute HTTPS URI.");
}
