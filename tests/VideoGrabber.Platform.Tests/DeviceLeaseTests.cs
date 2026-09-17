using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using VideoGrabber.Platform.Api.Access;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class DeviceLeaseTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(72)]
    public void Lease_maximum_is_a_hard_boundary(int hours)
    {
        var now = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var end = OfflineLeaseService.BoundExpiry(now, now.AddDays(7), hours == 72);
        Assert.Equal(now.AddHours(hours), end);
        Assert.Equal(now.AddHours(1),
            OfflineLeaseService.BoundExpiry(now, now.AddHours(1), hours == 72));
    }

    [Fact]
    public async Task Guest_has_one_windows_slot_and_replay_returns_same_device()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "device-guest");
        using var key = NewKey();
        var registration = Registration("Primary", key, Guid.NewGuid());
        var first = await account.Client.PostAsJsonAsync("/v1/devices", registration);
        var replay = await account.Client.PostAsJsonAsync("/v1/devices", registration);
        first.EnsureSuccessStatusCode(); replay.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadFromJsonAsync<DeviceReceipt>(),
            await replay.Content.ReadFromJsonAsync<DeviceReceipt>());
        using var secondKey = NewKey();
        var second = await account.Client.PostAsJsonAsync("/v1/devices",
            Registration("Second", secondKey, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
    [Fact]
    public async Task Owner_has_three_windows_slots_under_concurrency()
    {
        await using var f = await ApiFixture.StartAsync();
        var owner = await f.AdminAsync();
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var key = NewKey();
            return await owner.PostAsJsonAsync("/v1/devices",
                Registration("Owner-" + i, key, Guid.NewGuid()));
        });
        var responses = await Task.WhenAll(tasks);
        Assert.Equal(3, responses.Count(response => response.IsSuccessStatusCode));
        Assert.All(responses.Where(response => !response.IsSuccessStatusCode),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    [Fact]
    public async Task Non_windows_device_registration_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "device-platform");
        using var key = NewKey();
        var request = Registration("Telegram", key, Guid.NewGuid()) with { Platform = "telegram" };
        var response = await account.Client.PostAsJsonAsync("/v1/devices", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_only_account_cannot_receive_offline_lease()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "credit-only-lease");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 2, null, "credits", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        var lease = await IssueLeaseAsync(account.Client, device.DeviceId, key);
        Assert.Equal(HttpStatusCode.Forbidden, lease.StatusCode);
    }
    [Fact]
    public async Task Timed_account_receives_signed_minimal_lease_and_wrong_key_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "timed-lease");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "time", 2, 0, null, "time", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        using var wrong = NewKey();
        var wrongLease = await IssueLeaseAsync(account.Client, device.DeviceId, wrong);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongLease.StatusCode);
        var response = await IssueLeaseAsync(account.Client, device.DeviceId, key);
        response.EnsureSuccessStatusCode();
        var lease = await response.Content.ReadFromJsonAsync<SignedOfflineLease>();
        Assert.NotNull(lease);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(lease!.Token);
        Assert.Equal("ES256", jwt.Header.Alg);
        Assert.Equal(lease.KeyId, jwt.Header.Kid);
        Assert.DoesNotContain(jwt.Claims, claim =>
            claim.Type.Contains("remaining", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("credit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(jwt.Claims, claim => claim.Type == "device_id" && claim.Value == device.DeviceId.ToString("D"));
    }

    [Fact]
    public async Task Revoked_device_cannot_refresh_lease()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "revoked-lease");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "time", 1, 0, null, "time", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        (await account.Client.PostAsync($"/v1/devices/{device.DeviceId}/revoke", null)).EnsureSuccessStatusCode();
        var challenge = await account.Client.PostAsync($"/v1/devices/{device.DeviceId}/challenge", null);
        Assert.Equal(HttpStatusCode.NotFound, challenge.StatusCode);
    }
    [Fact]
    public async Task Registered_desktop_and_server_share_the_same_last_credit()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "mixed-last-credit");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "credits", 0, 1, null, "mixed", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        var tasks = Enumerable.Range(0, 100).Select(i => account.Client.PostAsJsonAsync(
            "/v1/reservations", new ReservationRequest(Guid.NewGuid(), i.ToString("D64"),
                "download", i % 2 == 0 ? "server_worker" : "desktop_worker",
                i % 2 == 0 ? null : device.DeviceId)));
        var responses = await Task.WhenAll(tasks);
        Assert.Single(responses, response => response.IsSuccessStatusCode);
    }


    [Fact]
    public async Task Guest_concurrent_registration_has_exactly_one_winner()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "device-guest-race");
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var key = NewKey();
            return await account.Client.PostAsJsonAsync("/v1/devices",
                Registration("Guest-" + i, key, Guid.NewGuid()));
        });
        var responses = await Task.WhenAll(tasks);
        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.All(responses.Where(response => !response.IsSuccessStatusCode),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    [Fact]
    public async Task Device_nonce_is_single_use_and_expires_after_sixty_seconds()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "device-nonce");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "time", 1, 0, null, "nonce", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        var challenge = await CreateChallengeAsync(account.Client, device.DeviceId);
        var proof = Sign(challenge, key);
        (await account.Client.PostAsJsonAsync($"/v1/devices/{device.DeviceId}/lease", proof))
            .EnsureSuccessStatusCode();
        var replay = await account.Client.PostAsJsonAsync($"/v1/devices/{device.DeviceId}/lease", proof);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var expiring = await CreateChallengeAsync(account.Client, device.DeviceId);
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        var expired = await account.Client.PostAsJsonAsync($"/v1/devices/{device.DeviceId}/lease",
            Sign(expiring, key));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }


    [Fact]
    public async Task Lease_verifies_against_public_key_and_tampering_is_rejected()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync("telegram", "lease-verify");
        var admin = await f.AdminAsync();
        (await admin.PostAsJsonAsync("/v1/admin/grants",
            new GrantRequest(account.Id, "time", 1, 0, null, "verify", Guid.NewGuid())))
            .EnsureSuccessStatusCode();
        using var key = NewKey();
        var device = await RegisterAsync(account.Client, Registration("PC", key, Guid.NewGuid()));
        var response = await IssueLeaseAsync(account.Client, device.DeviceId, key);
        response.EnsureSuccessStatusCode();
        var lease = (await response.Content.ReadFromJsonAsync<SignedOfflineLease>())!;
        var keys = (await account.Client.GetFromJsonAsync<LeasePublicKey[]>("/v1/lease-keys"))!;

        var principal = ValidateLease(lease.Token, keys);
        Assert.Equal(account.Id.ToString("D"), principal.FindFirst("account_id")?.Value);
        Assert.Equal(device.DeviceId.ToString("D"), principal.FindFirst("device_id")?.Value);

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => ValidateLease(lease.Token, []));
        var parts = lease.Token.Split('.');
        parts[2] = parts[2][..^1] + (parts[2][^1] == 'A' ? "B" : "A");
        Assert.ThrowsAny<SecurityTokenException>(() => ValidateLease(string.Join('.', parts), keys));
        var payload = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        payload = payload.Replace("\"lease_schema\":\"1\"", "\"lease_schema\":\"2\"", StringComparison.Ordinal);
        parts[1] = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(payload));
        Assert.ThrowsAny<SecurityTokenException>(() => ValidateLease(string.Join('.', parts), keys));
    }


    private static async Task<DeviceChallenge> CreateChallengeAsync(HttpClient client, Guid deviceId)
    {
        var response = await client.PostAsync($"/v1/devices/{deviceId}/challenge", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceChallenge>())!;
    }

    private static DeviceLeaseProof Sign(DeviceChallenge challenge, ECDsa key)
    {
        var signature = key.SignData(System.Text.Encoding.UTF8.GetBytes(challenge.Nonce),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new DeviceLeaseProof(challenge.Nonce, Convert.ToBase64String(signature));
    }

    private static System.Security.Claims.ClaimsPrincipal ValidateLease(
        string token, IReadOnlyList<LeasePublicKey> keys)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        if (!string.Equals(jwt.Header.Alg, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal))
            throw new SecurityTokenInvalidAlgorithmException();
        if (jwt.Claims.FirstOrDefault(c => c.Type == "lease_schema")?.Value != "1")
            throw new SecurityTokenException("Unsupported lease schema.");
        var published = keys.SingleOrDefault(k => k.KeyId == jwt.Header.Kid)
            ?? throw new SecurityTokenSignatureKeyNotFoundException();
        if (!string.Equals(published.Algorithm, "ES256", StringComparison.Ordinal))
            throw new SecurityTokenInvalidAlgorithmException();
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64UrlEncoder.DecodeBytes(published.X), Y = Base64UrlEncoder.DecodeBytes(published.Y) }
        };
        using var ecdsa = ECDsa.Create(parameters);
        var signingKey = new ECDsaSecurityKey(ecdsa) { KeyId = published.KeyId };
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "videograbber-licensing",
            ValidateAudience = true, ValidAudience = "videograbber-managed",
            ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256]
        }, out _);
    }

    private static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static DeviceRegistration Registration(string name, ECDsa key, Guid idempotencyKey)
        => new(name, "windows", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), idempotencyKey);

    private static async Task<DeviceReceipt> RegisterAsync(HttpClient client, DeviceRegistration request)
    {
        var response = await client.PostAsJsonAsync("/v1/devices", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceReceipt>())!;
    }

    private static async Task<HttpResponseMessage> IssueLeaseAsync(
        HttpClient client,
        Guid deviceId,
        ECDsa key)
    {
        var challengeResponse = await client.PostAsync($"/v1/devices/{deviceId}/challenge", null);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<DeviceChallenge>())!;
        var signature = key.SignData(System.Text.Encoding.UTF8.GetBytes(challenge.Nonce),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return await client.PostAsJsonAsync($"/v1/devices/{deviceId}/lease",
            new DeviceLeaseProof(challenge.Nonce, Convert.ToBase64String(signature)));
    }
}