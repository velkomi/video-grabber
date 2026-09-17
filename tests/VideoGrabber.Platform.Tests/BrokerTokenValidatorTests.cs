using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using VideoGrabber.Platform.Api.Auth;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class BrokerTokenValidatorTests
{
    private static readonly Uri Issuer = new("https://google.issuer.test/");
    private const string Audience = "video-grabber-google";

    [Fact]
    public async Task Signed_expected_identity_is_accepted()
    {
        using var emulator = new BrokerEmulator();
        var clock = new AdjustableTimeProvider();
        var validator = Validator(emulator, clock);
        var token = emulator.Issue(Issuer.AbsoluteUri, Audience, "google", "subject-1",
            "nonce-1", clock.GetUtcNow().AddMinutes(5), "person@example.test");

        var identity = await validator.ValidateAsync(token, "google", "nonce-1", default);

        Assert.Equal("subject-1", identity.Subject);
        Assert.Equal("google", identity.Provider);
        Assert.Equal("person@example.test", identity.VerifiedEmail);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("nonce")]
    [InlineData("expired")]
    [InlineData("unsigned")]
    public async Task Invalid_assertions_are_rejected(string mode)
    {
        using var emulator = new BrokerEmulator();
        var clock = new AdjustableTimeProvider();
        var validator = Validator(emulator, clock);
        var issuer = mode == "issuer" ? "https://evil.example.test/" : Issuer.AbsoluteUri;
        var audience = mode == "audience" ? "wrong-aud" : Audience;
        var nonce = mode == "nonce" ? "wrong-nonce" : "nonce-1";
        var expires = mode == "expired" ? clock.GetUtcNow().AddMinutes(-1) : clock.GetUtcNow().AddMinutes(5);
        var token = mode == "unsigned"
            ? emulator.Unsigned(issuer, audience, "google", "subject-1", nonce)
            : emulator.Issue(issuer, audience, "google", "subject-1", nonce, expires);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            validator.ValidateAsync(token, "google", "nonce-1", default));
    }

    private static BrokerTokenValidator Validator(BrokerEmulator emulator, AdjustableTimeProvider clock)
    {
        var partitions = new Dictionary<string, BrokerPartitionOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["google"] = new("google", Issuer, Audience,
                new Uri("https://api.example.test/auth/google/callback"), "google")
        };
        return new BrokerTokenValidator(partitions, new StaticKeySource(emulator.PublicKey), emulator, new MemoryBindingStore(), clock);
    }

    [Fact]
    public void Duplicate_partition_issuer_is_rejected()
    {
        var values = new Dictionary<string,string?>
        {
            ["BrokerPartitions:google:Provider"]="google", ["BrokerPartitions:google:Issuer"]="https://same.example.test/",
            ["BrokerPartitions:google:Audience"]="a", ["BrokerPartitions:google:CallbackUri"]="https://api.example.test/g", ["BrokerPartitions:google:ProviderKey"]="google",
            ["BrokerPartitions:apple:Provider"]="apple", ["BrokerPartitions:apple:Issuer"]="https://same.example.test/",
            ["BrokerPartitions:apple:Audience"]="b", ["BrokerPartitions:apple:CallbackUri"]="https://api.example.test/a", ["BrokerPartitions:apple:ProviderKey"]="apple"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Throws<InvalidOperationException>(() => BrokerPartitionConfiguration.Load(configuration));
    }

    [Fact]
    public async Task Duplicate_broker_identities_are_rejected()
    {
        using var emulator = new BrokerEmulator();
        var clock = new AdjustableTimeProvider();
        var validator = Validator(emulator, clock);
        var token = emulator.IssueWithBrokerSubject(Issuer.AbsoluteUri, Audience, "google",
            "broker-one", "subject-1", "nonce-1", clock.GetUtcNow().AddMinutes(5), identityCount: 2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            validator.ValidateAsync(token, "google", "nonce-1", default));
    }

    [Fact]
    public async Task Unverified_email_is_not_promoted_to_verified_identity()
    {
        using var emulator = new BrokerEmulator();
        var clock = new AdjustableTimeProvider();
        var validator = Validator(emulator, clock);
        var token = emulator.IssueWithBrokerSubject(Issuer.AbsoluteUri, Audience, "google",
            "broker-one", "subject-1", "nonce-1", clock.GetUtcNow().AddMinutes(5),
            "person@example.test", emailVerified: false);
        var identity = await validator.ValidateAsync(token, "google", "nonce-1", default);
        Assert.Null(identity.VerifiedEmail);
    }

    [Fact]
    public async Task Frozen_broker_subject_cannot_change_provider_subject()
    {
        using var emulator = new BrokerEmulator();
        var clock = new AdjustableTimeProvider();
        var bindings = new MemoryBindingStore();
        var partitions = new Dictionary<string, BrokerPartitionOptions>(StringComparer.OrdinalIgnoreCase)
        { ["google"] = new("google", Issuer, Audience, new Uri("https://api.example.test/auth/google/callback"), "google") };
        var validator = new BrokerTokenValidator(partitions, new StaticKeySource(emulator.PublicKey), emulator, bindings, clock);
        var first = emulator.IssueWithBrokerSubject(Issuer.AbsoluteUri, Audience, "google", "broker-one", "subject-1", "nonce-1", clock.GetUtcNow().AddMinutes(5));
        await validator.ValidateAsync(first, "google", "nonce-1", default);
        var changed = emulator.IssueWithBrokerSubject(Issuer.AbsoluteUri, Audience, "google", "broker-one", "subject-2", "nonce-2", clock.GetUtcNow().AddMinutes(5));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => validator.ValidateAsync(changed, "google", "nonce-2", default));
    }

    private sealed class StaticKeySource(SecurityKey key) : IBrokerSigningKeySource
    {
        public Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(
            BrokerPartitionOptions partition, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<SecurityKey>>([key]);
    }

    private sealed class MemoryBindingStore : IBrokerBindingStore
    {
        private readonly Dictionary<(string Provider,string Broker),string> _values = [];
        public Task FreezeAsync(string provider, string brokerSubject, string providerSubject, CancellationToken cancellationToken)
        {
            var key=(provider,brokerSubject);
            if (_values.TryGetValue(key,out var frozen) && frozen != providerSubject)
                throw new UnauthorizedAccessException("binding changed");
            _values[key]=providerSubject;
            return Task.CompletedTask;
        }
    }
}
