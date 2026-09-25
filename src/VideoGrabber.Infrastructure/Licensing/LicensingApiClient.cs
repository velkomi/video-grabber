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
    Func<DateTimeOffset>? utcNow = null,
    Func<CancellationToken, Task<string?>>? refreshAccessToken = null) : IManagedAccessClient
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
            operation.Kind switch
            {
                "course_download" => "course_download",
                "mp3" => "premium_media",
                _ => "download"
            },
            operation.Executor,
            selectedDevice);
        try
        {
            var reserved = await SendReservationAsync(
                reservation, token, cancellationToken).ConfigureAwait(false);
            if (reserved.StatusCode == System.Net.HttpStatusCode.Unauthorized
                && refreshAccessToken is not null)
            {
                reserved.Dispose();
                token = await refreshAccessToken(cancellationToken).ConfigureAwait(false)
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(token))
                    throw new UnauthorizedAccessException("managed_sign_in_required");
                reserved = await SendReservationAsync(
                    reservation, token, cancellationToken).ConfigureAwait(false);
            }
            using (reserved)
            {
                if (!reserved.IsSuccessStatusCode)
                {
                    var reason = reserved.StatusCode == System.Net.HttpStatusCode.Conflict
                        ? "access_unavailable"
                        : reserved.StatusCode == System.Net.HttpStatusCode.Unauthorized
                            ? "managed_sign_in_required"
                            : "reservation_failed";
                    throw new UnauthorizedAccessException(reason);
                }
                var receipt = await reserved.Content.ReadFromJsonAsync<ReservationReceipt>(
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Reservation response was empty.");
                return new(operation.IntentId, receipt.ReservationId, false);
            }
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

    public async Task ReportAsync(
        OperationPermit permit,
        string outcome,
        CancellationToken cancellationToken)
    {
        if (outcome is not ("completed" or "failed" or "cancel_requested"))
            throw new ArgumentException(
                "Unknown managed operation outcome.", nameof(outcome));

        if (permit.Offline || permit.ReservationId is null)
        {
            DiagnosticHub.Log.Write(
                "managed.operation", outcome,
                permit.Offline ? "signed offline time lease" : "time-access operation");
            return;
        }

        var currentDevice = deviceId();
        if (currentDevice is not Guid device || device == Guid.Empty)
            throw new UnauthorizedAccessException("registered_device_required");

        var token = accessToken();
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("managed_sign_in_required");

        var payload = new LocalReservationOutcomeRequest(
            device,
            outcome,
            "managed-local:" + permit.IntentId.ToString("N") + ":" + outcome);

        var response = await SendOutcomeAsync(
            permit.ReservationId.Value, payload, token, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && refreshAccessToken is not null)
        {
            response.Dispose();
            token = await refreshAccessToken(cancellationToken).ConfigureAwait(false)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException("managed_sign_in_required");
            response = await SendOutcomeAsync(
                permit.ReservationId.Value, payload, token, cancellationToken)
                .ConfigureAwait(false);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new UnauthorizedAccessException(
                    response.StatusCode == System.Net.HttpStatusCode.Conflict
                        ? "reservation_conflict"
                        : response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                            ? "managed_sign_in_required"
                            : "reservation_outcome_failed");
        }

        DiagnosticHub.Log.Write(
            "managed.operation", outcome, "local reservation finalized");
    }

    private async Task<HttpResponseMessage> SendReservationAsync(
        ReservationRequest reservation,
        string token,
        CancellationToken cancellationToken)
    {
        using var reserve = Create(HttpMethod.Post, "/v1/reservations", token);
        reserve.Content = JsonContent.Create(reservation);
        return await http.SendAsync(reserve, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendOutcomeAsync(
        Guid reservationId,
        LocalReservationOutcomeRequest outcome,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = Create(
            HttpMethod.Post,
            $"/v1/reservations/{reservationId:D}/local-outcome",
            token);
        request.Content = JsonContent.Create(outcome);
        return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private sealed record LocalReservationOutcomeRequest(
        Guid DeviceId,
        string Outcome,
        string EvidenceId);

    private bool TryOffline(ManagedOperation operation, out OperationPermit? permit)
    {
        permit = null;
        return offlineCache is not null && offlineCache.TryAuthorize(
            operation, utcNow?.Invoke() ?? DateTimeOffset.UtcNow, out permit);
    }

    private static bool RequiresTimeAccess(string kind)
        => kind is "edit" or "transcription";

    private static HttpRequestMessage Create(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-VideoGrabber-Api-Version", "1");
        return request;
    }
}