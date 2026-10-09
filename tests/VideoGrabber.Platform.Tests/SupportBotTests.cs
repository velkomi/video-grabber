using System.Text.Json;
using Npgsql;
using VideoGrabber.Platform.Api.Telegram;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupportBotTests
{
    private static Task SendAsync(ApiFixture f, string text, long user = 59991)
        => f.Service<BotCommandHandler>().HandleAsync(new TelegramUpdate(DateTime.UtcNow.Ticks,
            JsonSerializer.SerializeToElement(new { message = new { from = new { id = user, username = "synthetic_client", is_bot = false },
                chat = new { id = user, type = "private" }, text } })), CancellationToken.None);

    [Fact]
    public async Task Bot_support_mode_accepts_question_without_primary_sign_in()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        await SendAsync(f, "/support");
        await SendAsync(f, "Не удаётся сохранить видео после выбора качества.");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var count = new NpgsqlCommand("select count(*) from support.requests where source='telegram'", connection);
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Cancellation_leaves_no_support_ticket()
    {
        await using var f = await ApiFixture.StartAsync();
        f.SupportEnabled = true;
        await f.RestartAsync();
        await SendAsync(f, "/support");
        await SendAsync(f, "Отмена поддержки");
        await SendAsync(f, "Это обычный текст после отмены режима поддержки.");
        await using var connection = await f.Database.OpenConnectionAsync();
        await using var count = new NpgsqlCommand("select count(*) from support.requests", connection);
        Assert.Equal(0L, (long)(await count.ExecuteScalarAsync())!);
    }
}
