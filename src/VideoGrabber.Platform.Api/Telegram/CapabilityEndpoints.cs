namespace VideoGrabber.Platform.Api.Telegram;

public sealed record PlatformCapabilities(
    bool MediaAvailable,
    bool DesktopWorkerAvailable,
    string Reason);

public static class CapabilityEndpoints
{
    public static IEndpointRouteBuilder MapCapabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/capabilities", () => Results.Ok(new PlatformCapabilities(
            MediaAvailable: false,
            DesktopWorkerAvailable: false,
            Reason: "media_worker_not_installed")))
            .RequireAuthorization();
        return endpoints;
    }
}