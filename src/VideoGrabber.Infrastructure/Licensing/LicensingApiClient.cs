using System.Net.Http.Headers;
using System.Net.Http.Json;
using VideoGrabber.Core.Licensing;
using VideoGrabber.Infrastructure.Diagnostics;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Infrastructure.Licensing;

public sealed class LicensingApiClient(
    HttpClient http,
    Func<string?> accessToken,
    Func<Guid?> deviceId,
    OfflineAccessCache? offlineCache = null,
    Func<DateTimeOffset>? utcNow = null) : IManagedAccessClient
{
    public async Task<OperationPermit> AuthorizeAsync(
        ManagedOperation operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = accessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            if (TryOffline(operation, out var offline)) return offline!;
            throw new UnauthorizedAccessException("managed_sign_in_required");
        }

        if (RequiresTimeAccess(operation.Kind))
        {
            try
            {
                using var request = Create(HttpMethod.Get, "/v1/access", token);
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new UnauthorizedAccessException("access_check_failed");
                var access = await response.Content.ReadFromJsonAsync<AccessSnapshot>(cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidDataException("Access response was empty.");
                if (!access.CanEdit) throw new UnauthorizedAccessException(access.Reason);
                return new(operation.IntentId, null, false);
            }
            catch (HttpRequestException)
            {
                if (TryOffline(operation, out var offline)) return offline!;
                throw new UnauthorizedAccessException("online_access_required");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (TryOffline(operation, out var offline)) return offline!;
                throw new UnauthorizedAccessException("online_access_required");
            }
        }

        var selectedDevice = operation.DeviceId ?? deviceId();
        var reservation = new ReservationRequest(
            operation.IntentId,
            operation.RequestHash,
            "download",
            operation.Executor,
            selectedDevice);
        try
        {
            using var reserve = Create(HttpMethod.Post, "/v1/reservations", token);
            reserve.Content = JsonContent.Create(reservation);
            using var reserved = await http.SendAsync(reserve, cancellationToken).ConfigureAwait(false);
            if (!reserved.IsSuccessStatusCode)
            {
                var reason = reserved.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? "access_unavailable" : "reservation_failed";
                throw new UnauthorizedAccessException(reason);
            }
            var receipt = await reserved.Content.ReadFromJsonAsync<ReservationReceipt>(cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("Reservation response was empty.");
            return new(operation.IntentId, receipt.ReservationId, false);
        }
        catch (HttpRequestException)
        {
            if (TryOffline(operation, out var offline)) return offline!;
            throw new UnauthorizedAccessException("online_access_required");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (TryOffline(operation, out var offline)) return offline!;
            throw new UnauthorizedAccessException("online_access_required");
        }
    }

    public Task ReportAsync(OperationPermit permit, string outcome, CancellationToken cancellationToken)
    {
        if (outcome is not ("completed" or "failed" or "cancel_requested"))
            throw new ArgumentException("Unknown managed operation outcome.", nameof(outcome));
        DiagnosticHub.Log.Write("managed.operation", outcome,
            permit.Offline ? "signed offline time lease" :
            permit.ReservationId is null ? "time-access operation" : "reservation outcome pending worker proof");
        return Task.CompletedTask;
    }

    private bool TryOffline(ManagedOperation operation, out OperationPermit? permit)
    {
        permit = null;
        return offlineCache is not null && offlineCache.TryAuthorize(
            operation, utcNow?.Invoke() ?? DateTimeOffset.UtcNow, out permit);
    }

    private static bool RequiresTimeAccess(string kind)
        => kind is "edit" or "mp3" or "transcription";

    private static HttpRequestMessage Create(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-VideoGrabber-Api-Version", "1");
        return request;
    }
}