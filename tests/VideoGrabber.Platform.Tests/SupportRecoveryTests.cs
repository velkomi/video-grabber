using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupportRecoveryTests
{
    [Fact]
    public async Task Guest_clients_on_same_network_have_separate_challenge_and_submit_budgets()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true; await f.RestartAsync();
        using var a = f.TelegramWebClient(); using var b = f.TelegramWebClient();
        (await a.GetAsync("/v1/support/config")).EnsureSuccessStatusCode();
        (await b.GetAsync("/v1/support/config")).EnsureSuccessStatusCode();
        for (var i = 0; i < 7; i++) await a.GetAsync("/v1/support/challenge");
        using var challenge = await b.GetAsync("/v1/support/challenge");
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        for (var i = 0; i < 11; i++) await a.PostAsJsonAsync("/v1/support/requests", new { topic = "invalid" });
        using var proof = await b.GetAsync("/v1/support/challenge");
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var submit = await b.PostAsJsonAsync("/v1/support/requests", new { topic = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    [Fact]
    public async Task Stale_session_bootstraps_csrf_for_guest_support()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true; await f.RestartAsync();
        using var client = f.TelegramWebClient();
        client.DefaultRequestHeaders.Add("Cookie", "vg_session=expired-test-session");
        using var config = await client.GetAsync("/v1/support/config");
        using var json = JsonDocument.Parse(await config.Content.ReadAsStringAsync());
        var csrf = json.RootElement.GetProperty("csrfToken").GetString();
        Assert.False(string.IsNullOrEmpty(csrf));
        // Preserve the stale session while modelling browser receipt of the newly issued cookie.
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", "vg_session=expired-test-session; vg_csrf=" + csrf);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        var proof = await SupportRequestTests.SolveAsync(client);
        using var result = await client.PostAsJsonAsync("/v1/support/requests", new
        {requestId=Guid.NewGuid(),topic="sign_in",contact="guest@example.test",message="Не удаётся войти после истечения старой сессии.",altcha=proof});
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }
}
