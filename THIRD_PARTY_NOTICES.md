# Third-party notices

VideoGrabber запускает сторонние инструменты как отдельные процессы. Их исполняемые файлы поставляются вместе с приложением в составе проверенного runtime-набора.

| Компонент | Назначение | Источник | Лицензия |
|---|---|---|---|
| yt-dlp | Извлечение и загрузка доступных медиапотоков | официальный проект yt-dlp | Unlicense для исходного проекта; отдельные компоненты перечислены авторами |
| FFmpeg / FFprobe | Объединение потоков, проверка и редактирование | официальный проект FFmpeg | GPLv3 для выбранной сборки; FFmpeg содержит компоненты с различными лицензиями |
| Deno | Изолированное выполнение JavaScript challenge для yt-dlp | официальный проект Deno | MIT |
| whisper.cpp / OpenAI Whisper model | Локальная транскрибация аудио и видео | официальный проект whisper.cpp / опубликованная модель Whisper | MIT для whisper.cpp; условия/лицензия модели указаны в её источнике |
| Silero VAD (GGML) | Определение участков речи перед Whisper, снижение обработки тишины и hallucination | официальный Silero VAD runtime для whisper.cpp | MIT |
| yt-dlp EJS | Решение JavaScript challenge поддерживаемых сайтов | официальный проект yt-dlp EJS | лицензия исходного проекта |
| Microsoft Windows App SDK | Пользовательский интерфейс Windows | Microsoft | MIT |
| Microsoft WebView2 | Встроенный браузер | Microsoft | условия Microsoft |

Официальные Local/Managed релизы VideoGrabber являются self-contained и содержат проверенный media runtime рядом с приложением. При распространении такого архива необходимо сохранять эти уведомления и выполнять условия лицензий вложенных бинарников и моделей.

## Studio web interface

- Manrope and Onest variable web fonts: Google Fonts distribution, SIL Open Font License 1.1. Self-hosted WOFF2 files, licenses and source URLs/SHA-256 are retained in `wwwroot/assets/fonts/`. No external font request is made by the website.

- Bootstrap Icons: https://github.com/twbs/icons, revision `6945b7006285d444cc17ff2e22c7691719229526`, MIT. Original icon files and the full license are retained in `wwwroot/assets/icons/`.
- Telegram contact logo: unmodified `Logo.png` from the official logo archive at https://telegram.org/tour/screenshots. Telegram permits use for contact/forward buttons; the application does not represent Telegram. Source URL and SHA-256 are retained in `src/VideoGrabber.App/Assets/Icons/telegram.source.json`.
- Studio hero, plan emblems, pedestal and background: original assets generated with built-in OpenAI Image Gen for the owner-selected VideoGrabber concept on 2026-10-07. PNG originals are retained beside WebP delivery copies. The live hero and device illustration are Three.js geometry; the hero raster is used as the unsupported-WebGL/reduced-motion fallback.
