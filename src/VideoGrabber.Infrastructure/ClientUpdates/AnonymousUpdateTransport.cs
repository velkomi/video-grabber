using System.Net;
using VideoGrabber.Core.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

internal sealed class AnonymousUpdateTransport : IDisposable
{
    private readonly HttpClient _http;
    public AnonymousUpdateTransport(HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            MaxResponseHeadersLength = 32
        }) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<HttpResponseMessage> OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop <= 3; hop++)
        {
            ClientReleaseVerifier.ValidatePublicHttpsUrl(uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (hop == 3 || location is null) throw new ClientReleaseRejectedException("redirect");
                uri = new Uri(uri, location);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            { response.Dispose(); throw new HttpRequestException("Update service unavailable", null, response.StatusCode); }
            if (response.Content.Headers.ContentEncoding.Count != 0)
            { response.Dispose(); throw new ClientReleaseRejectedException("encoding"); }
            return response;
        }
        throw new ClientReleaseRejectedException("redirect");
    }

    public void Dispose() => _http.Dispose();
}
