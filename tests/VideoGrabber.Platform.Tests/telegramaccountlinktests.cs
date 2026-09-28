using System.Net;
using System.Net.Http.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using VideoGrabber.Platform.Contracts;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class TelegramAccountLinkTests
{
    [Fact]
    public async Task Temporary_telegram_account_links_to_primary_account_once_without_doubling_free()
    {
        await using var f = await ApiFixture.StartAsync();
        const long telegramUserId = 912345678;
        var source = await f.AccountAsync(
            "telegram",
            telegramUserId.ToString(),
            includeStarter: true,
            telegramOnly: true);
        var target = await f.AccountAsync(
            "email",
            "telegram-link-target",
            "linked@example.test",
            includeStarter: true);

        var service = f.Service<TelegramAccountLinkService>();
        var ticket = await service.BeginAsync(
            source.Id, telegramUserId, CancellationToken.None);

        var token = Query(ticket.LinkUri, "telegram_link");
        using var response = await target.Client.PostAsJsonAsync(
            "/v1/telegram/account-link/complete",
            new CompleteTelegramAccountLinkRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var identities = await target.Client.GetFromJsonAsync<LinkedIdentity[]>(
            "/v1/identities");
        Assert.NotNull(identities);
        Assert.Contains(
            identities!,
            identity => identity.Provider == "telegram");

        var access = await target.Client.GetFromJsonAsync<AccessSnapshot>(
            "/v1/access");
        Assert.NotNull(access);
        Assert.Equal(10, access!.RemainingDownloads);

        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            select
              (select blocked_at is not null
               from licensing.accounts
               where account_id=@source),
              (select count(*)
               from licensing.identities
               where account_id=@source),
              (select count(*)
               from licensing.identities
               where account_id=@target
                 and provider='telegram'
                 and provider_subject=@subject),
              (select coalesce(sum(void_delta),0)
               from licensing.credit_ledger
               where account_id=@source
                 and reason='telegram temporary account linked')
            """, connection))
        {
            command.Parameters.AddWithValue("source", source.Id);
            command.Parameters.AddWithValue("target", target.Id);
            command.Parameters.AddWithValue(
                "subject", telegramUserId.ToString());
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            Assert.Equal(0L, reader.GetInt64(1));
            Assert.Equal(1L, reader.GetInt64(2));
            Assert.Equal(10L, reader.GetInt64(3));
        }

        using var replay = await target.Client.PostAsJsonAsync(
            "/v1/telegram/account-link/complete",
            new CompleteTelegramAccountLinkRequest(token));
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Fact]
    public async Task Automatic_link_stops_if_temporary_account_has_nonstarter_data()
    {
        await using var f = await ApiFixture.StartAsync();
        const long telegramUserId = 998877665;
        var source = await f.AccountAsync(
            "telegram",
            telegramUserId.ToString(),
            includeStarter: true,
            telegramOnly: true);
        var target = await f.AccountAsync(
            "google",
            "telegram-link-target-financial",
            includeStarter: true);

        await using (var connection = await f.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            insert into licensing.entitlement_grants(
              grant_id,account_id,kind,source,plan_id,
              valid_from,valid_until,available,reserved,original_amount,
              reason,created_at)
            values(@grant,@account,'time','adjustment','start',
              @now,@until,0,0,0,'test nonstarter data',@now)
            """, connection))
        {
            var now = f.Clock.GetUtcNow();
            command.Parameters.AddWithValue("grant", Guid.NewGuid());
            command.Parameters.AddWithValue("account", source.Id);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("until", now.AddDays(1));
            await command.ExecuteNonQueryAsync();
        }

        var service = f.Service<TelegramAccountLinkService>();
        var ticket = await service.BeginAsync(
            source.Id, telegramUserId, CancellationToken.None);

        using var response = await target.Client.PostAsJsonAsync(
            "/v1/telegram/account-link/complete",
            new CompleteTelegramAccountLinkRequest(
                Query(ticket.LinkUri, "telegram_link")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(
            "telegram_link_reconciliation_required",
            body!["code"]);
    }

    [Fact]
    public async Task Link_start_rejects_nontelegram_origin()
    {
        await using var f = await ApiFixture.StartAsync();
        var account = await f.AccountAsync(
            "email", "not-telegram-link-source");

        await Assert.ThrowsAsync<TelegramAccountLinkConflictException>(
            () => f.Service<TelegramAccountLinkService>().BeginAsync(
                account.Id, 123456789, CancellationToken.None));
    }

    private static string Query(Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split(
                     '&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == key)
                return Uri.UnescapeDataString(
                    parts.Length == 2 ? parts[1] : string.Empty);
        }
        throw new KeyNotFoundException(key);
    }
}
