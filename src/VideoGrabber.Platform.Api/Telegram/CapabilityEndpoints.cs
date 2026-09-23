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
            var serverAsr = !string.IsNullOrWhiteSpace(
                configuration["VG_WORKER_WHISPER_MODEL"]);
            var operations = new[]
            {
                new MediaCapability(
                    "download", ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "course_download", ["desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "mp3", ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 0, Available: true),
                new MediaCapability(
                    "trim", ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 1, Available: true),
                new MediaCapability(
                    "join", ["server_worker","desktop_worker"],
                    RequiresSource: true, MinimumInputs: 2, Available: true),
                new MediaCapability(
                    "transcribe",
                    serverAsr
                        ? ["server_worker","desktop_worker"]
                        : ["desktop_worker"],
                    RequiresSource: true, MinimumInputs: 1, Available: true)
            };
            return Results.Ok(new PlatformCapabilities(
                MediaAvailable: true,
                DesktopWorkerAvailable: true,
                Reason: serverAsr ? "ready" : "server_asr_requires_model",
                Operations: operations));
        }).RequireAuthorization();
        return endpoints;
    }
}
