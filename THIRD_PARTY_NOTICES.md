# Third-party notices

VideoGrabber запускает сторонние инструменты как отдельные процессы. Их исполняемые файлы не хранятся в исходном Git-репозитории.

| Компонент | Назначение | Источник | Лицензия |
|---|---|---|---|
| yt-dlp | Извлечение и загрузка доступных медиапотоков | https://github.com/yt-dlp/yt-dlp | Unlicense для исходного проекта; отдельные компоненты перечислены авторами |
| FFmpeg / FFprobe | Объединение потоков, проверка и редактирование | https://github.com/BtbN/FFmpeg-Builds | GPLv3 для выбранной сборки; FFmpeg содержит компоненты с различными лицензиями |
| Deno | Изолированное выполнение JavaScript challenge для yt-dlp | https://github.com/denoland/deno | MIT |
| yt-dlp EJS | Решение JavaScript challenge поддерживаемых сайтов | https://github.com/yt-dlp/ejs | лицензия исходного проекта |
| Microsoft Windows App SDK | Пользовательский интерфейс Windows | https://github.com/microsoft/WindowsAppSDK | MIT |
| Microsoft WebView2 | Встроенный браузер | https://developer.microsoft.com/microsoft-edge/webview2/ | условия Microsoft |

Перед распространением архива с уже вложенными бинарниками необходимо сохранить уведомления и выполнить условия соответствующих лицензий. Стандартный релиз VideoGrabber содержит установщик, который получает компоненты из официальных GitHub Releases, а не перепубликует их.
