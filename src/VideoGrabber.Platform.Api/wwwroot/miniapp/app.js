const $ = (selector) => document.querySelector(selector);
const state = { csrf: "", profile: null, access: null };
const accountRefreshListeners = new Set();
let readyResolve;
let readyReject;
const ready = new Promise((resolve, reject) => {
  readyResolve = resolve;
  readyReject = reject;
});

function setStatus(message, kind = "") {
  const node = $("#status");
  node.textContent = message;
  node.className = "status" + (kind ? " " + kind : "");
}

async function api(path, options = {}) {
  const method = (options.method || "GET").toUpperCase();
  const headers = new Headers(options.headers || {});
  headers.set("Accept", "application/json");
  if (method !== "GET" && state.csrf) headers.set("X-CSRF-Token", state.csrf);
  const response = await fetch(path, {
    credentials: "same-origin",
    ...options,
    method,
    headers
  });
  if (!response.ok) {
    const error = new Error("HTTP " + response.status);
    error.status = response.status;
    throw error;
  }
  if (response.status === 204) return null;
  return response.json();
}

function userError(error) {
  if (error?.status === 401) return "Откройте приложение заново через Telegram.";
  if (error?.status === 403) return "Для этого действия недостаточно прав.";
  if (error?.status === 429) return "Слишком много запросов. Попробуйте чуть позже.";
  if (error?.status === 503) return "Сервис временно недоступен. Попробуйте позже.";
  return "Не удалось выполнить действие. Попробуйте ещё раз.";
}

function providerLabel(provider) {
  return { google: "Google", email: "Электронная почта", telegram: "Telegram", owner: "Основной аккаунт" }[provider] || "Связанный аккаунт";
}

function googleMark() {
  const badge = document.createElement("span");
  badge.className = "google-mark-badge";
  badge.setAttribute("aria-hidden", "true");
  const icon = document.createElement("img");
  icon.className = "google-mark";
  icon.src = "/assets/icons/google-g.png";
  icon.alt = "";
  icon.width = 20;
  icon.height = 20;
  badge.append(icon);
  return badge;
}

function destinationLabel(destination) {
  return destination.title || { private: "Личный чат", group: "Группа", supergroup: "Группа", channel: "Канал" }[destination.kind] || "Telegram-чат";
}

function makeItem(text) {
  const row = document.createElement("div");
  row.className = "item";
  const label = document.createElement("span");
  label.className = "item-text";
  label.textContent = text;
  row.append(label);
  return { row, label };
}

function planLabel(planId, profile) {
  const labels = { free: "Free", start: "Start", unlimited_video: "Unlimited Video", full_course: "Full Course" };
  return labels[planId] || (profile?.role === "owner_admin" ? "Полный доступ" : "Ваш тариф");
}

function primaryAccountReady(profile = state.profile) {
  return profile?.role === "owner_admin"
    || profile?.primaryAuthProvider === "google"
    || profile?.primaryAuthProvider === "email";
}

function renderProfile(profile, access) {
  state.profile = profile;
  state.access = access;
  const plan = planLabel(access.planId, profile);
  const primaryReady = primaryAccountReady(profile);
  const telegramLinked = (profile.linkedProviders || [])
    .some((provider) => String(provider).toLowerCase() === "telegram");

  $("#plan-chip").textContent = plan;
  $("#profile").textContent = profile.blocked
    ? "Доступ к аккаунту ограничен. Обратитесь в поддержку."
    : primaryReady ? "Ваш тариф работает в Telegram, на сайте и в Windows."
    : "Свяжите аккаунт, чтобы пользоваться одним тарифом на всех устройствах.";

  if (access.unlimited) {
    $("#balance").textContent = "∞";
    $("#quota-caption").textContent = "Без лимита на отдельные загрузки";
    $("#quota-bar").style.width = "100%";
  } else if (access.planId === "start") {
    $("#balance").textContent = "10/день";
    $("#quota-caption").textContent = "До 10 загрузок в сутки";
    $("#quota-bar").style.width = "100%";
  } else {
    const remaining = Math.max(0, Number(access.remainingDownloads || 0));
    $("#balance").textContent = String(remaining) + " / 10";
    $("#quota-caption").textContent = remaining > 0 ? "Бесплатные загрузки остались" : "Бесплатный лимит исчерпан";
    $("#quota-bar").style.width = String(Math.max(0, Math.min(100, (remaining / 10) * 100))) + "%";
  }

  $("#access-reason").textContent = profile.blocked ? "Доступ ограничен" : !primaryReady ? "Свяжите аккаунт" : access.canDownload ? "Можно загружать" : "Загрузки недоступны";
  $("#access-until").textContent = access.validUntil
    ? new Date(access.validUntil).toLocaleString()
    : "Без срока";
  $("#primary-provider").textContent = primaryReady
    ? providerLabel(profile.primaryAuthProvider || "owner")
    : "Основной аккаунт ещё не привязан";
  $("#primary-provider").classList.toggle("provider-label", primaryReady && profile.primaryAuthProvider === "google");
  if (primaryReady && profile.primaryAuthProvider === "google") {
    $("#primary-provider").prepend(googleMark());
  }

  if (primaryReady && telegramLinked) {
    $("#sync-state").textContent = "Аккаунт связан";
  } else if (primaryReady) {
    $("#sync-state").textContent = "Аккаунт подключён";
  } else {
    $("#sync-state").textContent = "Свяжите аккаунт";
  }

  $("#link-main-account").hidden = primaryReady;
  $("#link-account-hint").hidden = primaryReady;
  document.body.dataset.primaryAccount = primaryReady ? "ready" : "link-required";
  $("#admin").hidden = profile.role !== "owner_admin";
}

