using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class TelegramAuthTests
{
    internal const string BotToken = "test-telegram-bot-token-local-only";
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_init_data_returns_numeric_principal()
    {
        var validator = new MiniAppAssertionValidator(BotToken, TimeSpan.FromMinutes(5));
        var initData = SignInitData(new Dictionary<string, string>
        {
            ["auth_date"] = Now.ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AA-test-query",
            ["user"] = JsonSerializer.Serialize(new { id = 5001L, first_name = "Test" })
        }, BotToken);

        var principal = validator.Validate(initData, Now);

        Assert.Equal(5001L, principal.UserId);
        Assert.Equal(Now, principal.AuthTime);
        Assert.Equal(64, principal.AssertionHash.Length);
        Assert.DoesNotContain("AA-test-query", principal.AssertionHash);
    }

    [Fact]
    public void Tamper_unknown_hash_and_duplicate_fields_are_rejected()
    {
        var validator = new MiniAppAssertionValidator(BotToken, TimeSpan.FromMinutes(5));
        var fields = ValidFields(5002);
        var signed = SignInitData(fields, BotToken);
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(
            signed.Replace("5002", "5003", StringComparison.Ordinal), Now));
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(
            signed[..signed.LastIndexOf("hash=", StringComparison.Ordinal)] + "hash=" + new string('0', 64), Now));
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(
            signed + "&user=" + Uri.EscapeDataString(fields["user"]), Now));
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(
            signed + "&auth_date=" + fields["auth_date"], Now));
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(
            signed + "&hash=" + new string('0', 64), Now));
    }

    [Theory]
    [InlineData(-360, false)]
    [InlineData(-301, false)]
    [InlineData(-300, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Auth_date_has_strict_age_and_future_bounds(int offsetSeconds, bool accepted)
    {
        var validator = new MiniAppAssertionValidator(BotToken, TimeSpan.FromMinutes(5));
        var fields = ValidFields(5003);
        fields["auth_date"] = Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds().ToString();
        var initData = SignInitData(fields, BotToken);
        if (accepted) Assert.Equal(5003L, validator.Validate(initData, Now).UserId);
        else Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(initData, Now));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"id\":-1}")]
    [InlineData("{\"id\":0}")]
    [InlineData("{\"id\":9223372036854775808}")]
    public void Invalid_telegram_user_is_rejected(string userJson)
    {
        var validator = new MiniAppAssertionValidator(BotToken, TimeSpan.FromMinutes(5));
        var fields = ValidFields(5004);
        fields["user"] = userJson;
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(SignInitData(fields, BotToken), Now));
    }

    [Fact]
    public void Oversized_init_data_is_rejected_before_parsing()
    {
        var validator = new MiniAppAssertionValidator(BotToken, TimeSpan.FromMinutes(5));
        var fields = ValidFields(5005);
        fields["start_param"] = new string('x', 17 * 1024);
        Assert.Throws<UnauthorizedAccessException>(() => validator.Validate(SignInitData(fields, BotToken), Now));
    }

    [Fact]
    public async Task Telegram_session_maps_existing_numeric_identity_and_rejects_assertion_replay()
    {
        await using var f = await ApiFixture.StartAsync();
        var desktop = await f.AccountAsync("telegram", "5006");
        using var web = f.TelegramWebClient();
        var initData = SignInitData(ValidFields(5006, f.Clock.GetUtcNow()), BotToken);

        using var response = await web.PostAsync("/v1/telegram/session",
            new StringContent(initData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<TelegramWebSession>())!;
        web.DefaultRequestHeaders.Add("X-CSRF-Token", session.CsrfToken);
        var profile = await web.GetFromJsonAsync<AccountProfile>("/v1/me");
        Assert.Equal(desktop.Id, profile!.AccountId);

        using var replay = await web.PostAsync("/v1/telegram/session",
            new StringContent(initData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task New_numeric_telegram_user_maps_stably_across_fresh_assertions()
    {
        await using var f = await ApiFixture.StartAsync();
        using var web = f.TelegramWebClient();
        var firstFields = ValidFields(5010, f.Clock.GetUtcNow());
        var firstInitData = SignInitData(firstFields, BotToken);
        using var firstResponse = await web.PostAsync("/v1/telegram/session",
            new StringContent(firstInitData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstSession = (await firstResponse.Content.ReadFromJsonAsync<TelegramWebSession>())!;
        web.DefaultRequestHeaders.Add("X-CSRF-Token", firstSession.CsrfToken);
        var firstProfile = (await web.GetFromJsonAsync<AccountProfile>("/v1/me"))!;

        var secondFields = ValidFields(5010, f.Clock.GetUtcNow());
        secondFields["query_id"] = "AA-fresh-second-5010";
        var secondInitData = SignInitData(secondFields, BotToken);
        using var secondResponse = await web.PostAsync("/v1/telegram/session",
            new StringContent(secondInitData, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondSession = (await secondResponse.Content.ReadFromJsonAsync<TelegramWebSession>())!;
        web.DefaultRequestHeaders.Remove("X-CSRF-Token");
        web.DefaultRequestHeaders.Add("X-CSRF-Token", secondSession.CsrfToken);
        var secondProfile = (await web.GetFromJsonAsync<AccountProfile>("/v1/me"))!;

        Assert.Equal(firstProfile.AccountId, secondProfile.AccountId);
        Assert.Contains("telegram", secondProfile.LinkedProviders);
    }
    [Fact]
    public async Task Telegram_session_can_replace_existing_cookie_without_old_csrf()
    {
        await using var f = await ApiFixture.StartAsync();
        using var web = f.TelegramWebClient();
        var firstFields = ValidFields(5011, f.Clock.GetUtcNow());
        firstFields["query_id"] = "AA-reopen-first";
        using var first = await web.PostAsync("/v1/telegram/session",
            new StringContent(SignInitData(firstFields, BotToken), Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondFields = ValidFields(5011, f.Clock.GetUtcNow());
        secondFields["query_id"] = "AA-reopen-second";
        using var second = await web.PostAsync("/v1/telegram/session",
            new StringContent(SignInitData(secondFields, BotToken), Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
    internal static string SignInitData(Dictionary<string, string> fields, string botToken)
    {
        var check = string.Join("\n", fields.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key + "=" + x.Value));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check));
        var all = fields.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)).ToList();
        all.Add("hash=" + Convert.ToHexString(expected).ToLowerInvariant());
        return string.Join("&", all);
    }

    private static Dictionary<string, string> ValidFields(long userId, DateTimeOffset? now = null)
        => new(StringComparer.Ordinal)
        {
            ["auth_date"] = (now ?? Now).ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AA-query-" + userId,
            ["user"] = JsonSerializer.Serialize(new { id = userId, first_name = "Test" })
        };
}