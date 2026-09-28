using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class SystemBrowserSignIn
{
    private const string CallbackPath = "/videograbber-auth/callback";
    private readonly HttpClient _http;
    private readonly string _provider;
    private readonly Func<Uri, CancellationToken, Task> _launch;
    private readonly TimeSpan _timeout;
    private readonly WindowsSessionStore? _sessionStore;

    public SystemBrowserSignIn(
        HttpClient http,
        string provider,
        Func<Uri, CancellationToken, Task>? launch = null,
        TimeSpan? timeout = null,
        WindowsSessionStore? sessionStore = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _provider = string.IsNullOrWhiteSpace(provider) ? throw new ArgumentException("Provider is required.", nameof(provider)) : provider;
        _launch = launch ?? LaunchSystemBrowserAsync;
        _timeout = timeout ?? TimeSpan.FromMinutes(5);
        _sessionStore = sessionStore;
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<ApiSession> SignInAsync(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var returnUri = new Uri($"http://127.0.0.1:{port}{CallbackPath}");

        using var startResponse = await _http.PostAsJsonAsync(
            "/v1/auth/desktop/start",
            new DesktopSignInStartRequest(returnUri),
            cancellationToken).ConfigureAwait(false);
        if (!startResponse.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("managed_sign_in_start_failed");
        var started = await startResponse.Content.ReadFromJsonAsync<DesktopSignInStart>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Desktop sign-in start response was empty.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var callbackTask = ReceiveCallbackAsync(listener, timeout.Token);
        await _launch(started.VerificationUri, timeout.Token).ConfigureAwait(false);
        var callback = await callbackTask.ConfigureAwait(false);
        if (!FixedEquals(callback.State, started.State))
            throw new UnauthorizedAccessException("managed_sign_in_state_mismatch");

        using var complete = await _http.PostAsJsonAsync(
            "/v1/auth/desktop/consume",
            new DesktopSignInConsumeRequest(
                started.FlowId,
                callback.Code,
                callback.State),
            cancellationToken).ConfigureAwait(false);
        if (!complete.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("managed_sign_in_complete_failed");
        var session = await complete.Content.ReadFromJsonAsync<ApiSession>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Sign-in completion response was empty.");
        await SaveRefreshAsync(session.RefreshToken, cancellationToken)
            .ConfigureAwait(false);
        return session;
    }

    public async Task LinkAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        if (challengeId == Guid.Empty) throw new ArgumentException("Link challenge is required.", nameof(challengeId));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var returnUri = new Uri($"http://127.0.0.1:{port}{CallbackPath}");
        var verifier = RandomToken();
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        using var startResponse = await _http.PostAsJsonAsync(
            "/v1/identities/link/browser/start",
            new BeginIdentityLink(challengeId, _provider, returnUri, challenge),
            cancellationToken).ConfigureAwait(false);
        if (!startResponse.IsSuccessStatusCode)
            throw new HttpRequestException("Managed identity link start failed.", null, startResponse.StatusCode);
        var started = await startResponse.Content.ReadFromJsonAsync<SignInStart>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Identity link start response was empty.");
        var expectedState = QueryValue(started.AuthorizationUri, "state");
        if (string.IsNullOrWhiteSpace(expectedState)) throw new InvalidDataException("Identity link state was missing.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var callbackTask = ReceiveCallbackAsync(listener, timeout.Token);
        await _launch(started.AuthorizationUri, timeout.Token).ConfigureAwait(false);
        var callback = await callbackTask.ConfigureAwait(false);
        if (!FixedEquals(callback.State, expectedState))
            throw new UnauthorizedAccessException("managed_identity_link_state_mismatch");

        using var complete = await _http.PostAsJsonAsync(
            "/v1/identities/link/browser/complete",
            new CompleteIdentityLink(challengeId, started.FlowId, callback.Code, callback.State, verifier),
            cancellationToken).ConfigureAwait(false);
        if (!complete.IsSuccessStatusCode)
            throw new HttpRequestException("Managed identity link failed.", null, complete.StatusCode);
    }
    public async Task<ApiSession> RefreshAsync(CancellationToken cancellationToken)
    {
        if (_sessionStore is null) throw new InvalidOperationException("Protected session storage is not configured.");
        var bytes = await _sessionStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (bytes is null) throw new UnauthorizedAccessException("managed_sign_in_required");
        try
        {
            var refreshToken = Encoding.UTF8.GetString(bytes);
            using var response = await _http.PostAsJsonAsync("/v1/auth/refresh",
                new RefreshSession(refreshToken), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await _sessionStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
                throw new UnauthorizedAccessException("managed_session_expired");
            }
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Managed session refresh failed.", null, response.StatusCode);
            var session = await response.Content.ReadFromJsonAsync<ApiSession>(cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("Refresh response was empty.");
            await SaveRefreshAsync(session.RefreshToken, cancellationToken).ConfigureAwait(false);
            return session;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        if (_sessionStore is null) return;
        var bytes = await _sessionStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (bytes is null) return;
        try
        {
            var refreshToken = Encoding.UTF8.GetString(bytes);
            using var response = await _http.PostAsJsonAsync("/v1/auth/logout",
                new RefreshSession(refreshToken), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
                throw new HttpRequestException("Managed sign-out failed.", null, response.StatusCode);
            await _sessionStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private async Task SaveRefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (_sessionStore is null) return;
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        try { await _sessionStore.SaveAsync(bytes, cancellationToken).ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static Task LaunchSystemBrowserAsync(Uri authorizationUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Process.Start(new ProcessStartInfo(authorizationUri.AbsoluteUri) { UseShellExecute = true })
            ?? throw new InvalidOperationException("The system browser could not be started.");
        return Task.CompletedTask;
    }

    private static async Task<BrowserCallback> ReceiveCallbackAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine)) throw new InvalidDataException("Browser callback request was empty.");
        for (var i = 0; i < 64; i++)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null || line.Length == 0) break;
            if (i == 63) throw new InvalidDataException("Browser callback headers were too large.");
        }
        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "GET")
        {
            await ReplyAsync(stream, 400, "Invalid sign-in response.", cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("managed_sign_in_callback_invalid");
        }
        if (!Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out var uri) ||
            !string.Equals(uri.AbsolutePath, CallbackPath, StringComparison.Ordinal))
        {
            await ReplyAsync(stream, 404, "Invalid sign-in path.", cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("managed_sign_in_callback_path");
        }
        var code = QueryValue(uri, "code");
        var state = QueryValue(uri, "state");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            await ReplyAsync(stream, 400, "Missing sign-in parameters.", cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("managed_sign_in_callback_missing");
        }
        await ReplyAsync(stream, 200, "VideoGrabber: вход подтверждён. Это окно можно закрыть.", cancellationToken)
            .ConfigureAwait(false);
        return new(code, state);
    }

    private static async Task ReplyAsync(NetworkStream stream, int status, string message, CancellationToken cancellationToken)
    {
        var reason = status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request";
        var body = Encoding.UTF8.GetBytes("<!doctype html><meta charset=\"utf-8\"><title>VideoGrabber</title><p>" +
            WebUtility.HtmlEncode(message) + "</p>");
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string QueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = pair.Split('=', 2);
            if (Uri.UnescapeDataString(item[0]) == name)
                return Uri.UnescapeDataString(item.Length == 2 ? item[1] : string.Empty);
        }
        return string.Empty;
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed record BrowserCallback(string Code, string State);
}