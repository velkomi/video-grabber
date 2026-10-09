using VideoGrabber.Core.ClientUpdates;
using VideoGrabber.Platform.Contracts.ClientUpdates;

namespace VideoGrabber.Infrastructure.ClientUpdates;

public sealed class ClientReleaseClient(HttpMessageHandler? handler = null, TimeSpan? bodyTimeout = null) : IDisposable
{
    private readonly AnonymousUpdateTransport _transport = new(handler);
    private readonly TimeSpan _bodyTimeout = bodyTimeout ?? TimeSpan.FromSeconds(20);

    public async Task<SignedClientRelease> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _transport.OpenAsync(uri, cancellationToken).ConfigureAwait(false);
        using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bodyDeadline.CancelAfter(_bodyTimeout);
        cancellationToken = bodyDeadline.Token;
        if (response.Content.Headers.ContentLength > ClientReleaseVerifier.MaxEnvelopeBytes)
            throw new ClientReleaseRejectedException("size");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > ClientReleaseVerifier.MaxEnvelopeBytes) throw new ClientReleaseRejectedException("size");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        if (response.Content.Headers.ContentLength is { } expected && expected != buffer.Length)
            throw new ClientReleaseRejectedException("size");
        return ClientReleaseSigner.DeserializeEnvelope(buffer.ToArray());
    }

    public void Dispose() => _transport.Dispose();
}
