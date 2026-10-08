(() => {
  "use strict";
  const guides = {
  "link": {
    "title": "Шаг 1. Скопируйте ссылку",
    "lead": "Откройте видео и скопируйте его адрес через «Поделиться» или из адресной строки.",
    "sections": [
      [
        "Вставьте адрес",
        "Перейдите к загрузкам и вставьте ссылку на конкретный ролик. Для курса используйте страницу с уроками."
      ],
      [
        "Закрытые страницы",
        "Откройте источник во встроенном браузере Windows-приложения и войдите в свой аккаунт на этом сайте."
      ],
      [
        "Если ссылка не открывается",
        "Проверьте адрес и доступ к видео. Если источник временно недоступен, попробуйте позже."
      ]
    ],
    "action": "Перейти к загрузкам",
    "href": "#app"
  },
  "format": {
    "title": "Шаг 2. Выберите формат",
    "lead": "Сохраните видео, только звук или полный курс.",
    "sections": [
      [
        "Видео MP4",
        "Ролик с изображением и звуком. Доступное качество зависит от исходного видео."
      ],
      [
        "Аудио MP3",
        "Только звуковая дорожка — удобно для лекций, интервью и музыки. Для обработки используйте Windows-приложение и подходящий тариф."
      ],
      [
        "Полный курс",
        "Уроки и материалы с сохранением структуры. Понадобятся Windows-приложение, тариф Full Course и доступ к курсу на исходном сайте."
      ],
      [
        "Как выбрать тариф",
        "Free — 10 видео навсегда. Start — до 10 загрузок в сутки. Unlimited Video — отдельные видео без лимита. Full Course — полные курсы. Возможности каждого тарифа указаны в его карточке."
      ]
    ],
    "action": "Посмотреть тарифы",
    "href": "#pricing"
  },
  "file": {
    "title": "Шаг 3. Сохраните файл",
    "lead": "Следите за ходом загрузки в списке заданий.",
    "sections": [
      [
        "В браузере",
        "Нажмите «Скачать» у готового результата. Если открылся плеер, выберите «Сохранить видео». Файл попадёт в выбранную вами папку."
      ],
      [
        "В Windows",
        "Выберите папку в приложении. Для заданий с сайта оставьте выбранный компьютер и VideoGrabber включёнными."
      ],
      [
        "В Telegram",
        "Готовое видео появится в диалоге с ботом. Если для ссылки требуется Windows-приложение, бот подскажет следующий шаг."
      ]
    ],
    "action": "Открыть загрузки",
    "href": "#app"
  },
  "windows": {
    "title": "VideoGrabber для Windows",
    "lead": "Видео, MP3 и курсы — с сохранением в удобную папку.",
    "sections": [
      [
        "Установка",
        "Скачайте установщик. Для запуска без установки распакуйте Portable ZIP целиком."
      ],
      [
        "Общий аккаунт",
        "Войдите через Google или почту, как на сайте. Подтвердите вход в браузере и вернитесь в приложение."
      ],
      [
        "Курсы и закрытые страницы",
        "Откройте источник во встроенном браузере и войдите в свой аккаунт на этом сайте. Для полного курса нужен тариф Full Course."
      ]
    ],
    "action": "Скачать для Windows",
    "href": "#download"
  },
  "web": {
    "title": "Один аккаунт — везде",
    "lead": "Ваш тариф, лимиты и история заданий доступны на сайте, в Windows и Telegram.",
    "sections": [
      [
        "Вход",
        "Используйте Google или почту. Для почты придёт одноразовая ссылка — пароль не нужен."
      ],
      [
        "Windows",
        "Войдите тем же способом в приложении. После входа компьютер появится в списке устройств на сайте."
      ],
      [
        "Telegram",
        "Откройте @VideoGra_bot, отправьте /link и войдите в тот же аккаунт на открывшейся странице."
      ]
    ],
    "action": "Войти в аккаунт",
    "href": "#app"
  },
  "telegram": {
    "title": "VideoGrabber в Telegram",
    "lead": "Отправьте ссылку боту или откройте Mini App.",
    "sections": [
      [
        "Подключите аккаунт",
        "Отправьте @VideoGra_bot команду /link. Откройте полученную ссылку и войдите через Google или почту, как на сайте."
      ],
      [
        "Получите видео",
        "Отправьте ссылку на видео прямо в диалог с ботом. Если готовый файл доступен, он появится в этом диалоге. Для других ссылок бот предложит Windows-приложение."
      ],
      [
        "Меню под рукой",
        "Кнопки помогут открыть аккаунт, загрузки и помощь. Меню можно скрыть и вернуть командой /menu."
      ]
    ],
    "action": "Открыть Telegram",
    "href": "https://t.me/VideoGra_bot"
  }
};
  const topics = {"workflow-url":"link","workflow-format":"format","workflow-download":"file",
    "device-windows":"windows","windows-app":"windows","device-web":"web","device-telegram":"telegram"};
  let opener = null;
  let currentTopic = null;
  function openInfo(topic, source) {
    const guide = guides[topic];
    if (!guide) return;
    const dialog = document.querySelector("#info-dialog");
    opener = source || document.activeElement;
    currentTopic = topic;
    document.querySelector("#info-dialog-title").textContent = guide.title;
    document.querySelector("#info-dialog-lead").textContent = guide.lead;
    const body = document.querySelector("#info-dialog-content");
    body.replaceChildren();
    for (const [title, text] of guide.sections) {
      const section = document.createElement("section");
      const heading = document.createElement("h3"); heading.textContent = title;
      const paragraph = document.createElement("p"); paragraph.textContent = text;
      section.append(heading, paragraph); body.append(section);
    }
    const action = document.querySelector("#info-dialog-action");
    action.textContent = guide.action; action.setAttribute("href", guide.href);
    if (!dialog.open) dialog.showModal();
    dialog.scrollTop = 0;
  }
  function activate(feature, source) {
    if (typeof feature !== "string") return;
    if (feature.startsWith("pricing-")) {
      const plan = feature.slice(8);
      if (["free","start","unlimited_video","full_course"].includes(plan)) window.openPlanDialog(plan);
    } else if (topics[feature]) openInfo(topics[feature], source);
  }
  document.addEventListener("DOMContentLoaded", () => {
    const dialog = document.querySelector("#info-dialog");
    const close = () => { if (dialog.open) dialog.close(); };
    for (const id of ["info-dialog-x","info-dialog-close","info-dialog-action"])
      document.querySelector("#" + id).addEventListener("click", close);
    dialog.addEventListener("click", event => { if (event.target === dialog) close(); });
    dialog.addEventListener("close", () => {
      const fallback = document.querySelector(`[data-info="${currentTopic}"]`) || document.querySelector("#header-login");
      (opener?.getClientRects().length ? opener : fallback)?.focus({preventScroll:true});
    });
    for (const button of document.querySelectorAll("[data-info]"))
      button.addEventListener("click", () => openInfo(button.dataset.info, button));
    const visual = document.querySelector("#hero-visual");
    for (const button of document.querySelectorAll(".scene-control")) {
      button.addEventListener("click", () => activate(button.dataset.feature, button));
      const focus = () => visual.dispatchEvent(new CustomEvent("videograbber:scene-focus", {detail:{feature:button.dataset.feature}}));
      button.addEventListener("pointerenter", focus); button.addEventListener("focus", focus);
      const blur = () => visual.dispatchEvent(new CustomEvent("videograbber:scene-focus", {detail:{feature:null}}));
      button.addEventListener("pointerleave", blur); button.addEventListener("blur", blur);
    }
    visual.addEventListener("videograbber:scene-action", event => {
      const feature = event.detail?.feature;
      const button = [...document.querySelectorAll(".scene-control")].find(node => node.dataset.feature === feature && !node.hidden);
      if (button) { button.focus({preventScroll:true}); button.click(); }
    });
  }, {once:true});
  window.VGSceneActions = Object.freeze({guides, activate});
})();
