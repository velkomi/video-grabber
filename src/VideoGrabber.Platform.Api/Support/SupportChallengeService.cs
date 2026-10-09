using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Altcha;
using VideoGrabber.Platform.Api.Auth;

namespace VideoGrabber.Platform.Api.Support;

public sealed record SupportChallengeClaim(string Key, DateTimeOffset ExpiresAt);

public sealed class SupportChallengeService(SessionJwtOptions session, TimeProvider clock)
{
    private readonly string _signingKey = Convert.ToBase64String(HMACSHA256.HashData(session.SigningKey,
        Encoding.UTF8.GetBytes("VideoGrabber.support.challenge-signature.v1")));
    private readonly string _keySignature = Convert.ToBase64String(HMACSHA256.HashData(session.SigningKey,
        Encoding.UTF8.GetBytes("VideoGrabber.support.challenge-key.v1")));

    public Challenge Create()
        => AltchaPow.CreateChallenge(new CreateChallengeOptions
        {
            Algorithm = "PBKDF2/SHA-256", Cost = 5000,
            Counter = RandomNumberGenerator.GetInt32(50, 100),
            ExpiresAt = clock.GetUtcNow().AddMinutes(5),
            HmacSignatureSecret = _signingKey, HmacKeySignatureSecret = _keySignature,
            Data = new Dictionary<string, object?> { ["scope"] = "videograbber-support", ["challengeId"] = Guid.NewGuid().ToString("N") }
        });

    public SupportChallengeClaim Verify(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 8192)
            throw new SupportRequestException(403, "support_verification_required");
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            var payload = JsonSerializer.Deserialize<Payload>(bytes, AltchaJson.SerializerOptions)
                ?? throw new JsonException();
            var parameters = payload.Challenge.Parameters;
            if (parameters.Algorithm != "PBKDF2/SHA-256" || parameters.Cost != 5000
                || parameters.KeyLength != 32 || parameters.MemoryCost is not null || parameters.Parallelism is not null)
                throw new SupportRequestException(403, "support_verification_required");
            var result = AltchaPow.VerifySolution(new VerifySolutionOptions
            {
                Challenge = payload.Challenge, Solution = payload.Solution,
                HmacSignatureSecret = _signingKey, HmacKeySignatureSecret = _keySignature
            });
            if (!result.Verified) throw new SupportRequestException(403, "support_verification_required");
            if (parameters.ExpiresAt is not { } expiresSeconds || !double.IsFinite(expiresSeconds))
                throw new SupportRequestException(403, "support_verification_required");
            var expires = DateTimeOffset.UnixEpoch.AddSeconds(expiresSeconds);
            if (expires <= clock.GetUtcNow() || expires > clock.GetUtcNow().AddMinutes(5).AddSeconds(5))
                throw new SupportRequestException(403, "support_verification_required");
            var data = JsonSerializer.SerializeToElement(parameters.Data);
            if (!data.TryGetProperty("scope", out var scope) || scope.GetString() != "videograbber-support"
                || !data.TryGetProperty("challengeId", out var challengeId)
                || !Guid.TryParseExact(challengeId.GetString(), "N", out var id))
                throw new SupportRequestException(403, "support_verification_required");
            return new SupportChallengeClaim(id.ToString("N"), expires);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or InvalidOperationException)
        { throw new SupportRequestException(403, "support_verification_required"); }
    }
}
