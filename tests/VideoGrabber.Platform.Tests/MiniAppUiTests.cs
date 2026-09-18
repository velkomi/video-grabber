using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class MiniAppUiTests
{
    [Fact]
    public void Mini_app_uses_verified_init_data_real_APIs_and_safe_text_rendering()
    {
        var root = FindRepoRoot();
        var html = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.Platform.Api", "wwwroot", "miniapp", "index.html"));
        var js = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.Platform.Api", "wwwroot", "miniapp", "app.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "VideoGrabber.Platform.Api", "wwwroot", "miniapp", "styles.css"));

        Assert.Contains("window.Telegram.WebApp.initData", js);
        Assert.DoesNotContain("initDataUnsafe", js, StringComparison.Ordinal);
        Assert.Contains("/v1/telegram/session", js);
        Assert.Contains("/v1/me", js);
        Assert.Contains("/v1/access", js);
        Assert.Contains("/v1/identities", js);
        Assert.Contains("/v1/devices", js);
        Assert.Contains("/v1/capabilities", js);
        Assert.Contains("/v1/destinations", js);
        Assert.Contains("/v1/destinations/challenges", js);
        Assert.Contains("id=\"destination-form\"", html);
        Assert.Contains("id=\"destination-chat\"", html);
        Assert.Contains("id=\"destinations\"", html);
        Assert.Contains("id=\"media-form\"", html);
        Assert.Contains("/miniapp/media.js", html);
        Assert.Contains("credentials: \"same-origin\"", js);
        Assert.Contains("X-CSRF-Token", js);
        Assert.Contains("textContent", js);
        Assert.DoesNotContain("innerHTML", js, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-live", html);
        Assert.Contains("id=\"balance\"", html);
        Assert.Contains("id=\"providers\"", html);
        Assert.Contains("id=\"devices\"", html);
        Assert.Contains("id=\"admin\"", html);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("@media", css);
        Assert.Contains("320px", css);
    }

    [Fact]
    public async Task Capabilities_are_authenticated_and_media_is_available_after_P5()
    {
        await using var f = await ApiFixture.StartAsync();
        using var anonymous = await f.Anonymous.GetAsync("/v1/capabilities");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var miniApp = await f.MiniAppAsync(6601);
        var json = await miniApp.GetStringAsync("/v1/capabilities");
        Assert.Contains("\"mediaAvailable\":true", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mini_app_cookie_session_requires_csrf_for_mutations()
    {
        await using var f = await ApiFixture.StartAsync();
        using var miniApp = await f.MiniAppAsync(6602);

        using var withCsrf = await miniApp.PostAsync("/v1/identities/link", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, withCsrf.StatusCode);

        miniApp.DefaultRequestHeaders.Remove("X-CSRF-Token");
        using var withoutCsrf = await miniApp.PostAsync("/v1/identities/link", null);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, withoutCsrf.StatusCode);
    }
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VideoGrabber.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("VideoGrabber repository root not found.");
    }
}
