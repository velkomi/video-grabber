# Third-party notices

VideoGrabber запускает сторонние инструменты как отдельные процессы. Их исполняемые файлы не хранятся в исходном Git-репозитории.

| Компонент | Назначение | Источник | Лицензия |
|---|---|---|---|
| yt-dlp | Извлечение и загрузка доступных медиапотоков | https://github.com/yt-dlp/yt-dlp | Unlicense для исходного проекта; отдельные компоненты перечислены авторами |
| FFmpeg / FFprobe | Объединение потоков, проверка и редактирование | https://github.com/BtbN/FFmpeg-Builds | GPLv3 для выбранной сборки; FFmpeg содержит компоненты с различными лицензиями |
| Deno | Изолированное выполнение JavaScript challenge для yt-dlp | https://github.com/denoland/deno | MIT |
| whisper.cpp / OpenAI Whisper model | Локальная транскрибация аудио и видео | https://github.com/ggml-org/whisper.cpp / https://huggingface.co/ggerganov/whisper.cpp | MIT для whisper.cpp; условия/лицензия модели указаны в источнике модели |
| Silero VAD (GGML) | Определение участков речи перед Whisper, снижение обработки тишины и hallucination | https://huggingface.co/ggml-org/whisper-vad | MIT |
| yt-dlp EJS | Решение JavaScript challenge поддерживаемых сайтов | https://github.com/yt-dlp/ejs | лицензия исходного проекта |
| Microsoft Windows App SDK | Пользовательский интерфейс Windows | https://github.com/microsoft/WindowsAppSDK | MIT |
| Microsoft WebView2 | Встроенный браузер | https://developer.microsoft.com/microsoft-edge/webview2/ | условия Microsoft |

Официальные Local/Managed релизы VideoGrabber являются self-contained и содержат проверенный media runtime рядом с приложением. При распространении такого архива необходимо сохранять эти уведомления и выполнять условия лицензий вложенных бинарников и моделей.
