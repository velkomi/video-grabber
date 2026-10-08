using System.Security.Cryptography;
using System.Text;
using VideoGrabber.Platform.Api.Admin;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using VideoGrabber.Platform.Persistence;
namespace VideoGrabber.Platform.Api.Jobs;

public sealed record PreparedDirectDownload(DirectMediaSource Source, ReservationReceipt Receipt, bool Created);
public sealed class DirectDownloadService(
    DirectMediaResolver resolver, CreditLedger ledger, IAccountStore accounts,
    AdminFeatureOverrideService overrides, IBotApiClient bot)
{
    public async Task<PreparedDirectDownload> PrepareAsync(Guid accountId, Uri source, Guid intentId,
        bool telegram, CancellationToken cancellationToken, long? telegramChatId = null)
    {
        var profile = await accounts.ReadAsync(accountId, cancellationToken);
        if (profile is null || profile.Blocked || !AccountEligibility.CanUseProtectedDownloads(profile))
            throw new UnauthorizedAccessException();
        var adminOverride = await overrides.ReadEffectiveAsync(accountId, "download", cancellationToken);
        if (adminOverride is false) throw new ReservationUnavailableException();
        var resolved = await resolver.ResolveAsync(source, cancellationToken);
        // Cloud Telegram fetching is intentionally limited; there is no local Bot API fallback.
        if (telegram && (resolved.MediaType != "video/mp4" || resolved.Bytes > 20 * 1024 * 1024))
            throw new DirectSourceUnsupportedException();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "direct:" + (telegram ? "telegram:" + telegramChatId?.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" : "web:") + source.AbsoluteUri))).ToLowerInvariant();
        var result = await ledger.PrepareDirectLinkAsync(accountId, intentId, hash, cancellationToken, adminOverride is true, telegram);
        return new PreparedDirectDownload(resolved, result.Receipt, result.Created);
    }

    public async Task<string> SendTelegramAsync(Guid accountId, Uri source, Guid intentId, long chatId, CancellationToken token)
    {
        var prepared = await PrepareAsync(accountId, source, intentId, true, token, chatId);
        if (!prepared.Created)
        {
            return prepared.Receipt.State == "completed" ? "already_delivered"
                : prepared.Receipt.State == "reserved" ? "pending" : "review_required";
        }
        try
        {
            await bot.SendVideoUrlAsync(chatId, prepared.Source.Url, TelegramCaption(source), token);
            // Telegram already confirmed the send. A canceled caller must not
            // turn this confirmed delivery into an unknown quota reservation.
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await CompleteAsync(accountId, prepared, completion.Token);
            return "delivered";
        }
        catch (NotSupportedException)
        { await ReleaseAsync(accountId, prepared, token); throw new DirectSourceUnsupportedException(); }
        catch (HttpRequestException ex) when (ex.StatusCode is >= System.Net.HttpStatusCode.BadRequest and < System.Net.HttpStatusCode.InternalServerError)
        { await ReleaseAsync(accountId, prepared, token); throw new DirectSourceUnsupportedException(); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or System.Text.Json.JsonException)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await MarkUnknownAsync(accountId, prepared, cleanup.Token);
            return "review_required";
        }
    }

    private static string TelegramCaption(Uri originalSource)
    {
        // HEAD supplies no title, resolution or duration. Use only the submitted
        // filename and never expose redirected transport URLs or signed parameters.
        var sensitivePath = originalSource.AbsolutePath.Contains("/v1/downloads/", StringComparison.OrdinalIgnoreCase)
            || originalSource.Segments.Any(segment => segment.Trim('/').Equals("signed", StringComparison.OrdinalIgnoreCase)
                || segment.Trim('/').Equals("token", StringComparison.OrdinalIgnoreCase)
                || segment.Trim('/').Equals("ticket", StringComparison.OrdinalIgnoreCase)
                || segment.Length > 160);
        var fileName = sensitivePath ? string.Empty : Uri.UnescapeDataString(originalSource.Segments.LastOrDefault() ?? string.Empty);
        fileName = string.Concat(fileName.Select(character => char.IsControl(character) ? ' ' : character)).Trim();
        var name = fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            && fileName.Length <= 160 ? fileName + " (имя файла)" : "неизвестно";
        var sourceLabel = sensitivePath ? originalSource.GetLeftPart(UriPartial.Authority) + " (путь скрыт)"
            : originalSource.GetLeftPart(UriPartial.Path);
        if (sourceLabel.Length > 600)
            sourceLabel = originalSource.GetLeftPart(UriPartial.Authority) + " (ссылка сокращена)";
        var parametersHidden = originalSource.Query.Length > 0 || originalSource.Fragment.Length > 0;
        return $"Название: {name}\nИсточник: {sourceLabel}" +
            (parametersHidden ? " (параметры скрыты)" : string.Empty) +
            "\nКачество: разрешение неизвестно\nДлительность: неизвестна";
    }

    public Task<ReservationReceipt> CompleteAsync(Guid accountId, PreparedDirectDownload prepared, CancellationToken token)
        => ledger.CompleteDirectLinkAsync(accountId, prepared.Receipt,
            "direct-link-issued:" + prepared.Receipt.IntentId.ToString("N"), token);

    public Task<ReservationReceipt> MarkUnknownAsync(Guid accountId, PreparedDirectDownload prepared, CancellationToken token)
        => ledger.FinalizeAsync(accountId, new FinalizeReservation(prepared.Receipt.ReservationId,
            prepared.Receipt.IntentId, 1, "review_required", "direct-telegram-unknown:" + prepared.Receipt.IntentId.ToString("N")), token);

    public Task<bool> ReleaseAsync(Guid accountId, PreparedDirectDownload prepared, CancellationToken token)
        => ledger.ReleaseDirectUnsentAsync(accountId, prepared.Receipt, token);
}