function renderIdentities(identities) {
  const host = $("#providers");
  host.replaceChildren();
  if (identities.length === 0) {
    host.textContent = "Нет связанных способов входа.";
    return;
  }
  for (const identity of identities) {
    const { row, label } = makeItem(providerLabel(identity.provider) +
      (identity.verifiedEmail ? " • " + identity.verifiedEmail : ""));
    if (identity.provider === "google") {
      const text = document.createElement("span");
      text.textContent = label.textContent;
      label.classList.add("provider-label");
      label.replaceChildren(googleMark(), text);
    }
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "Отвязать";
    button.disabled = identities.length <= 1;
    button.addEventListener("click", async () => {
      try {
        setStatus("Отвязываю способ входа…");
        await api("/v1/identities/" + encodeURIComponent(identity.identityId),
          { method: "DELETE" });
        await loadAll();
        setStatus("Способ входа отвязан.", "success");
      } catch (error) {
        setStatus("Не удалось отвязать: " + userError(error), "error");
      }
    });
    row.append(button);
    host.append(row);
  }
}

function renderDevices(devices) {
  const host = $("#devices");
  host.replaceChildren();
  const active = devices.filter((device) => !device.revoked);
  if (active.length === 0) {
    host.textContent = "Активных компьютеров нет.";
    return;
  }
  for (const device of active) {
    const { row } = makeItem(device.name || "Компьютер Windows");
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "Отозвать";
    button.addEventListener("click", async () => {
      try {
        setStatus("Отзываю устройство…");
        await api("/v1/devices/" + encodeURIComponent(device.deviceId) + "/revoke",
          { method: "POST" });
        await loadAll();
        setStatus("Устройство отозвано.", "success");
      } catch (error) {
        setStatus("Не удалось отозвать: " + userError(error), "error");
      }
    });
    row.append(button);
    host.append(row);
  }
}

function renderDestinations(destinations) {
  const host = $("#destinations");
  host.replaceChildren();
  const active = destinations.filter((destination) => !destination.revoked);
  if (active.length === 0) {
    host.textContent = "Проверенных получателей пока нет.";
    return;
  }
  for (const destination of active) {
    const { row } = makeItem(destinationLabel(destination));
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "Отозвать";
    button.addEventListener("click", async () => {
      try {
        setStatus("Отзываю получателя…");
        await api("/v1/destinations/" + encodeURIComponent(destination.destinationId) + "/revoke",
          { method: "POST" });
        await loadAll();
        setStatus("Получатель отозван.", "success");
      } catch (error) {
        setStatus("Не удалось отозвать получателя: " + userError(error), "error");
      }
    });
    row.append(button);
    host.append(row);
  }
}
function renderCapabilities(capabilities) {
  const analyze = $("#media-analyze");
  const create = $("#media-create");
  const allowed = capabilities.mediaAvailable && primaryAccountReady()
    && state.access?.canDownload === true;
  $("#capability").textContent = capabilities.mediaAvailable
    ? capabilities.reason === "client_only_media" ? "Отправьте MP4 в чат или загрузите видео в Windows." : "Готово к загрузке."
    : "Загрузки временно недоступны. Попробуйте позже.";
  if (analyze) analyze.disabled = !allowed;
  if (create) create.disabled = !allowed;
  const lock = $("#media-lock");
  if (!lock) return;
  if (!primaryAccountReady()) {
    lock.hidden = false;
    lock.className = "notice error";
    lock.textContent = "Свяжите аккаунт: отправьте /link боту @VideoGra_bot и войдите через Google или почту.";
  } else if (!state.access?.canDownload) {
    lock.hidden = false;
    lock.className = "notice error";
    lock.textContent = "Лимит загрузок исчерпан. Выберите подписку, чтобы продолжить.";
  } else {
    lock.hidden = true;
    lock.textContent = "";
  }
}
function onAccountRefresh(handler) {
  accountRefreshListeners.add(handler);
  return () => accountRefreshListeners.delete(handler);
}
async function loadAll() {
  const version = state.loadVersion = (state.loadVersion || 0) + 1;
  const notify = async phase => {
    const event = { phase, version, accountId: phase === "loaded" ? state.profile?.accountId : null };
    await Promise.allSettled([...accountRefreshListeners].map(handler => handler(event)));
  };
  await notify("loading");
  if (version !== state.loadVersion) return;
  setStatus("Обновляю данные…");
  const [profile, access, identities, devices, capabilities, destinations] = await Promise.all([
    api("/v1/me"),
    api("/v1/access"),
    api("/v1/identities"),
    api("/v1/devices"),
    api("/v1/capabilities"),
    api("/v1/destinations")
  ]);
  if (version !== state.loadVersion) return;
  renderProfile(profile, access);
  renderIdentities(identities);
  renderDevices(devices);
  renderCapabilities(capabilities);
  renderDestinations(destinations);
  await notify("loaded");
  if (version === state.loadVersion) setStatus("Данные обновлены.", "success");
}

