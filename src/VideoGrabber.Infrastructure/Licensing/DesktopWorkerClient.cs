using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class DesktopWorkerClient(
    HttpClient http,
    Func<string?> accessToken,
    Func<string, byte[]> signNonce)
{
    public async Task<AttemptLease?> PollAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        using var challengeRequest = Create(
            HttpMethod.Post, $"/v1/devices/{deviceId:D}/challenge");
        using var challengeResponse = await http.SendAsync(
            challengeRequest, cancellationToken).ConfigureAwait(false);
        if (challengeResponse.StatusCode == HttpStatusCode.NotFound)
            throw new UnauthorizedAccessException("desktop_device_revoked");
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content
            .ReadFromJsonAsync<DeviceChallenge>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("Device challenge response was empty.");
        var signature = signNonce(challenge.Nonce);
        try
        {
            using var claim = Create(
                HttpMethod.Post, $"/v1/desktop-worker/devices/{deviceId:D}/claim");
            claim.Content = JsonContent.Create(new DeviceLeaseProof(
                challenge.Nonce, Convert.ToBase64String(signature)));
            using var response = await http.SendAsync(
                claim, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException("desktop_device_revoked");
            response.EnsureSuccessStatusCode();
            var lease = await response.Content.ReadFromJsonAsync<AttemptLease>(
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Desktop worker lease was empty.");
            if (lease.Work.Executor != "desktop_worker"
                || lease.Work.DeviceId != deviceId)
                throw new UnauthorizedAccessException("worker_scope_mismatch");
            return lease;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature);
        }
    }

    public async Task<bool> HeartbeatAsync(
        Guid deviceId,
        AttemptLease lease,
        CancellationToken cancellationToken)
    {
        EnsureScope(deviceId, lease);
        using var request = Create(
            HttpMethod.Post,
            $"/v1/desktop-worker/devices/{deviceId:D}/heartbeat");
        request.Content = JsonContent.Create(lease);
        using var response = await http.SendAsync(
            request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return true;
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return false;
    }

    public async Task<WorkerSourceDescriptor> ResolveSourceAsync(
        Guid deviceId,
        string sourceId,
        string quality,
        CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty
            || string.IsNullOrWhiteSpace(sourceId)
            || string.IsNullOrWhiteSpace(quality))
            throw new ArgumentException("Desktop source request is incomplete.");

        using var request = Create(
            HttpMethod.Get,
            $"/v1/desktop-worker/devices/{deviceId:D}/sources/{Uri.EscapeDataString(sourceId)}?quality={Uri.EscapeDataString(quality)}");
        using var response = await http.SendAsync(
            request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new UnauthorizedAccessException("desktop_source_unavailable");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkerSourceDescriptor>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Desktop source response was empty.");
    }

    public async Task<JobView> CompleteLocalAsync(
        AttemptLease lease,
        string outcome,
        string evidenceId,
        CancellationToken cancellationToken)
    {
        if (lease.Work.DeviceId is not Guid deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");
        EnsureScope(deviceId, lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);

        using var request = Create(
            HttpMethod.Post,
            $"/v1/desktop-worker/devices/{deviceId:D}/complete-local");
        request.Content = JsonContent.Create(
            new DesktopLocalCompletionRequest(lease, outcome, evidenceId));
        using var response = await http.SendAsync(
            request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobView>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Local completion response was empty.");
    }

    public async Task<ArtifactReceipt> UploadAsync(
        AttemptLease lease,
        string selectedLocalPath,
        CancellationToken cancellationToken)
    {
        if (lease.Work.DeviceId is not Guid deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");
        EnsureScope(deviceId, lease);
        var path = Path.GetFullPath(selectedLocalPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Selected local output does not exist.", path);
        var info = new FileInfo(path);
        if (info.Length <= 0)
            throw new InvalidDataException("Selected local output is empty.");

        string sha;
        await using (var input = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            sha = Convert.ToHexString(
                await SHA256.HashDataAsync(input, cancellationToken))
                .ToLowerInvariant();
        }

        using var ticketRequest = Create(
            HttpMethod.Post,
            $"/v1/desktop-worker/devices/{deviceId:D}/uploads");
        ticketRequest.Content = JsonContent.Create(new UploadTicketRequest(
            lease, info.Length, sha, MediaType(path)));
        using var ticketResponse = await http.SendAsync(
            ticketRequest, cancellationToken).ConfigureAwait(false);
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<UploadTicket>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Upload ticket was empty.");
        if (ticket.JobId != lease.JobId
            || ticket.AttemptId != lease.AttemptId
            || ticket.Fence != lease.Fence
            || info.Length > ticket.MaximumBytes)
            throw new UnauthorizedAccessException("upload_ticket_scope_mismatch");

        await using var content = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var upload = Create(
            HttpMethod.Put,
            $"/v1/desktop-worker/devices/{deviceId:D}/uploads/{ticket.UploadId:D}");
        upload.Content = new StreamContent(content);
        upload.Content.Headers.ContentLength = info.Length;
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaType(path));
        using var response = await http.SendAsync(
            upload, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ArtifactReceipt>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Artifact upload receipt was empty.");
    }

    public async Task<JobView> CompleteAsync(
        AttemptLease lease,
        ArtifactReceipt artifact,
        CancellationToken cancellationToken)
    {
        if (lease.Work.DeviceId is not Guid deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");
        EnsureScope(deviceId, lease);
        using var request = Create(
            HttpMethod.Post,
            $"/v1/desktop-worker/devices/{deviceId:D}/complete");
        request.Content = JsonContent.Create(
            new DesktopCompletionRequest(lease, artifact));
        using var response = await http.SendAsync(
            request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobView>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Desktop completion response was empty.");
    }

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var token = accessToken();
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("managed_sign_in_required");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-VideoGrabber-Api-Version", "1");
        return request;
    }

    private static void EnsureScope(Guid deviceId, AttemptLease lease)
    {
        if (lease.Work.Executor != "desktop_worker"
            || lease.Work.DeviceId != deviceId)
            throw new UnauthorizedAccessException("worker_scope_mismatch");
    }

    private static string MediaType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".txt" => "text/plain",
            ".srt" => "application/x-subrip",
            _ => throw new InvalidDataException("Unsupported desktop artifact type.")
        };
}
