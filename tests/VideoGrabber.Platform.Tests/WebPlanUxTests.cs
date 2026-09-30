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

        var htmlIds = System.Text.RegularExpressions.Regex.Matches(
                html, "id=\\\"([^\\\"]+)\\\"")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var referencedIds = System.Text.RegularExpressions.Regex.Matches(
                js, @"\$\(\""\#([A-Za-z0-9_-]+)\""\)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(referencedIds, id => !htmlIds.Contains(id));

        Assert.Contains("data-plan=\"free\"", html);
        Assert.Contains("data-plan=\"start\"", html);
        Assert.Contains("data-plan=\"unlimited_video\"", html);
        Assert.Contains("data-plan=\"full_course\"", html);
        Assert.Contains("id=\"plan-dialog\"", html);
        Assert.Contains("href=\"/download/windows\"", html);
        Assert.Contains("href=\"/download/windows/portable\"", html);
        Assert.Contains("Скачивайте видео проще", html);
        Assert.Contains("Как это работает", html);
        Assert.Contains("Вход и синхронизация", html);
        Assert.Contains(">Тарифы</h2>", html);
        Assert.Contains("10 обычных видео навсегда", html);
        Assert.Contains("Популярный", html);
        Assert.Contains("Для курсов", html);
        Assert.Contains("Всё из Unlimited Video", html);
        Assert.Contains("Скачать установщик", html);
        Assert.Contains("Portable ZIP", html);
        Assert.Contains("planet-scene", html);
        Assert.Contains("hero-visual", html);
        Assert.DoesNotContain("Тарифы без скрытых", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Отправить на мой компьютер", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Supabase", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cannot set properties", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"admin-link\"", html);
        Assert.Contains("/assets/videograbber-icon.png", html);
        Assert.DoesNotContain("/download/windows/checksum", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SHA-256 текущей версии", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com", js, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("billing-environment", html, StringComparison.Ordinal);
        Assert.DoesNotContain("billing-environment", js, StringComparison.Ordinal);
        Assert.DoesNotContain("YooKassa", html, StringComparison.Ordinal);
        Assert.DoesNotContain("YooKassa", js, StringComparison.Ordinal);
        Assert.DoesNotContain("Whisper", js, StringComparison.Ordinal);
        Assert.Contains("Оплата открывается на отдельной защищённой странице", html, StringComparison.Ordinal);
        Assert.Contains("Сейчас не удалось открыть оплату", js, StringComparison.Ordinal);
        Assert.Contains("VG_WINDOWS_SETUP_PATH", program);
        Assert.Contains("VideoGrabber-Setup.exe", program);
        Assert.Contains("VG_WINDOWS_DOWNLOAD_PATH", program);
        Assert.Contains("VideoGrabber-Windows.zip", program);
        Assert.Contains("MapGet(\"/download/windows/portable\"", program);
        Assert.Contains("href=\"/miniapp/\"", html);
        Assert.Contains("openPlanDialog", js);
        Assert.Contains("setupHeroScene", js);
        Assert.Contains("Не удалось загрузить данные аккаунта", js);
        Assert.Contains("recommendedPlanForOperation", js);
        Assert.DoesNotContain("courseOption.disabled", js, StringComparison.Ordinal);
        Assert.DoesNotContain("mp3Option.disabled", js, StringComparison.Ordinal);
        Assert.Contains("MapGet(\"/download/windows\"", program);
    }

    [Fact]
    public void Owner_admin_surface_requires_totp_and_exposes_granular_controls()
    {
        var root = FindRepoRoot();
        var adminHtml = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "admin", "index.html"));
        var adminJs = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "wwwroot", "admin", "admin.js"));
        var program = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "Program.cs"));
        var compose = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "compose.staging.yml"));
        var ownerPolicy = File.ReadAllText(Path.Combine(
            root, "src", "VideoGrabber.Platform.Api", "Admin", "OwnerAdminAccessService.cs"));

        Assert.Contains("Двухфакторная аутентификация", adminHtml);
        Assert.Contains("mfa-code", adminHtml);
        Assert.Contains("feature-grid", adminHtml);
        Assert.Contains("grant-all-button", adminHtml);
        Assert.Contains("/v1/admin/mfa/status", adminJs);
        Assert.Contains("/v1/admin/mfa/verify", adminJs);
        Assert.Contains("vg_admin_access", adminJs);
        Assert.Contains("VG_ADMIN_MFA_ENCRYPTION_KEY", program);
        Assert.Contains("admin_mfa_encryption_key", compose);
        Assert.Contains("VG_OWNER_ADMIN_SUBJECT", compose, StringComparison.Ordinal);
        Assert.Contains("VG_OWNER_ADMIN_SUBJECT", ownerPolicy, StringComparison.Ordinal);
        Assert.Contains("provider_subject=@subject", ownerPolicy, StringComparison.Ordinal);
        Assert.Contains("account_id<>@owner", ownerPolicy, StringComparison.Ordinal);
        Assert.Contains("base_role='owner_admin'", ownerPolicy, StringComparison.Ordinal);
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
        var socialDocker = File.ReadAllText(Path.Combine(
            root, "deploy", "platform", "social-egress.dockerfile"));
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
        Assert.Contains("wireproxy", socialDocker);
        Assert.Contains("ENTRYPOINT [\"/usr/local/bin/wireproxy\"]", socialDocker);
        Assert.Contains("deno:", analysis);
        Assert.Contains("deno:", worker);
        Assert.Contains("youtubepot-bgutilhttp", analysis);
        Assert.Contains("youtube:player_client=mweb,default", analysis);
        Assert.Contains("youtube:player_client=mweb,default", worker);
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