async function establishSession() {
  const initData = window.Telegram && window.Telegram.WebApp
    ? window.Telegram.WebApp.initData
    : "";
  if (!initData) throw new Error("Telegram initData отсутствует");
  const response = await fetch("/v1/telegram/session", {
    method: "POST",
    headers: { "Content-Type": "text/plain;charset=utf-8" },
    body: initData,
    credentials: "same-origin"
  });
  if (!response.ok) throw new Error("Telegram session: HTTP " + response.status);
  const session = await response.json();
  state.csrf = session.csrfToken;
  if (!state.csrf) throw new Error("CSRF token отсутствует");
}
$("#refresh").addEventListener("click", async () => {
  try { await loadAll(); }
  catch (error) { setStatus("Ошибка: " + userError(error), "error"); }
});

$("#destination-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const chatText = $("#destination-chat").value.trim();
  const title = $("#destination-title").value.trim();
  if (!/^-?\d+$/.test(chatText) || !title) {
    setStatus("Укажите числовой номер чата и название.", "error");
    return;
  }
  const chatId = Number(chatText);
  if (!Number.isSafeInteger(chatId) || chatId === 0) {
    setStatus("Проверьте номер чата: он указан неверно.", "error");
    return;
  }
  try {
    setStatus("Проверяю права Telegram…");
    const challenge = await api("/v1/destinations/challenges", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ chatId })
    });
    await api("/v1/destinations", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ chatId, title, proof: challenge.proof })
    });
    $("#destination-title").value = "";
    await loadAll();
    setStatus("Получатель проверен и привязан.", "success");
  } catch (error) {
    setStatus(error.status === 403
      ? "Нет подтверждённых прав пользователя или бота на этот чат."
      : "Не удалось привязать получателя: " + userError(error), "error");
  }
});
$("#admin-search").addEventListener("submit", async (event) => {
  event.preventDefault();
  const identity = $("#admin-identity").value.trim();
  const host = $("#admin-results");
  host.replaceChildren();
  if (!identity) return;
  try {
    const results = await api("/v1/admin/accounts?identity=" + encodeURIComponent(identity));
    if (results.length === 0) {
      host.textContent = "Аккаунт не найден.";
      return;
    }
    for (const account of results) {
      const { row } = makeItem(
        "Аккаунт найден • " + (account.blocked ? "доступ ограничен" : "доступ открыт"));
      host.append(row);
    }
  } catch (error) {
    host.textContent = error.status === 403
      ? "Доступ к управлению ограничен."
      : "Ошибка поиска: " + userError(error);
  }
});

function currentAccess() { return state.access; }

window.VideoGrabberApi = {
  api,
  loadAll,
  onAccountRefresh,
  currentAccountId: () => state.profile?.accountId || null,
  setStatus,
  ready,
  currentAccess,
  userError,
  providerLabel,
  destinationLabel,
  isPrimaryAccount: primaryAccountReady
};

const tabs = [...document.querySelectorAll("[data-tab-target]")];
function selectTab(button) {
  tabs.forEach((item) => {
    const selected = item === button;
    item.classList.toggle("active", selected);
    item.setAttribute("aria-selected", String(selected));
    item.tabIndex = selected ? 0 : -1;
  });
  document.querySelectorAll("[data-tab-panel]").forEach((panel) => {
    const selected = panel.id === button.dataset.tabTarget;
    panel.classList.toggle("active", selected);
    panel.hidden = !selected;
  });
}
tabs.forEach((button, index) => {
  button.addEventListener("click", () => selectTab(button));
  button.addEventListener("keydown", (event) => {
    let next;
    if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
    if (event.key === "ArrowLeft") next = (index + tabs.length - 1) % tabs.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = tabs.length - 1;
    if (next === undefined) return;
    event.preventDefault();
    selectTab(tabs[next]);
    tabs[next].focus();
  });
});

$("#link-main-account").addEventListener("click", () => {
  const webApp = window.Telegram?.WebApp;
  if (webApp?.openTelegramLink) webApp.openTelegramLink("https://t.me/VideoGra_bot");
  else location.href = "https://t.me/VideoGra_bot";
});

async function start() {
  try {
    const webApp = window.Telegram?.WebApp;
    webApp?.ready();
    webApp?.expand();
    try { webApp?.setHeaderColor?.("#060b17"); } catch {}
    try { webApp?.setBackgroundColor?.("#060b17"); } catch {}
    await establishSession();
    await loadAll();
    readyResolve(true);
  } catch (error) {
    readyReject(error);
    setStatus(window.Telegram?.WebApp?.initData ? "Не удалось подключить аккаунт. Откройте приложение заново через Telegram." : "Откройте VideoGrabber через бота @VideoGra_bot в Telegram.", "error");
  }
}
start();
