using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using VideoGrabber.Platform.Api.Auth;

namespace VideoGrabber.Platform.Tests;

public sealed class BrokerEmulator : IDisposable,
    IBrokerCodeExchange, IBrokerSigningKeySource, IBrokerUserInfoSource
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RsaSecurityKey _key;
    private readonly ConcurrentDictionary<string, IssuedCode> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, BrokerUserInfo> _users = new(StringComparer.Ordinal);

    public BrokerEmulator()
    {
        _key = new RsaSecurityKey(_rsa) { KeyId = "test-key" };
    }

    public SecurityKey PublicKey
        => new RsaSecurityKey(_rsa.ExportParameters(false)) { KeyId = _key.KeyId };

    public string RegisterCode(BrokerPartitionOptions partition, string subject,
        string nonce, DateTimeOffset expiresAt, string? email = null)
        => RegisterCodeCustom(partition, "broker-" + subject, subject, nonce, expiresAt, email);

    public string RegisterCodeCustom(BrokerPartitionOptions partition, string brokerSubject,
        string providerSubject, string nonce, DateTimeOffset expiresAt, string? email = null,
        bool emailVerified = true, int identityCount = 1)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var token = IssueWithBrokerSubject(partition.Issuer.AbsoluteUri, partition.Audience,
            partition.Provider, brokerSubject, providerSubject, nonce, expiresAt, email, emailVerified, identityCount);
        _codes[code] = new IssuedCode(partition.Provider, token);
        return code;
    }
    public Task<string> ExchangeAsync(BrokerPartitionOptions partition, string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_codes.TryRemove(code, out var issued)
            || !issued.Provider.Equals(partition.Provider, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Unknown or mismatched broker code.");
        return Task.FromResult(issued.Token);
    }

    public Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(
        BrokerPartitionOptions partition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<SecurityKey>>([PublicKey]);
    }

    public Task<BrokerUserInfo> GetAsync(BrokerPartitionOptions partition, string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_users.TryGetValue(token, out var user))
            throw new UnauthorizedAccessException("Unknown broker token.");
        return Task.FromResult(user);
    }

    public string Issue(string issuer, string audience, string provider, string subject,
        string nonce, DateTimeOffset expiresAt, string? email = null)
        => IssueWithBrokerSubject(issuer, audience, provider, "broker-" + subject,
            subject, nonce, expiresAt, email, emailVerified: email is not null, identityCount: 1);
    public string IssueWithBrokerSubject(string issuer, string audience, string provider,
        string brokerSubject, string providerSubject, string nonce, DateTimeOffset expiresAt,
        string? email = null, bool emailVerified = true, int identityCount = 1)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, brokerSubject),
            new("provider", provider),
            new("nonce", nonce)
        };
        var token = new JwtSecurityToken(
            issuer, audience, claims,
            notBefore: expiresAt.AddMinutes(-10).UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        var identities = Enumerable.Range(0, identityCount)
            .Select(index => new BrokerUserIdentity(
                provider,
                index == 0 ? providerSubject : providerSubject + "-duplicate-" + index,
                email,
                emailVerified))
            .ToArray();
        _users[encoded] = new BrokerUserInfo(brokerSubject, identities);
        return encoded;
    }

    public string Unsigned(string issuer, string audience, string provider,
        string subject, string nonce)
    {
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "broker-" + subject),
             new Claim("provider", provider), new Claim("nonce", nonce)],
            expires: DateTime.UtcNow.AddMinutes(5));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public void Dispose() => _rsa.Dispose();

    private sealed record IssuedCode(string Provider, string Token);
}