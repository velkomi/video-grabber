using VideoGrabber.Core.Downloads;
using VideoGrabber.Core.Security;

namespace VideoGrabber.Infrastructure.Downloads;

internal static class DownloadFailureFormatter
{
    public static DownloadResult Create(Uri source, string standardError)
    {
        var lines = standardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var raw = lines.LastOrDefault(x => x.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            ?? lines.LastOrDefault() ?? "Компонент завершился с ошибкой без описания.";
        bool Has(params string[] values) => values.Any(x => raw.Contains(x, StringComparison.OrdinalIgnoreCase));
        var (code, title, help) = Has("CERTIFICATE_VERIFY_FAILED", "certificate verify failed")
            ? ("TLS_FAILURE", "Не удалось проверить защищённое соединение.", "Проверьте дату Windows и сертификат сайта. Не отключайте проверку сертификата и не вводите пароль на небезопасной странице.")
            : Has("getaddrinfo failed", "failed to resolve", "Name or service not known", "No such host")
            ? ("DNS_FAILURE", "Не удалось найти адрес сайта.", "Проверьте открытие сайта в браузере и работу DNS или VPN. Повторный вход не исправляет ошибку поиска адреса.")
            : Has("timed out", "timeout", "WinError 10060")
            ? ("NETWORK_TIMEOUT", "Сайт не ответил вовремя.", "Проверьте, открывается ли сайт в обычном браузере через то же подключение. Возможны проблемы сайта, сети или VPN. Cookies не устраняют тайм-аут соединения; менять VPN программа сама не будет.")
            : Has("Connection reset", "Connection refused", "Failed to connect", "WinError 10054", "WinError 10061")
            ? ("CONNECTION_FAILED", "Соединение с сайтом не установлено или прервано.", "Проверьте доступность сайта и настройки сети или VPN. Сначала должно восстановиться соединение, затем можно проверять вход и видео.")
            : Has("HTTP Error 401", "login required", "Sign in to", "You need to log in")
            ? ("LOGIN_REQUIRED", "Сайт запросил вход в аккаунт.", "Откройте ссылку во встроенном браузере и войдите на самом сайте. Выберите «Встроенный браузер — только эта загрузка» и повторите. Пароль в журнал не передаётся.")
            : Has("HTTP Error 403", "HTTP Error 429", "IP address is blocked")
            ? ("ACCESS_DENIED", "Сайт отказал в доступе или ограничил запросы.", "Причиной могут быть права на урок, ограничение запросов, сессия или IP-адрес. Это не доказательство неправильного пароля. Проверьте воспроизведение в браузере; не повторяйте запросы бесконечно.")
            : Has("HTTP Error 404", "HTTP Error 410")
            ? ("NOT_FOUND", "Страница не найдена или больше недоступна.", "Проверьте ссылку в личном кабинете школы. Повторная установка видеокомпонентов не восстанавливает удалённую страницу.")
            : Has("Unsupported URL", "No video formats found")
            ? ("UNSUPPORTED_PAGE", "Загрузчик не нашёл поддерживаемое видео на странице.", "Откройте урок во встроенном браузере, войдите при необходимости и запустите видео. Затем выберите найденный поток. Наличие страницы ещё не подтверждает возможность скачать видео.")
            : ("DOWNLOAD_FAILED", "Не удалось завершить загрузку.", "Техническая причина приведена ниже и записана в полный журнал. Проверьте страницу в браузере; не передавайте пароли или cookies в переписку.");
        var scoped = source.Query.Length > 1
            ? raw.Replace(source.Query[1..], "[QUERY REDACTED]", StringComparison.Ordinal) : raw;
        var technical = SensitiveDataRedactor.Redact(scoped);
        if (technical.Length > 4000) technical = technical[..4000] + " [обрезано]";
        return new(false, $"{title} [{code}]", Details: $"Сайт: {source.IdnHost}\n{help}\n\nТехническая причина:\n{technical}");
    }
}
