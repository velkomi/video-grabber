using VideoGrabber.Platform.Api.Jobs;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using VideoGrabber.Platform.Core.Jobs;
using VideoGrabber.Platform.Persistence;

namespace VideoGrabber.Platform.Api.Telegram;

public sealed class BotMediaHandler(
    SourceAnalysisService sources,
    JobStore jobs,
    DeviceStore devices,
    GrantStore grants,
    IAccountStore accounts,
    TimeProvider clock,
    IBotApiClient bot)
{
    public async Task<bool> HandleAsync(
        long chatId,
        Guid accountId,
        string command,
        string args,
        CancellationToken cancellationToken)
    {
        if (command is "/download" or "/course" or "/mp3"
            or "/trim" or "/join" or "/transcribe")
        {
            var profile = await accounts.ReadAsync(accountId, cancellationToken);
            if (profile is null || !AccountEligibility.CanUseProtectedDownloads(profile))
            {
                await bot.SendMessageAsync(new BotMessage(
                    chatId,
                    "Скачивание доступно после входа в основной VideoGrabber-аккаунт, " +
                    "изначально зарегистрированный через Google или e-mail. " +
                    "Войдите на сайте/в Windows VideoGrabber и привяжите Telegram к этому аккаунту."),
                    cancellationToken);
                return true;
            }
        }

        switch (command)
        {
            case "/media":
                await bot.SendMessageAsync(new BotMessage(
                    chatId,
                    "Media: /download <URL> [quality], /course <URL> [quality], /mp3 <URL> [quality], " +
                    "/trim <artifactId> <URL> <startMs> <durationMs> [quality], " +
                    "/join <artifactId1,artifactId2,...> <URL> [quality], " +
                    "/transcribe <artifactId> <URL> [quality]. " +
                    "Сложные операции также доступны в Mini App."), cancellationToken);
                return true;
            case "/jobs":
                await SendJobsAsync(chatId, accountId, cancellationToken);
                return true;
            case "/download":
                await CreateFromUrlAsync(chatId, accountId, "download", args, [], null, null, cancellationToken);
                return true;
            case "/course":
                await CreateCourseAsync(chatId, accountId, args, cancellationToken);
                return true;
            case "/mp3":
                await CreateFromUrlAsync(chatId, accountId, "mp3", args, [], null, null, cancellationToken);
                return true;
            case "/trim":
                await CreateTrimAsync(chatId, accountId, args, cancellationToken);
                return true;
            case "/join":
                await CreateJoinAsync(chatId, accountId, args, cancellationToken);
                return true;
            case "/transcribe":
                await CreateTranscribeAsync(chatId, accountId, args, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    private async Task CreateCourseAsync(
        long chatId,
        Guid accountId,
        string args,
        CancellationToken cancellationToken)
    {
        var parts = args.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1
            || !Uri.TryCreate(parts[0], UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Формат: /course <HTTP(S)-URL> [quality]."),
                cancellationToken);
            return;
        }

        var access = await grants.EvaluateAsync(accountId, cancellationToken);
        if (!access.CanDownloadCourse)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Скачивание полного курса не входит в текущий тариф. " +
                "Нужен Full Course; owner_admin имеет доступ постоянно."),
                cancellationToken);
            return;
        }

        var activeDevices = (await devices.ListAsync(accountId, cancellationToken))
            .Where(device => !device.Revoked)
            .OrderByDescending(device =>
                device.LastSeenAt is DateTimeOffset seen
                && clock.GetUtcNow() - seen < TimeSpan.FromSeconds(25))
            .ThenByDescending(device => device.LastSeenAt)
            .ToArray();
        if (activeDevices.Length == 0)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Нет зарегистрированного Windows VideoGrabber. " +
                "Войдите в Managed-приложение и включите задания с сайта и Telegram."),
                cancellationToken);
            return;
        }

        var quality = parts.Length >= 2 ? parts[1] : "best";
        try
        {
            var source = await sources.RegisterDesktopAsync(
                accountId,
                new RegisterDesktopSourceRequest(uri, quality),
                cancellationToken);
            var request = NewDesktopRequest(
                "course_download",
                activeDevices[0].DeviceId,
                source.SourceId,
                quality);
            var job = await jobs.CreateAsync(
                accountId, request, cancellationToken);
            var online = activeDevices[0].LastSeenAt is DateTimeOffset lastSeen
                && clock.GetUtcNow() - lastSeen < TimeSpan.FromSeconds(25);
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                $"Курс поставлен в очередь: {job.JobId:D}\n" +
                $"Компьютер: {activeDevices[0].Name} ({(online ? "online" : "offline")})\n" +
                $"Качество: {quality}\n" +
                (online
                    ? "VideoGrabber заберёт задание автоматически."
                    : "Загрузка начнётся, когда Windows VideoGrabber станет online.")),
                cancellationToken);
        }
        catch (ReservationUnavailableException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Сервер отклонил скачивание курса: доступ Full Course не активен."),
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException
            or InvalidDataException
            or JobUnavailableException
            or ArgumentException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Не удалось создать задание курса: " + ex.Message),
                cancellationToken);
        }
    }

    private async Task CreateFromUrlAsync(
        long chatId,
        Guid accountId,
        string kind,
        string args,
        Guid[] inputs,
        long? trimStart,
        long? trimDuration,
        CancellationToken cancellationToken)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1 || !Uri.TryCreate(parts[0], UriKind.Absolute, out var uri))
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Нужен корректный HTTP(S) URL."), cancellationToken);
            return;
        }

        try
        {
            var analyzed = await sources.AnalyzeAsync(accountId, uri, cancellationToken);
            var selected = analyzed.FirstOrDefault();
            if (selected is null)
                throw new InvalidDataException("Источник не дал media-вариантов.");
            var quality = parts.Length >= 2
                ? parts[^1]
                : selected.Qualities.FirstOrDefault() ?? "best";
            if (!selected.Qualities.Contains(quality, StringComparer.Ordinal))
                quality = selected.Qualities.FirstOrDefault() ?? "best";

            var request = NewRequest(
                kind, selected.SourceId, quality, inputs, trimStart, trimDuration);
            var job = await jobs.CreateAsync(accountId, request, cancellationToken);
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                $"Задание создано: {job.JobId:D}\nОперация: {kind}\nСостояние: {job.State}\nКачество: {quality}."),
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Источник отклонён политикой безопасности."), cancellationToken);
        }
        catch (ReservationUnavailableException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Недостаточно доступа/кредитов для задания."), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or JobUnavailableException or ArgumentException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Не удалось создать media-задание: " + ex.Message), cancellationToken);
        }
    }

    private async Task CreateTrimAsync(
        long chatId,
        Guid accountId,
        string args,
        CancellationToken cancellationToken)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4
            || !Guid.TryParse(parts[0], out var artifactId)
            || !Uri.TryCreate(parts[1], UriKind.Absolute, out _)
            || !long.TryParse(parts[2], out var start)
            || !long.TryParse(parts[3], out var duration))
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Формат: /trim <artifactId> <URL> <startMs> <durationMs> [quality]"),
                cancellationToken);
            return;
        }
        var tail = parts.Length >= 5
            ? parts[1] + " " + parts[4]
            : parts[1];
        await CreateFromUrlAsync(
            chatId, accountId, "trim", tail, [artifactId], start, duration, cancellationToken);
    }

    private async Task CreateJoinAsync(
        long chatId,
        Guid accountId,
        string args,
        CancellationToken cancellationToken)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2
            || !Uri.TryCreate(parts[1], UriKind.Absolute, out _))
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Формат: /join <artifactId1,artifactId2,...> <URL> [quality]"),
                cancellationToken);
            return;
        }
        var ids = new List<Guid>();
        foreach (var raw in parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(raw, out var id)) ids.Add(id);
        if (ids.Count < 2)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Join требует минимум два artifact ID."), cancellationToken);
            return;
        }
        var tail = parts.Length >= 3 ? parts[1] + " " + parts[2] : parts[1];
        await CreateFromUrlAsync(
            chatId, accountId, "join", tail, ids.ToArray(), null, null, cancellationToken);
    }

    private async Task CreateTranscribeAsync(
        long chatId,
        Guid accountId,
        string args,
        CancellationToken cancellationToken)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2
            || !Guid.TryParse(parts[0], out var artifactId)
            || !Uri.TryCreate(parts[1], UriKind.Absolute, out _))
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Формат: /transcribe <artifactId> <URL> [quality]. " +
                "Если server ASR недоступен, используйте Managed Desktop."),
                cancellationToken);
            return;
        }
        var tail = parts.Length >= 3 ? parts[1] + " " + parts[2] : parts[1];
        await CreateFromUrlAsync(
            chatId, accountId, "transcribe", tail, [artifactId], null, null, cancellationToken);
    }

    private async Task SendJobsAsync(
        long chatId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var rows = await jobs.ListAsync(accountId, cancellationToken);
        var text = rows.Count == 0
            ? "Media-заданий пока нет."
            : string.Join("\n", rows.TakeLast(15).Select(
                x => $"{x.JobId:D} • {x.State} • {x.Reason}"));
        await bot.SendMessageAsync(new BotMessage(chatId, text), cancellationToken);
    }

    private static CreateJob NewDesktopRequest(
        string kind,
        Guid deviceId,
        string sourceId,
        string quality)
    {
        var request = new CreateJob(
            Guid.NewGuid(),
            string.Empty,
            kind,
            "desktop_worker",
            deviceId,
            sourceId,
            quality,
            [],
            null,
            null);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }

    private static CreateJob NewRequest(
        string kind,
        string sourceId,
        string quality,
        Guid[] inputs,
        long? trimStart,
        long? trimDuration)
    {
        var request = new CreateJob(
            Guid.NewGuid(),
            string.Empty,
            kind,
            "server_worker",
            null,
            sourceId,
            quality,
            inputs,
            trimStart,
            trimDuration);
        return request with { RequestHash = JobRequestHasher.Hash(request) };
    }
}
