using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Infrastructure.Licensing;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Tests;

public sealed class ManagedSessionTests
{
    [Fact]
    public async Task Session_store_roundtrips_with_current_user_protection_and_no_plaintext_backup()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            var store = new WindowsSessionStore(root, "refresh.bin");
            var secret = Encoding.UTF8.GetBytes("refresh-secret-sentinel-42");
            await store.SaveAsync(secret, CancellationToken.None);
            Assert.Equal(secret, await store.ReadAsync(CancellationToken.None));
            var disk = await File.ReadAllBytesAsync(Path.Combine(root, "refresh.bin"));
            Assert.DoesNotContain("refresh-secret-sentinel-42", Encoding.UTF8.GetString(disk));

            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
            await store.DeleteAsync(CancellationToken.None);
            Assert.Null(await store.ReadAsync(CancellationToken.None));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Protected_refresh_device_and_lease_files_are_independent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            var refresh = new WindowsSessionStore(root, "refresh.bin");
            var device = new WindowsSessionStore(root, "device-key.bin");
            var lease = new WindowsSessionStore(root, "lease.bin");
            await refresh.SaveAsync(Encoding.UTF8.GetBytes("refresh"), CancellationToken.None);
            await device.SaveAsync(Encoding.UTF8.GetBytes("private-device-key"), CancellationToken.None);
            await lease.SaveAsync(Encoding.UTF8.GetBytes("signed-lease"), CancellationToken.None);

            await refresh.DeleteAsync(CancellationToken.None);

