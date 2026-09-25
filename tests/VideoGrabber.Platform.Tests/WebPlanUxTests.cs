using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class WebPlanUxTests
{
    [Fact]
    public void Web_pricing_is_clickable_and_windows_download_is_published()
    {
        var root = FindRepoRoot();
        var html = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "web", "index.html"));
        var js = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "web", "app.js"));
        var program = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "Program.cs"));

        Assert.Contains("data-plan=\"free\"", html);
        Assert.Contains("data-plan=\"start\"", html);
        Assert.Contains("data-plan=\"unlimited_video\"", html);
        Assert.Contains("data-plan=\"full_course\"", html);
        Assert.Contains("id=\"plan-dialog\"", html);
        Assert.Contains("href=\"/download/windows\"", html);
        Assert.Contains("https://t.me/Velkoshkin", html);
        Assert.Contains("openPlanDialog", js);
        Assert.Contains("recommendedPlanForOperation", js);
        Assert.DoesNotContain("courseOption.disabled", js, StringComparison.Ordinal);
        Assert.DoesNotContain("mp3Option.disabled", js, StringComparison.Ordinal);
        Assert.Contains("MapGet(\"/download/windows\"", program);
    }

    [Fact]
    public void Web_download_defaults_to_browser_and_uses_server_worker()
    {
        var root = FindRepoRoot();
        var html = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "web", "index.html"));
        var js = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "web", "app.js"));
        var jobs = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "Jobs", "JobEndpoints.cs"));
        var mini = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "miniapp", "index.html"));

        Assert.Contains("id=\"download-target\"", html);
        Assert.Contains("value=\"browser\" selected", html);
        Assert.Contains("/assets/videograbber-icon.png", html);
        Assert.Contains("/assets/videograbber-icon.png", mini);
        Assert.Contains(
            "executor: target === \"browser\" ? \"server_worker\" : \"desktop_worker\"",
            js);
        Assert.Contains("downloadJobResult", js);
        Assert.Contains(
            "/v1/jobs/\" + encodeURIComponent(jobId) + \"/download-link",
            js);
        Assert.Contains("/v1/jobs/{jobId:guid}/download-link", jobs);
        Assert.Contains("/v1/downloads/{ticket}", jobs);
    }

    [Fact]
    public void Social_video_runtime_has_deno_impersonation_and_youtube_pot_provider()
    {
        var root = FindRepoRoot();
        var apiDocker = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "api.Dockerfile"));
        var workerDocker = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "worker.Dockerfile"));
        var compose = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "compose.staging.yml"));
        var analysis = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "Jobs", "SourceAnalysisService.cs"));
        var worker = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Worker", "MediaJobExecutor.cs"));
        var web = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "web", "app.js"));

        Assert.Contains("python3 python3-pip", apiDocker);
        Assert.Contains("python3 python3-pip", workerDocker);
        Assert.Contains("curl-cffi==0.16.0", apiDocker);
        Assert.Contains("bgutil-ytdlp-pot-provider==2.0.0", apiDocker);
        Assert.Contains("COPY --from=deno_bin /deno /usr/local/bin/deno", apiDocker);
        Assert.Contains("youtube-pot-provider:", compose);
        Assert.Contains("VG_YOUTUBE_POT_PROVIDER_URL", compose);
        Assert.Contains("social-egress:", compose);
        Assert.Contains("VG_SOCIAL_EGRESS_PROXY_URI", compose);
        Assert.Contains("warp_wireproxy_config", compose);
        Assert.Contains("deno:", analysis);
        Assert.Contains("deno:", worker);
        Assert.Contains("youtubepot-bgutilhttp", analysis);
        Assert.Contains("\"--impersonate\", \"chrome\"", analysis);
        Assert.Contains("tiktok.com", analysis);
        Assert.Contains("instagram.com", analysis);
        Assert.Contains("pinterest.com", analysis);
        Assert.Contains("source_unavailable", analysis);
        Assert.Contains("source_rate_limited", analysis);
        Assert.Contains("source_runtime_incomplete", web);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
