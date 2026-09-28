const $ = (selector) => document.querySelector(selector);
const state = { csrf: "", profile: null, access: null };
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
  return labels[planId] || (profile?.role === "owner_admin" ? "Owner" : "Legacy");
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
  $("#profile").textContent = "ID: " + profile.accountId + "\nРоль: " + profile.role + "\nБлокировка: " + (profile.blocked ? "да" : "нет") + ".";

  if (access.unlimited) {
    $("#balance").textContent = "∞";
    $("#quota-caption").textContent = "Без лимита на отдельные загрузки";
    $("#quota-bar").style.width = "100%";
  } else if (access.planId === "start") {
    $("#balance").textContent = "10/день";
    $("#quota-caption").textContent = "Дневной лимит обновляется по UTC";
    $("#quota-bar").style.width = "100%";
  } else {
    const remaining = Math.max(0, Number(access.remainingDownloads || 0));
    $("#balance").textContent = String(remaining) + " / 10";
    $("#quota-caption").textContent = remaining > 0 ? "Бесплатные загрузки остались" : "Бесплатный лимит исчерпан";
    $("#quota-bar").style.width = String(Math.max(0, Math.min(100, (remaining / 10) * 100))) + "%";
  }

  $("#access-reason").textContent = access.reason || "—";
  $("#access-until").textContent = access.validUntil
    ? new Date(access.validUntil).toLocaleString()
    : "Без срока";
  $("#primary-provider").textContent = primaryReady
    ? "Основной вход: " + (profile.primaryAuthProvider || "owner")
    : "Основной аккаунт ещё не привязан";

  if (primaryReady && telegramLinked) {
    $("#sync-state").textContent = "Синхронизировано: Web · Windows · Telegram";
  } else if (primaryReady) {
    $("#sync-state").textContent = "Web · Windows синхронизированы";
  } else {
    $("#sync-state").textContent = "Telegram нужно связать с основным аккаунтом";
  }

  $("#link-main-account").hidden = primaryReady;
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
    const { row } = makeItem(identity.provider +
      (identity.verifiedEmail ? " • " + identity.verifiedEmail : ""));
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
        setStatus("Не удалось отвязать: " + error.message, "error");
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
    const { row } = makeItem(device.name + " • " + device.deviceId);
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
        setStatus("Не удалось отозвать: " + error.message, "error");
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
    const { row } = makeItem(destination.kind + " • " + String(destination.chatId));
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
        setStatus("Не удалось отозвать получателя: " + error.message, "error");
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
    ? "Media worker доступен."
    : "Media worker сейчас недоступен: " + capabilities.reason;
  if (analyze) analyze.disabled = !allowed;
  if (create) create.disabled = !allowed;
  const lock = $("#media-lock");
  if (!lock) return;
  if (!primaryAccountReady()) {
    lock.hidden = false;
    lock.className = "notice error";
    lock.textContent = "Telegram ещё не связан с основным VideoGrabber-аккаунтом. Отправьте /link боту @VideoGra_bot и войдите через Google или e-mail.";
  } else if (!state.access?.canDownload) {
    lock.hidden = false;
    lock.className = "notice error";
    lock.textContent = "Лимит загрузок исчерпан. Выберите подписку, чтобы продолжить.";
  } else {
    lock.hidden = true;
    lock.textContent = "";
  }
}
async function loadAll() {
  setStatus("Обновляю данные…");
  const [profile, access, identities, devices, capabilities, destinations] = await Promise.all([
    api("/v1/me"),
    api("/v1/access"),
    api("/v1/identities"),
    api("/v1/devices"),
    api("/v1/capabilities"),
    api("/v1/destinations")
  ]);
  renderProfile(profile, access);
  renderIdentities(identities);
  renderDevices(devices);
  renderCapabilities(capabilities);
  renderDestinations(destinations);
  setStatus("Данные обновлены.", "success");
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
  catch (error) { setStatus("Ошибка: " + error.message, "error"); }
});

$("#destination-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const chatText = $("#destination-chat").value.trim();
  const title = $("#destination-title").value.trim();
  if (!/^-?\d+$/.test(chatText) || !title) {
    setStatus("Укажите корректный Telegram chat ID и название.", "error");
    return;
  }
  const chatId = Number(chatText);
  if (!Number.isSafeInteger(chatId) || chatId === 0) {
    setStatus("Chat ID вне безопасного диапазона JavaScript. Используйте Telegram ID до 2^53-1.", "error");
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
      : "Не удалось привязать получателя: " + error.message, "error");
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
        account.accountId + " • " + account.role +
        (account.blocked ? " • заблокирован" : ""));
      host.append(row);
    }
  } catch (error) {
    host.textContent = error.status === 403
      ? "Admin-доступ отсутствует."
      : "Ошибка поиска: " + error.message;
  }
});

function currentAccess() { return state.access; }

window.VideoGrabberApi = {
  api,
  loadAll,
  setStatus,
  ready,
  currentAccess,
  isPrimaryAccount: primaryAccountReady
};

document.querySelectorAll("[data-tab-target]").forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll("[data-tab-target]").forEach((item) =>
      item.classList.toggle("active", item === button));
    document.querySelectorAll("[data-tab-panel]").forEach((panel) =>
      panel.classList.toggle("active", panel.id === button.dataset.tabTarget));
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
    try { webApp?.setHeaderColor?.("#0d1522"); } catch {}
    try { webApp?.setBackgroundColor?.("#0c1320"); } catch {}
    await establishSession();
    await loadAll();
    readyResolve(true);
  } catch (error) {
    readyReject(error);
    setStatus("Не удалось открыть Mini App: " + error.message, "error");
  }
}
start();
