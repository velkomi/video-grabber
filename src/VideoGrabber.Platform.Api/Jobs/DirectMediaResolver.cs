using System.Net;
namespace VideoGrabber.Platform.Api.Jobs;

public sealed class DirectSourceUnsupportedException : Exception;
public sealed record DirectMediaSource(Uri Url, string MediaType, long Bytes);

public sealed class DirectMediaResolver(EgressProxy egress, HttpClient http)
{
    public async Task<DirectMediaSource> ResolveAsync(Uri source, CancellationToken cancellationToken)
    {
        if (source.AbsoluteUri.Length > 8192) throw new DirectSourceUnsupportedException();
        var target = source;
        for (var redirects = 0; redirects < 5; redirects++)
        {
            await egress.ValidatePublicTargetAsync(target, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Head, target);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
                or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location ?? throw new DirectSourceUnsupportedException();
                target = location.IsAbsoluteUri ? location : new Uri(target, location);
                continue;
            }
            var type = response.Content.Headers.ContentType?.MediaType;
            var bytes = response.Content.Headers.ContentLength;
            if (!response.IsSuccessStatusCode || type is not ("video/mp4" or "video/webm")
                || bytes is not > 0)
                throw new DirectSourceUnsupportedException();
            // Do not read or buffer the response body. MP3/merging/manifests require a local executor.
            return new DirectMediaSource(target, type, bytes.Value);
        }
        throw new DirectSourceUnsupportedException();
    }
}
