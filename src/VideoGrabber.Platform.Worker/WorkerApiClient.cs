using System.Net;
using System.Net.Http.Json;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Worker;

public sealed class WorkerApiClient(
    HttpClient http,
    Guid workerId,
    string workerToken,
    string[] supportedOperations) : IWorkerSourceResolver, IWorkerArtifactResolver
{
    private const string Header = "X-VideoGrabber-Worker-Token";

    public async Task<AttemptLease?> ClaimAsync(CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Post, "/v1/worker/jobs/claim");
        request.Content = JsonContent.Create(new WorkerClaim(
            workerId,
            PlatformProtocol.Current,
            supportedOperations));
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttemptLease>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HeartbeatAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Post, "/v1/worker/jobs/heartbeat");
        request.Content = JsonContent.Create(lease);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<JobView> CompleteAsync(
        AttemptCompletion completion,
        CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Post, "/v1/worker/jobs/complete");
        request.Content = JsonContent.Create(completion);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobView>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Worker completion response was empty.");
    }

    public async Task<WorkerResolvedSource> ResolveAsync(
        string sourceId,
        string quality,
        CancellationToken cancellationToken)
    {
        using var request = Create(
            HttpMethod.Get,
            "/v1/worker/sources/" + Uri.EscapeDataString(sourceId)
            + "?quality=" + Uri.EscapeDataString(quality));
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadFromJsonAsync<WorkerSourceDescriptor>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Worker source response was empty.");
        return new WorkerResolvedSource(
            source.Source,
            source.FormatSelector,
            source.Width,
            source.Height,
            source.MediaType);
    }

    public async Task<IReadOnlyList<WorkerArtifactDescriptor>> ResolveArtifactsAsync(
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Post, "/v1/worker/jobs/inputs");
        request.Content = JsonContent.Create(lease);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkerArtifactDescriptor[]>(
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
    }
    public async Task<RetentionCandidate?> ClaimRetentionAsync(
        CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Get, "/v1/worker/retention/claim");
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RetentionCandidate>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task AcknowledgeRetentionAsync(
        RetentionCleanupResult result,
        CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Post, "/v1/worker/retention/ack");
        request.Content = JsonContent.Create(result);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(Header, workerToken);
        request.Headers.Add(
            "X-VideoGrabber-Protocol",
            PlatformProtocol.Current.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return request;
    }
}
