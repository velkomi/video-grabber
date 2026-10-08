using System.Text.Json;
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
    IBotApiClient bot,
    IConfiguration configuration,
    DirectDownloadService directDownloads)
{
    public async Task<bool> HandleAsync(
        long chatId,
        Guid accountId,
        string command,
        string args,
        CancellationToken cancellationToken,
        Guid? intentId = null,
        JsonElement? replyMarkup = null)
    {
        if (command is "/download" or "/course" or "/mp3"
            or "/trim" or "/join" or "/transcribe")
        {
            var profile = await accounts.ReadAsync(accountId, cancellationToken);
            if (profile is null || !AccountEligibility.CanUseProtectedDownloads(profile))
            {
                await bot.SendMessageAsync(new BotMessage(
                    chatId,
                    "Для скачивания привяжите Telegram к аккаунту VideoGrabber: /link. " +
                    "Войдите через Google или почту."),
                    cancellationToken);
                return true;
            }
        }

        if (MediaStoragePolicy.ClientOnly(configuration))
        {
            if (command == "/download")
            {
                await SendDirectAsync(chatId, accountId, args, intentId ?? Guid.NewGuid(), cancellationToken);
                return true;
            }
            if (command is "/mp3" or "/trim" or "/join" or "/transcribe" or "/course" or "/media")
            {
                await bot.SendMessageAsync(new BotMessage(chatId,
                    "В Telegram можно получить готовый MP4 до 20 МиБ: /download <ссылка>. " +
                    "Для MP3, обработки, текста и полного курса откройте VideoGrabber на Windows. " +
                    "На сайте войдите в тот же аккаунт и выберите свой компьютер.", replyMarkup), cancellationToken);
                return true;
            }
        }
        switch (command)
        {
            case "/media":
                await bot.SendMessageAsync(new BotMessage(
                    chatId,
                    "Видео: /download <ссылка>\nКурс на Windows: /course <ссылка>\n" +
                    "MP3: /mp3 <ссылка>\nОчередь: /jobs\n\n" +
                    "Обрезка (/trim), склейка (/join) и текст (/transcribe): " +
                    "выберите файлы и параметры в приложении бота или VideoGrabber на Windows.", replyMarkup), cancellationToken);
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

    private async Task SendDirectAsync(long chatId, Guid accountId, string args, Guid intentId, CancellationToken token)
    {
        var raw = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            await bot.SendMessageAsync(new BotMessage(chatId, "Отправьте /download и прямую ссылку на MP4 до 20 МиБ."), token);
            return;
        }
        try
        {
            var state = await directDownloads.SendTelegramAsync(accountId, uri, intentId, chatId, token);
            if (state != "delivered")
                await bot.SendMessageAsync(new BotMessage(chatId, state == "already_delivered"
                    ? "Файл уже отправлен."
                    : state == "pending" ? "Отправка уже обрабатывается. Подождите немного."
                    : "Не удалось подтвердить отправку. Проверьте чат: файл мог уже прийти. " +
                      "Перед новым запросом убедитесь, что его нет. Автоматического повтора не будет."), token);
        }
        catch (Exception ex) when (ex is DirectSourceUnsupportedException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            await bot.SendMessageAsync(new BotMessage(chatId,
                "Не удалось отправить видео. Нужна доступная прямая ссылка на MP4 до 20 МиБ. " +
                "Для других ссылок или больших файлов используйте VideoGrabber на Windows."), token);
        }
        catch (Exception ex) when (ex is ReservationUnavailableException or ReservationConflictException)
        {
            await bot.SendMessageAsync(new BotMessage(chatId,
                "Отправка недоступна. Проверьте тариф и предыдущую загрузку в аккаунте."), token);
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
                chatId, "Отправьте /course и ссылку на курс. Качество можно указать после ссылки, например 720p."),
                cancellationToken);
            return;
        }

        var access = await grants.EvaluateAsync(accountId, cancellationToken);
        if (!access.CanDownloadCourse)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Полный курс не входит в текущий тариф. Выберите Full Course в аккаунте."),
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
                "Компьютер пока не подключён. Откройте VideoGrabber на Windows, " +
                "войдите в тот же аккаунт и включите приём заданий с сайта и Telegram."),
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
                "Курс добавлен в очередь.\n" +
                $"Компьютер: {activeDevices[0].Name}\n" +
                $"Качество: {QualityLabel(quality)}\n" +
                (online
                    ? "VideoGrabber примет задание автоматически."
                    : "Откройте VideoGrabber на этом компьютере, чтобы начать загрузку.")),
                cancellationToken);
        }
        catch (ReservationUnavailableException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId,
                "Тариф Full Course сейчас не активен. Проверьте подписку в аккаунте."),
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
                "Не удалось добавить курс. Проверьте ссылку и доступ к курсу, затем повторите запрос."),
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
                chatId, "Укажите ссылку на видео после команды."), cancellationToken);
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
                $"В очереди: {OperationLabel(kind)}.\n" +
                $"Статус: {JobStateLabel(job.State)}\nКачество: {QualityLabel(quality)}"),
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Эта ссылка недоступна для загрузки. Проверьте ссылку и доступ к видео."), cancellationToken);
        }
        catch (ReservationUnavailableException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Загрузка недоступна по текущему тарифу или лимиту. Проверьте /subscription."), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or JobUnavailableException or ArgumentException)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Не удалось добавить загрузку. Проверьте ссылку и выбранные параметры, затем повторите запрос."), cancellationToken);
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
                "Для обрезки выберите видео, начало и длительность в приложении бота или VideoGrabber на Windows."),
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
                "Для склейки выберите минимум два видео в приложении бота или VideoGrabber на Windows."),
                cancellationToken);
            return;
        }
        var ids = new List<Guid>();
        foreach (var raw in parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(raw, out var id)) ids.Add(id);
        if (ids.Count < 2)
        {
            await bot.SendMessageAsync(new BotMessage(
                chatId, "Для склейки нужны минимум два видео. Выберите их в приложении бота или VideoGrabber на Windows."), cancellationToken);
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
                "Чтобы получить текст, выберите файл в приложении бота или VideoGrabber на Windows. " +
                "Если обработка недоступна, используйте Windows."),
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
            ? "В очереди пока ничего нет. Для видео отправьте /download и ссылку."
            : "Последние задания:\n" + string.Join("\n", rows.TakeLast(15).Select(
                (x, index) => $"{index + 1}. {OperationLabel(x.Kind)} — {JobStateLabel(x.State)}"));
        await bot.SendMessageAsync(new BotMessage(chatId, text), cancellationToken);
    }

    private static string OperationLabel(string? kind) => kind switch
    {
        "download" => "Скачивание видео",
        "course_download" => "Скачивание курса",
        "mp3" => "Создание MP3",
        "trim" => "Обрезка видео",
        "join" => "Склейка видео",
        "transcribe" => "Получение текста",
        _ => "Задание"
    };

    private static string QualityLabel(string quality)
        => quality == "best" ? "лучшее доступное" : quality;

    private static string JobStateLabel(string state) => state switch
    {
        "queued" or "pending" => "в очереди",
        "waiting_for_worker" => "ожидает запуска",
        "running" => "выполняется",
        "cancel_requested" => "отменяется",
        "review_required" => "результат требует проверки в аккаунте",
        "completed" => "готово",
        "failed" => "не удалось выполнить; проверьте ссылку и доступ",
        "canceled" or "cancelled" => "отменено",
        "paused" => "приостановлено",
        _ => "проверьте статус в аккаунте"
    };

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