            Assert.Null(await refresh.ReadAsync(CancellationToken.None));
            Assert.Equal("private-device-key", Encoding.UTF8.GetString((await device.ReadAsync(CancellationToken.None))!));
            Assert.Equal("signed-lease", Encoding.UTF8.GetString((await lease.ReadAsync(CancellationToken.None))!));
        }
        finally { SafeDelete(root); }
    }
    [Fact]
    public async Task Offline_cache_allows_valid_time_lease_but_never_invents_credit_access()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var account = Guid.NewGuid(); var device = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);
            var cache = new OfflineAccessCache(new WindowsSessionStore(root, "lease.bin"), account, device);
            await cache.SaveAsync(SignLease(key, "lease-key", account, device, now.AddMinutes(-1), now.AddHours(1)),
                new Dictionary<string, string> { ["lease-key"] = key.ExportSubjectPublicKeyInfoPem() }, now,
                CancellationToken.None);
            await cache.LoadAsync(CancellationToken.None);

            Assert.True(cache.TryAuthorize(new ManagedOperation(Guid.NewGuid(), "hash", "edit", "desktop_worker", device),
                now.AddMinutes(1), out var editPermit));
            Assert.True(editPermit!.Offline);
            Assert.Null(editPermit.ReservationId);
            Assert.True(cache.TryAuthorize(new ManagedOperation(Guid.NewGuid(), "hash", "direct_download", "desktop_worker", device),
                now.AddMinutes(1), out var downloadPermit));
            Assert.True(downloadPermit!.Offline);

            var empty = new OfflineAccessCache(new WindowsSessionStore(root, "empty.bin"), account, device);
            Assert.False(empty.TryAuthorize(new ManagedOperation(Guid.NewGuid(), "hash", "direct_download", "desktop_worker", device),
                now, out _));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Offline_cache_rejects_clock_rollback_after_reload()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var account = Guid.NewGuid(); var device = Guid.NewGuid();
            var serverUtc = new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);
            var store = new WindowsSessionStore(root, "lease.bin");
            var cache = new OfflineAccessCache(store, account, device);
            await cache.SaveAsync(SignLease(key, "lease-key", account, device, serverUtc.AddMinutes(-1), serverUtc.AddHours(1)),
                new Dictionary<string, string> { ["lease-key"] = key.ExportSubjectPublicKeyInfoPem() }, serverUtc,
                CancellationToken.None);
            var restored = new OfflineAccessCache(store, account, device);
            await restored.LoadAsync(CancellationToken.None);
            Assert.False(restored.TryAuthorize(new ManagedOperation(Guid.NewGuid(), "hash", "edit", "desktop_worker", device),
                serverUtc.AddMinutes(-3), out _));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task System_browser_sign_in_uses_one_time_web_handoff_and_exact_loopback_callback()
    {
        var handler = new BrowserFlowHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://licensing.example.test/") };
        var signIn = new SystemBrowserSignIn(http, "google", async (verification, cancellationToken) =>
        {
            Assert.Equal("https", verification.Scheme);
            Assert.Equal("/web/", verification.AbsolutePath);
            Assert.Equal("state-123", Query(verification, "desktop_state"));
            Assert.Equal(
                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                Query(verification, "desktop_flow"));

            var callback = handler.ReturnUri ?? throw new InvalidOperationException("Return URI was not captured.");
            Assert.Equal("http", callback.Scheme);
            Assert.Equal("127.0.0.1", callback.Host);
            Assert.Equal("/videograbber-auth/callback", callback.AbsolutePath);

            var callbackUri = new UriBuilder(callback)
            {
                Query = "code=handoff-code&state=state-123"
            }.Uri;
            using var callbackClient = new HttpClient();
            using var response = await callbackClient.GetAsync(callbackUri, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }, TimeSpan.FromSeconds(5));

        var session = await signIn.SignInAsync(CancellationToken.None);

        Assert.Equal("access-token", session.AccessToken);
        Assert.Equal("refresh-token", session.RefreshToken);
        Assert.True(handler.StartSeen);
        Assert.True(handler.ConsumeSeen);
    }

    [Fact]
    public async Task Licensing_client_uses_only_signed_time_lease_when_network_is_unreachable()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var account = Guid.NewGuid(); var device = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);
            var cache = new OfflineAccessCache(new WindowsSessionStore(root, "lease.bin"), account, device);
            await cache.SaveAsync(SignLease(key, "lease-key", account, device, now.AddMinutes(-1), now.AddHours(1)),
                new Dictionary<string, string> { ["lease-key"] = key.ExportSubjectPublicKeyInfoPem() }, now,
                CancellationToken.None);
            using var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://licensing.example.test/") };
            var client = new LicensingApiClient(http, () => "access-token", () => device, cache, () => now.AddMinutes(1));

            var permit = await client.AuthorizeAsync(
                new ManagedOperation(Guid.NewGuid(), "hash", "direct_download", "desktop_worker", device),
                CancellationToken.None);

            Assert.True(permit.Offline);
            Assert.Null(permit.ReservationId);
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Licensing_client_never_overrides_server_denial_with_offline_cache()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var account = Guid.NewGuid(); var device = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);
            var cache = new OfflineAccessCache(new WindowsSessionStore(root, "lease.bin"), account, device);
            await cache.SaveAsync(SignLease(key, "lease-key", account, device, now.AddMinutes(-1), now.AddHours(1)),
                new Dictionary<string, string> { ["lease-key"] = key.ExportSubjectPublicKeyInfoPem() }, now,
                CancellationToken.None);
            using var http = new HttpClient(new ConflictHandler()) { BaseAddress = new Uri("https://licensing.example.test/") };
            var client = new LicensingApiClient(http, () => "access-token", () => device, cache, () => now.AddMinutes(1));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.AuthorizeAsync(
                new ManagedOperation(Guid.NewGuid(), "hash", "direct_download", "desktop_worker", device),
                CancellationToken.None));
        }
        finally { SafeDelete(root); }
    }

    [Fact]
    public async Task Refresh_rotates_protected_secret_and_logout_removes_only_session_file()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = TempRoot();
        try
        {
            var store = new WindowsSessionStore(root, "refresh.bin");
            await store.SaveAsync(Encoding.UTF8.GetBytes("old-refresh"), CancellationToken.None);
            var unrelated = Path.Combine(root, "user-media-marker.txt");
            await File.WriteAllTextAsync(unrelated, "keep");
            using var http = new HttpClient(new SessionLifecycleHandler()) { BaseAddress = new Uri("https://licensing.example.test/") };
            var signIn = new SystemBrowserSignIn(http, "google", (_, _) => Task.CompletedTask,
                TimeSpan.FromSeconds(5), store);

            var refreshed = await signIn.RefreshAsync(CancellationToken.None);
            Assert.Equal("new-access", refreshed.AccessToken);
            Assert.Equal("rotated-refresh", Encoding.UTF8.GetString((await store.ReadAsync(CancellationToken.None))!));

            await signIn.SignOutAsync(CancellationToken.None);
            Assert.Null(await store.ReadAsync(CancellationToken.None));
            Assert.True(File.Exists(unrelated));
        }
        finally { SafeDelete(root); }
    }

    private sealed class SessionLifecycleHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/v1/auth/refresh")
            {
                var body = await request.Content!.ReadFromJsonAsync<RefreshSession>(cancellationToken: cancellationToken);
                Assert.Equal("old-refresh", body!.RefreshToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ApiSession("new-access", "rotated-refresh", DateTimeOffset.UtcNow.AddMinutes(5)))
                };
            }
            if (request.RequestUri.AbsolutePath == "/v1/auth/logout")
            {
                var body = await request.Content!.ReadFromJsonAsync<RefreshSession>(cancellationToken: cancellationToken);
                Assert.Equal("rotated-refresh", body!.RefreshToken);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("offline");
    }

    private sealed class ConflictHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
    }
    private sealed class BrowserFlowHandler : HttpMessageHandler
    {
        public Uri? ReturnUri { get; private set; }
        public bool StartSeen { get; private set; }
        public bool ConsumeSeen { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/v1/auth/desktop/start")
            {
                var begin = await request.Content!.ReadFromJsonAsync<DesktopSignInStartRequest>(
                    cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException();
                ReturnUri = begin.ReturnUri;
                StartSeen = true;
                return Json(
                    HttpStatusCode.OK,
                    new DesktopSignInStart(
                        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                        new Uri(
                            "https://licensing.example.test/web/?" +
                            "desktop_flow=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&" +
                            "desktop_state=state-123"),
                        "state-123",
                        DateTimeOffset.UtcNow.AddMinutes(5)));
            }
            if (request.RequestUri.AbsolutePath == "/v1/auth/desktop/consume")
            {
                var complete = await request.Content!.ReadFromJsonAsync<DesktopSignInConsumeRequest>(
                    cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException();
                Assert.Equal(
                    Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    complete.FlowId);
                Assert.Equal("state-123", complete.State);
                Assert.Equal("handoff-code", complete.Code);
                ConsumeSeen = true;
                return Json(
                    HttpStatusCode.OK,
                    new ApiSession(
                        "access-token",
                        "refresh-token",
                        DateTimeOffset.UtcNow.AddMinutes(5)));
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, T value)
            => new(status) { Content = JsonContent.Create(value) };
    }

    private static SignedOfflineLease SignLease(ECDsa key, string kid, Guid account, Guid device,
        DateTimeOffset issued, DateTimeOffset expires)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid, typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["iss"] = "videograbber-licensing", ["aud"] = "videograbber-managed",
            ["sub"] = account.ToString("D"), ["jti"] = Guid.NewGuid().ToString("D"),
            ["account_id"] = account.ToString("D"), ["device_id"] = device.ToString("D"),
            ["lease_version"] = "1", ["role_class"] = "guest", ["lease_schema"] = "1",
            ["feature"] = new[] { "download", "edit" }, ["iat"] = issued.ToUnixTimeSeconds(),
            ["nbf"] = issued.ToUnixTimeSeconds(), ["exp"] = expires.ToUnixTimeSeconds()
        }));
        var input = Encoding.ASCII.GetBytes(header + "." + payload);
        var signature = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new SignedOfflineLease(header + "." + payload + "." + Base64Url(signature), kid);
    }

    private static string Query(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name) return Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : "");
        }
        throw new KeyNotFoundException(name);
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "vg-managed-session-" + Guid.NewGuid().ToString("N"));
    private static void SafeDelete(string root) { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
}