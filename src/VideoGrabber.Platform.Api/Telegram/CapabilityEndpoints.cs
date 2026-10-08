using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed record PlatformCapabilities(
    bool MediaAvailable,
    bool DesktopWorkerAvailable,
    string Reason,
    MediaCapability[] Operations);

public static class CapabilityEndpoints
{
    public static IEndpointRouteBuilder MapCapabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/capabilities", (IConfiguration configuration) =>
        {
            var clientOnly = Jobs.MediaStoragePolicy.ClientOnly(configuration);
            var serverAsr = !clientOnly && !string.IsNullOrWhiteSpace(
                configuration["VG_WORKER_WHISPER_MODEL"]);
            var operations = new[]
            {
                new MediaCapability(
                    "download", clientOnly ? ["desktop_worker"] : ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "course_download", ["desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "mp3", clientOnly ? ["desktop_worker"] : ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "trim", clientOnly ? ["desktop_worker"] : ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 1, Available: !clientOnly),
                new MediaCapability(
                    "join", clientOnly ? ["desktop_worker"] : ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 2, Available: !clientOnly),
                new MediaCapability(
                    "transcribe",
                    serverAsr
                        ? ["server_worker","desktop_worker"]
                        : ["desktop_worker"],
                    RequiresSource: true, MinimumInputs: 1, Available: !clientOnly)
            };
            return Results.Ok(new PlatformCapabilities(
                MediaAvailable: true,
                DesktopWorkerAvailable: true,
                Reason: clientOnly ? "client_only_media" : serverAsr ? "ready" : "server_asr_requires_model",
                Operations: operations));
        }).RequireAuthorization();
        endpoints.MapGet("/v1/telegram/bot-link", async (IBotApiClient bot, CancellationToken token) =>
        {
            try { return Results.Ok(new { url = (await bot.PublicBotLinkAsync(token))?.AbsoluteUri }); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            { return Results.Ok(new { url = (string?)null }); }
        }).AllowAnonymous();
        return endpoints;
    }
}
