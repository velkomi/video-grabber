# Third-party notices

VideoGrabber запускает сторонние инструменты как отдельные процессы. Их исполняемые файлы поставляются вместе с приложением в составе проверенного runtime-набора.

| Компонент | Назначение | Источник | Лицензия |
|---|---|---|---|
| yt-dlp | Извлечение и загрузка доступных медиапотоков | официальный проект yt-dlp | Исходный проект Unlicense; поставляемый Windows PyInstaller EXE содержит GPLv3+ компоненты и распространяется как GPLv3+ combined work; полный перечень в `licenses/yt-dlp-THIRD-PARTY.txt` |
| FFmpeg / FFprobe | Объединение потоков, проверка и редактирование | официальный проект FFmpeg | GPLv3 для выбранной сборки; FFmpeg содержит компоненты с различными лицензиями |
| Deno | Изолированное выполнение JavaScript challenge для yt-dlp | официальный проект Deno | MIT |
| whisper.cpp / OpenAI Whisper model | Локальная транскрибация аудио и видео | официальный проект whisper.cpp / опубликованная модель Whisper | MIT для whisper.cpp; условия/лицензия модели указаны в её источнике |
| Silero VAD (GGML) | Определение участков речи перед Whisper, снижение обработки тишины и hallucination | официальный Silero VAD runtime для whisper.cpp | MIT |
| yt-dlp EJS | Решение JavaScript challenge поддерживаемых сайтов | официальный проект yt-dlp EJS | лицензия исходного проекта |
| Microsoft Windows App SDK | Пользовательский интерфейс Windows | Microsoft | MIT |
| Microsoft WebView2 | Встроенный браузер | Microsoft | условия Microsoft |

Официальные Local/Managed релизы VideoGrabber являются self-contained и содержат проверенный media runtime рядом с приложением. При распространении такого архива необходимо сохранять эти уведомления и выполнять условия лицензий вложенных бинарников и моделей.

## Studio web interface

## Support form and notifications (2026-10-10)

- ALTCHA .NET1.1.0: MIT, official `altcha-org/altcha-lib-dotnet`, revision `c48002521c862e15cc08c2018378634203e9f925`. Full notice: `licenses/Altcha-1.1.0-MIT.txt`. Used by the API and Windows support client.
- ALTCHA browser widget3.3.0: MIT; local widget/i18n and full notice: `wwwroot/support/vendor/LICENSE.txt`. No paid CAPTCHA service connected.
- MailKit4.18.1 and MimeKit4.18.1: MIT, .NET Foundation and Contributors. Full notices: `licenses/MailKit-4.18.1-MIT.txt`, `licenses/MimeKit-4.18.1-MIT.txt`. Server email notification transport.
- BouncyCastle.Cryptography2.6.2: upstream license retained in `licenses/BouncyCastle-2.6.2-LICENSE.md`; transitive dependency of ALTCHA/MimeKit, API and Windows support client.

- Three.js: MIT; полный текст сохранён в `wwwroot/web/vendor/three.LICENSE.txt`.
- GSAP/ScrollTrigger3.15.0: GSAP Standard No Charge License; лицензия сохранена в `wwwroot/web/vendor/gsap.LICENSE.txt`. Обычная анимация сайта входит в разрешённое бесплатное использование. Это не MIT; запрет на конкурирующие визуальные конструкторы анимации сохраняется.

## Проверка конкретной сборки

Проверенные версии и SHA-256 перечисляются отдельно в инвентаризации runtime. Наличие этих уведомлений не подменяет полный комплект соответствующих исходников, сборочных материалов и лицензий зависимостей для GPL-бинарников. Коммерческий redistribution gate не считается закрытым, пока происхождение и необходимые материалы подтверждены. Оплата собственной подписки VideoGrabber не отменяет права на сторонние компоненты.

Тексты основных лицензий и URL/хэши их источников сохранены в `licenses/`. MIT-тексты для whisper.cpp/Whisper/Silero фиксируют опубликованные условия проектов, но сами по себе не доказывают точную версию или происхождение вложенной модели/бинарника. Никаких платных лицензий этим изменением не приобретается.

- Manrope and Onest variable web fonts: Google Fonts distribution, SIL Open Font License 1.1. Self-hosted WOFF2 files, licenses and source URLs/SHA-256 are retained in `wwwroot/assets/fonts/`. No external font request is made by the website.

- Bootstrap Icons: https://github.com/twbs/icons, revision `6945b7006285d444cc17ff2e22c7691719229526`, MIT. Original icon files and the full license are retained in `wwwroot/assets/icons/`.
- Telegram contact logo: unmodified `Logo.png` from the official logo archive at https://telegram.org/tour/screenshots. Telegram permits use for contact/forward buttons; the application does not represent Telegram. Source URL and SHA-256 are retained in `src/VideoGrabber.App/Assets/Icons/telegram.source.json`.
- Studio hero, plan emblems, pedestal and background: original assets generated with built-in OpenAI Image Gen for the owner-selected VideoGrabber concept on 2026-10-07. PNG originals are retained beside WebP delivery copies. The live hero and device illustration are Three.js geometry; the hero raster is used as the unsupported-WebGL/reduced-motion fallback.
