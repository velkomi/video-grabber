using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

public sealed record ClientServiceSelection(ClientServiceEndpoints Services, bool BlockOnline, bool ExplicitOverride,
    ClientReleaseManifest? RuntimeProof = null);

public static class ClientServiceDirectory
{
    public static ClientServiceSelection Select(ClientReleaseCacheSnapshot snapshot,
        ClientServiceEndpoints bootstrap, string? configuredOverride)
    {
        var services = snapshot.Active?.Services ?? bootstrap;
        if (TryOverride(configuredOverride, out var uri)) return new(new(uri, services.WebsiteBase), false, true);
        return new(services, snapshot.ActiveBlocked, false, snapshot.Active);
    }

    private static bool TryOverride(string? configured, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var parsed) || parsed.UserInfo.Length != 0 ||
            parsed.Fragment.Length != 0 || parsed.Query.Length != 0) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps && !(parsed.Scheme == Uri.UriSchemeHttp && parsed.Host == "127.0.0.1")) return false;
        uri = parsed.AbsoluteUri.EndsWith('/') ? parsed : new Uri(parsed.AbsoluteUri + "/");
        return true;
    }
}

public sealed class ClientDirectoryRequestGate(
    ClientReleaseCache cache, ClientServiceSelection selection, ClientServiceEndpoints bootstrap,
    HttpMessageHandler innerHandler, TimeProvider? clock = null) : DelegatingHandler(innerHandler)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private ClientReleaseManifest? _runtimeProof = selection.RuntimeProof;
    private readonly object _proofGate = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !SameOrigin(request.RequestUri, selection.Services.ApiBase) || !Allowed())
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            { Content = JsonContent.Create(new { code = "service_directory_refresh_required" }) });
        return base.SendAsync(request, cancellationToken);
    }

    private bool Allowed()
    {
        if (selection.ExplicitOverride) return true;
        var state = cache.Read();
        if (state.Latest is null) return !state.ActiveBlocked && ClientReleaseCache.SameServices(selection.Services, bootstrap);
        if (state.Active is null && state.ActiveBlocked) return false;
        var now = _clock.GetUtcNow();
        lock (_proofGate)
        {
            var renewal = new[] { state.Latest, state.Active, state.Previous }
                .Where(m => m is not null && ClientReleaseCache.SameServices(m.Services, selection.Services))
                .OrderByDescending(m => m!.Sequence).FirstOrDefault();
            if (renewal is not null && (_runtimeProof is null || renewal.Sequence >= _runtimeProof.Sequence))
                _runtimeProof = renewal;
            // Keep the runtime proof even after several future addresses have been approved.
            if (_runtimeProof is not null) return _runtimeProof.ExpiresAt > now;
        }
        return !selection.BlockOnline && state.Latest.ExpiresAt > now &&
            ClientReleaseCache.SameServices(selection.Services, bootstrap);
    }

    private static bool SameOrigin(Uri a, Uri b) => a.IsAbsoluteUri && a.Scheme == b.Scheme &&
        a.IdnHost.Equals(b.IdnHost, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;
}