public sealed record DirectDownloadRequest(Uri Source, Guid IntentId);
public sealed record DirectTelegramRequest(Uri Source, Guid IntentId, Guid DestinationId);
public static class DirectDownloadEndpoints
{
    public static IEndpointRouteBuilder MapDirectDownloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/media-policy", (IConfiguration configuration) => Results.Ok(new
        { mode = MediaStoragePolicy.ClientOnly(configuration) ? "client_only" : "server_artifacts",
          serverMediaEnabled = !MediaStoragePolicy.ClientOnly(configuration), directTelegramMaxBytes = 20 * 1024 * 1024 })).AllowAnonymous();
        endpoints.MapPost("/v1/direct-downloads", async (DirectDownloadRequest request,
            HttpContext http, DirectDownloadService downloads, CancellationToken token) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId)) return Results.Unauthorized();
            if (request.Source is null || request.IntentId == Guid.Empty)
                return Results.BadRequest(new { code = "invalid_direct_request" });
            try
            {
                var prepared = await downloads.PrepareAsync(accountId, request.Source, request.IntentId, false, token);
                await downloads.CompleteAsync(accountId, prepared, token);
                http.Response.Headers.CacheControl = "private, no-store";
                return Results.Ok(new { url = prepared.Source.Url.AbsoluteUri, mediaType = prepared.Source.MediaType,
                    bytes = prepared.Source.Bytes, quotaAccounting = "link_issued" });
            }
            catch (DirectSourceUnsupportedException) { return Results.Conflict(new { code = "desktop_execution_required" }); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (ReservationUnavailableException) { return Results.Conflict(new { code = "access_unavailable" }); }
            catch (ReservationConflictException) { return Results.Conflict(new { code = "direct_request_conflict" }); }
            catch (HttpRequestException) { return Results.Conflict(new { code = "desktop_execution_required" }); }
            catch (TaskCanceledException) when (!token.IsCancellationRequested) { return Results.Conflict(new { code = "desktop_execution_required" }); }
        }).RequireAuthorization();
        endpoints.MapPost("/v1/direct-downloads/telegram", async (DirectTelegramRequest request, HttpContext http,
            DirectDownloadService downloads, DestinationService destinations, CancellationToken token) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("account_id")?.Value, out var accountId)) return Results.Unauthorized();
            if (request.Source is null || request.IntentId == Guid.Empty || request.DestinationId == Guid.Empty)
                return Results.BadRequest(new { code = "invalid_direct_request" });
            try
            {
                var destination = await destinations.AuthorizeSendAsync(accountId, request.DestinationId, token);
                var state = await downloads.SendTelegramAsync(accountId, request.Source, request.IntentId, destination.ChatId, token);
                return Results.Ok(new { state });
            }
            catch (DirectSourceUnsupportedException) { return Results.Conflict(new { code = "desktop_execution_required" }); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ReservationUnavailableException) { return Results.Conflict(new { code = "access_unavailable" }); }
            catch (ReservationConflictException) { return Results.Conflict(new { code = "direct_request_conflict" }); }
            catch (HttpRequestException) { return Results.Conflict(new { code = "direct_source_unavailable" }); }
            catch (TaskCanceledException) when (!token.IsCancellationRequested) { return Results.Conflict(new { code = "direct_source_unavailable" }); }
        }).RequireAuthorization();
        return endpoints;
    }
}
