namespace VideoGrabber.Platform.Api.Jobs;

public static class MediaStoragePolicy
{
    // Legacy artifact processing is an explicit opt-in, never a production default.
    public static bool ClientOnly(IConfiguration configuration)
        => configuration["VG_MEDIA_STORAGE_MODE"] != "server_artifacts";

    public static bool BlocksMediaPath(string path, string method)
        => (path.StartsWith("/v1/desktop-worker/", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/uploads", StringComparison.OrdinalIgnoreCase))
            || path.StartsWith("/v1/downloads/", StringComparison.OrdinalIgnoreCase)
            || (path.StartsWith("/v1/jobs/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/download-link", StringComparison.OrdinalIgnoreCase))
            || (method != "GET" && path.StartsWith("/v1/deliveries", StringComparison.OrdinalIgnoreCase));
}
