using Npgsql;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class BotInputLimiter(NpgsqlDataSource database, TimeProvider clock)
{
    public async Task<bool> AllowAsync(long senderId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var update = new NpgsqlCommand("""
            insert into support.bot_input_windows(user_id,started_at,request_count) values(@user,@now,1)
            on conflict(user_id) do update set
              started_at=case when support.bot_input_windows.started_at<=@cutoff then @now else support.bot_input_windows.started_at end,
              request_count=case when support.bot_input_windows.started_at<=@cutoff then 1 else least(31,support.bot_input_windows.request_count+1) end
            returning request_count
            """, connection);
        update.Parameters.AddWithValue("user", senderId); update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("cutoff", now.AddMinutes(-1));
        return (int)(await update.ExecuteScalarAsync(ct))! <= 30;
    }
}
