"use strict";

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];
const queryParams = new URLSearchParams(location.search);
const visualTestMode = queryParams.get("visualTest") === "1";
const perfDebugMode = queryParams.get("perfDebug") === "1";
window.__VG_VISUAL_TEST = visualTestMode;
window.__VG_WEB_VITALS = {
  lcp: null,
  cls: 0,
  inp: null
};
if (visualTestMode)
  document.documentElement.classList.add("visual-test");

const keys = {
  access: "vg_web_access",
  refresh: "vg_web_refresh",
  auth: "vg_web_auth",
  desktop: "vg_desktop_auth",
  telegramLink: "vg_telegram_account_link"
};

const state = {
  profile: null,
  access: null,
  identities: [],
  devices: [],
  jobs: [],
  subscriptions: [],
  paymentProducts: null,
  refreshing: null
};

function setStatus(selector, text, kind = "") {
  const node = $(selector);
  if (!node) return;
  node.textContent = text || "";
  node.className = "status" + (kind ? " " + kind : "");
}

function accessToken() {
  return sessionStorage.getItem(keys.access) || "";
}

function refreshToken() {
  return sessionStorage.getItem(keys.refresh) || "";
}

function saveSession(session) {
  sessionStorage.setItem(keys.access, session.accessToken);
  sessionStorage.setItem(keys.refresh, session.refreshToken);
}

function clearSession() {
  sessionStorage.removeItem(keys.access);
  sessionStorage.removeItem(keys.refresh);
  sessionStorage.removeItem(keys.auth);
  state.profile = null;
  state.access = null;
  state.identities = [];
  state.devices = [];
  state.jobs = [];
  state.subscriptions = [];
}

async function refreshSession() {
  if (state.refreshing) return state.refreshing;
  const token = refreshToken();
  if (!token) throw new Error("Сессия отсутствует.");

  state.refreshing = (async () => {
    const response = await fetch("/v1/auth/refresh", {
      method: "POST",
      headers: {
        "Accept": "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ refreshToken: token })
    });
    if (!response.ok) {
      clearSession();
      throw new Error("Сессия истекла.");
    }
    const session = await response.json();
    saveSession(session);
    return session.accessToken;
  })().finally(() => {
    state.refreshing = null;
  });

  return state.refreshing;
}

async function api(path, options = {}, retry = true) {
  const headers = new Headers(options.headers || {});
  headers.set("Accept", "application/json");
  if (accessToken()) headers.set("Authorization", "Bearer " + accessToken());

  const response = await fetch(path, {
    ...options,
    headers
  });

  if (response.status === 401 && retry && refreshToken()) {
    await refreshSession();
    return api(path, options, false);
  }

  if (!response.ok) {
    let detail = "";
    try {
      const body = await response.json();
      detail = body.detail || body.code || body.title || "";
    } catch {}
    const error = new Error(detail || ("HTTP " + response.status));
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) return null;
  return response.json();
}

let supabaseAuthConfig = null;

async function getSupabaseAuthConfig() {
  if (supabaseAuthConfig) return supabaseAuthConfig;
  const response = await fetch("/v1/auth/supabase-config", {
    headers: { "Accept": "application/json" },
    cache: "no-store"
  });
  if (!response.ok) throw new Error("Авторизация VideoGrabber пока не настроена.");
  supabaseAuthConfig = await response.json();
  return supabaseAuthConfig;
}

function authRedirectUri(cfg) {
  const redirect = new URL(cfg.redirectUri);

  const telegram = pendingTelegramAccountLink();
  if (telegram?.token)
    redirect.searchParams.set("telegram_link", telegram.token);

  const desktop = pendingDesktopFlow();
  if (desktop?.flowId && desktop?.state) {
    redirect.searchParams.set("desktop_flow", desktop.flowId);
    redirect.searchParams.set("desktop_state", desktop.state);
  }

  return redirect.href;
}

async function beginGoogleSignIn() {
  setStatus("#auth-status", "Перенаправляю в Google…");
  const cfg = await getSupabaseAuthConfig();
  if (!cfg.googleEnabled) throw new Error("Google-вход сейчас недоступен.");
  const target =
    cfg.url.replace(/\/$/u, "") +
    "/auth/v1/authorize?provider=google&redirect_to=" +
    encodeURIComponent(authRedirectUri(cfg));
  location.assign(target);
}

async function sendMagicLink(email) {
  const cfg = await getSupabaseAuthConfig();
  if (!cfg.emailEnabled) throw new Error("E-mail вход сейчас недоступен.");
  const normalized = String(email || "").trim().toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/u.test(normalized))
    throw new Error("Проверьте адрес e-mail.");

  const response = await fetch(
    cfg.url.replace(/\/$/u, "") +
      "/auth/v1/otp?redirect_to=" +
      encodeURIComponent(authRedirectUri(cfg)),
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "apikey": cfg.publishableKey,
        "Authorization": "Bearer " + cfg.publishableKey
      },
      body: JSON.stringify({
        email: normalized,
        create_user: true,
        data: { product: "videograbber" }
      })
    }
  );
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const message =
      body.msg || body.message || body.error_description || body.error ||
      "Не удалось отправить ссылку.";
    throw new Error(message);
  }
}

async function completeSupabaseCallback() {
  if (!location.hash || location.hash === "#app") return false;
  const params = new URLSearchParams(location.hash.slice(1));
  const error = params.get("error_description") || params.get("error");
  if (error) {
    history.replaceState({}, "", "/web/#app");
    throw new Error(error);
  }

  const accessToken = params.get("access_token");
  if (!accessToken) return false;

  const response = await fetch("/v1/auth/supabase-session", {
    method: "POST",
    headers: {
      "Accept": "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ accessToken })
  });
  history.replaceState({}, "", "/web/#app");
  if (!response.ok)
    throw new Error("VideoGrabber не подтвердил Supabase-сессию.");

  const session = await response.json();
  saveSession(session);
  return true;
}

function captureTelegramAccountLink() {
  const query = new URLSearchParams(location.search);
  const token = query.get("telegram_link");
  if (!token) return;

  localStorage.setItem(keys.telegramLink, JSON.stringify({
    token,
    expiresAt: Date.now() + 5 * 60 * 1000
  }));
  query.delete("telegram_link");
  const rest = query.toString();
  history.replaceState(
    {},
    "",
    "/web/" + (rest ? "?" + rest : "") + (location.hash || "")
  );
}

function pendingTelegramAccountLink() {
  try {
    const raw = localStorage.getItem(keys.telegramLink);
    if (!raw) return null;
    const value = JSON.parse(raw);
    if (!value
        || !value.token
        || Number(value.expiresAt || 0) <= Date.now()) {
      localStorage.removeItem(keys.telegramLink);
      return null;
    }
    return value;
  } catch {
    localStorage.removeItem(keys.telegramLink);
    return null;
  }
}

async function completeTelegramAccountLinkIfPending() {
  const pending = pendingTelegramAccountLink();
  if (!pending || !accessToken()) return false;

  await api("/v1/telegram/account-link/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ token: pending.token })
  });
  localStorage.removeItem(keys.telegramLink);
  setStatus(
    "#job-status",
    "Telegram успешно привязан к этому VideoGrabber-аккаунту.",
    "success"
  );
  return true;
}

function captureDesktopFlow() {
  const query = new URLSearchParams(location.search);
  const flowId = query.get("desktop_flow");
  const stateValue = query.get("desktop_state");
  if (!flowId || !stateValue) return;

  localStorage.setItem(keys.desktop, JSON.stringify({
    flowId,
    state: stateValue,
    expiresAt: Date.now() + 5 * 60 * 1000
  }));
  history.replaceState({}, "", "/web/" + (location.hash || ""));
}

function pendingDesktopFlow() {
  try {
    const raw = localStorage.getItem(keys.desktop);
    if (!raw) return null;
    const value = JSON.parse(raw);
    if (!value
        || !value.flowId
        || !value.state
        || Number(value.expiresAt || 0) <= Date.now()) {
      localStorage.removeItem(keys.desktop);
      return null;
    }
    return value;
  } catch {
    localStorage.removeItem(keys.desktop);
    return null;
  }
}

async function completeDesktopHandoffIfPending() {
  const pending = pendingDesktopFlow();
  if (!pending || !accessToken()) return false;

  const approval = await api("/v1/auth/desktop/approve", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      flowId: pending.flowId,
      state: pending.state
    })
  });

  localStorage.removeItem(keys.desktop);
  const callback = new URL(approval.returnUri);
  callback.searchParams.set("code", approval.code);
  callback.searchParams.set("state", approval.state);
  location.assign(callback.href);
  return true;
}

function isOnline(device) {
  if (device.revoked || !device.lastSeenAt) return false;
  const seen = Date.parse(device.lastSeenAt);
  return Number.isFinite(seen) && Date.now() - seen < 25_000;
}

function planName(planId) {
  return ({
    free: "Free",
    start: "Start",
    unlimited_video: "Unlimited Video",
    full_course: "Full Course"
  })[planId] || (state.profile?.role === "owner_admin" ? "Owner" : "Legacy");
}

const planCatalog = {
  free: {
    name: "Free",
    price: "0 ₽",
    description: "Для знакомства с VideoGrabber без оплаты.",
    features: [
      "10 обычных видео навсегда",
      "Один аккаунт для сайта, Windows и Telegram",
      "Полные курсы не входят"
    ]
  },
  start: {
    name: "Start",
    price: "1 500 ₽ / 30 дней",
    description: "Для обычных регулярных загрузок.",
    features: [
      "До 10 загрузок в сутки",
      "MP3 и редактор",
      "Локальная транскрибация",
      "Полные курсы не входят"
    ]
  },
  unlimited_video: {
    name: "Unlimited Video",
    price: "2 500 ₽ / 30 дней",
    description: "Для тех, кто часто скачивает отдельные видео.",
    features: [
      "Отдельные видео без лимита",
      "MP3 и редактор",
      "Локальная транскрибация",
      "Полные курсы не входят"
    ]
  },
  full_course: {
    name: "Full Course",
    price: "5 000 ₽ / 30 дней",
    description: "Для курсов, закрытых страниц и максимальных возможностей.",
    features: [
      "Всё из Unlimited Video",
      "Полные курсы и закрытые страницы",
      "Сохранение структуры уроков и материалов",
      "TXT рядом с видео по желанию"
    ]
  }
};

let requestedPlan = null;

function recommendedPlanForOperation(kind) {
  if (kind === "course_download") return "full_course";
  if (kind === "mp3") return "start";
  return "start";
}

function openPlanDialog(planId, reason = "") {
  const plan = planCatalog[planId] || planCatalog.start;
  requestedPlan = planId in planCatalog ? planId : "start";

  $("#plan-dialog-title").textContent = plan.name;
  $("#plan-dialog-price").textContent = plan.price;
  $("#plan-dialog-description").textContent = plan.description;

  const features = $("#plan-dialog-features");
  features.replaceChildren();
  for (const text of plan.features) {
    const item = document.createElement("li");
    item.textContent = text;
    features.append(item);
  }

  const reasonNode = $("#plan-dialog-reason");
  reasonNode.hidden = !reason;
  reasonNode.textContent = reason || "";

  const action = $("#plan-dialog-action");
  action.textContent = requestedPlan === "free"
    ? "Начать бесплатно"
    : "Выбрать этот тариф";
  $("#plan-dialog").showModal();
}

function closePlanDialog() {
  if ($("#plan-dialog").open) $("#plan-dialog").close();
}

function findPaymentProduct(planId) {
  return state.paymentProducts?.products?.find(
    (product) =>
      product.planId === planId &&
      product.sku &&
      product.prices?.yookassa
  ) || null;
}

async function handlePlanAction(planId) {
  closePlanDialog();

  if (planId === "free") {
    $("#app").scrollIntoView({ behavior: "smooth", block: "start" });
    if (!accessToken()) $("#google-login")?.focus();
    return;
  }

  if (!accessToken()) {
    setStatus(
      "#auth-status",
      "Сначала войдите. После входа выбранный тариф можно оформить здесь же."
    );
    $("#app").scrollIntoView({ behavior: "smooth", block: "start" });
    return;
  }

  if (state.access?.planId === planId) {
    setStatus("#job-status", "Этот тариф уже активен.", "success");
    $("#app").scrollIntoView({ behavior: "smooth", block: "start" });
    return;
  }

  const product = findPaymentProduct(planId);
  if (!product) {
    setStatus(
      "#job-status",
      "Сейчас этот тариф нельзя оформить онлайн. Попробуйте ещё раз позже.",
      "error"
    );
    $("#app").scrollIntoView({ behavior: "smooth", block: "start" });
    return;
  }

  await createCheckout(product);
}

function operationName(kind) {
  return ({
    download: "Видео",
    mp3: "MP3",
    course_download: "Полный курс",
    trim: "Обрезка",
    join: "Склейка",
    transcribe: "Транскрибация"
  })[kind] || kind;
}

function stateName(value) {
  return ({
    waiting_for_worker: "ждёт компьютер",
    queued: "в очереди",
    running: "выполняется",
    completed: "готово",
    failed: "ошибка",
    review_required: "нужна проверка",
    cancel_requested: "отмена…",
    cancelled: "отменено"
  })[value] || value;
}

function renderAccount() {
  const access = state.access;
  const profile = state.profile;
  if (!access || !profile) return;

  $("#account-title").textContent =
    profile.role === "owner_admin" ? "Ваш аккаунт · Администратор" : "Ваш аккаунт";
  const adminLink = $("#admin-link");
  if (adminLink) adminLink.hidden = profile.role !== "owner_admin";
  $("#plan-value").textContent = planName(access.planId);

  if (access.unlimited) {
    $("#limit-value").textContent = "Без лимита на отдельные видео";
  } else if (access.planId === "start") {
    $("#limit-value").textContent = "До 10 загрузок за UTC-сутки";
  } else {
    $("#limit-value").textContent =
      String(access.remainingDownloads) + " загрузок осталось";
  }

  $("#course-value").textContent =
    access.canDownloadCourse ? "Доступен" : "Не входит в тариф";

  const telegram = state.identities.some(
    (identity) => String(identity.provider).toLowerCase() === "telegram"
  );
  $("#telegram-value").textContent = telegram ? "Привязан" : "Не привязан";

  // Keep every operation selectable. If the tariff does not include it,
  // the submit action opens a clear plan explanation instead of looking broken.
  renderCourseHint();
}

function accessFeature(feature, fallback) {
  const overrides = state.access?.featureOverrides || {};
  if (Object.prototype.hasOwnProperty.call(overrides, feature))
    return overrides[feature] === true;
  return Boolean(fallback);
}

function renderCourseHint() {
  if (!state.access) return;

  const kind = $("#operation").value;
  const target = $("#download-target");
  const browserOption = target.querySelector('option[value="browser"]');

  if (kind === "course_download") {
    browserOption.disabled = true;
    target.value = "desktop";
    $("#course-hint").textContent = accessFeature("course_download", state.access.canDownloadCourse)
      ? "Полный курс скачивается через Windows VideoGrabber: приложению нужна ваша авторизованная сессия курса. Перед запуском можно включить транскрибацию — тогда текст каждого видео будет автоматически сохраняться рядом с ним."
      : "Полный курс доступен на Full Course. После выбора тарифа скачивание выполняется в Windows-приложении.";
  } else {
    browserOption.disabled = false;
    if (kind === "mp3") {
      $("#course-hint").textContent = accessFeature("mp3", state.access.canEdit)
        ? "MP3 можно подготовить прямо через сайт — Windows-приложение не требуется."
        : "MP3 относится к расширенным функциям. Нажмите «Скачать», чтобы увидеть подходящий тариф.";
    } else {
      $("#course-hint").textContent = $("#download-target").value === "browser"
        ? "Обычное видео скачивается прямо через сайт. Windows-приложение можно не открывать."
        : "Видео будет отправлено в выбранный Windows VideoGrabber и сохранено в его локальную папку.";
    }
  }

  renderSelectedDevice();
}

function renderDevices() {
  const select = $("#device-select");
  const previous = select.value;
  select.replaceChildren();

  const active = state.devices.filter((device) => !device.revoked);
  for (const device of active) {
    const option = document.createElement("option");
    option.value = device.deviceId;
    option.textContent =
      device.name + (isOnline(device) ? " — online" : " — offline");
    select.append(option);
  }
  if (previous && active.some((device) => device.deviceId === previous))
    select.value = previous;

  const list = $("#devices-list");
  list.replaceChildren();
  if (active.length === 0) {
    list.textContent =
      "Нет зарегистрированного Windows VideoGrabber. Войдите в Managed-приложение и включите задания с сайта.";
  } else {
    for (const device of active) {
      const row = document.createElement("div");
      row.className = "device-row";
      const main = document.createElement("div");
      main.className = "row-main";
      const name = document.createElement("b");
      name.textContent = device.name;
      const meta = document.createElement("small");
      meta.textContent = device.lastSeenAt
        ? "Последняя связь: " + new Date(device.lastSeenAt).toLocaleString()
        : "Компьютер ещё не выходил на связь";
      main.append(name, meta);
      const pill = document.createElement("span");
      pill.className = "pill " + (isOnline(device) ? "online" : "offline");
      pill.textContent = isOnline(device) ? "в сети" : "не в сети";
      row.append(main, pill);
      list.append(row);
    }
  }

  renderSelectedDevice();
}

function renderSelectedDevice() {
  const target = $("#download-target").value;
  const field = $("#device-field");
  const pill = $("#device-pill");
  const submit = $("#submit-job");

  field.hidden = target !== "desktop";

  if (target === "browser") {
    pill.className = "pill online";
    pill.textContent = "Скачать прямо в браузер";
    submit.disabled = false;
    submit.textContent = $("#operation").value === "mp3"
      ? "Скачать MP3"
      : "Скачать";
    return;
  }

  const id = $("#device-select").value;
  const device = state.devices.find((item) => item.deviceId === id);

  if (!device) {
    pill.className = "pill offline";
    pill.textContent = "Windows-приложение не выбрано";
    submit.disabled = false;
    submit.textContent = "Отправить в Windows";
    return;
  }

  const online = isOnline(device);
  pill.className = "pill " + (online ? "online" : "offline");
  pill.textContent = online
    ? "Windows online"
    : "Windows offline · можно поставить в очередь";
  submit.disabled = false;
  submit.textContent = $("#operation").value === "course_download"
    ? "Скачать курс в Windows"
    : "Отправить в Windows";
}

function renderJobs() {
  const host = $("#jobs-list");
  host.replaceChildren();
  const jobs = [...state.jobs].reverse().slice(0, 12);
  if (jobs.length === 0) {
    host.textContent = "Заданий пока нет.";
    return;
  }

  for (const job of jobs) {
    const row = document.createElement("div");
    row.className = "job-row";

    const main = document.createElement("div");
    main.className = "row-main";
    const title = document.createElement("b");
    title.textContent = operationName(job.kind || job.reason || "download");
    const meta = document.createElement("small");
    meta.textContent =
      (job.executor === "server_worker" ? "Сайт" : "Windows") +
      (job.reason ? " · " + job.reason : "");
    main.append(title, meta);

    const action = document.createElement("div");
    action.className = "job-action";
    const status = document.createElement("span");
    status.className = "state";
    status.textContent = stateName(job.state);
    action.append(status);

    if (job.state === "completed"
        && job.executor === "server_worker"
        && job.artifactId) {
      const download = document.createElement("button");
      download.type = "button";
      download.className = "text-button";
      download.textContent = "Скачать";
      download.addEventListener("click", async () => {
        try {
          await downloadJobResult(job.jobId);
        } catch (error) {
          setStatus("#job-status", "Не удалось скачать файл: " + error.message, "error");
        }
      });
      action.append(download);
    }

    row.append(main, action);
    host.append(row);
  }
}

async function loadDashboard() {
  const [profile, access, identities, devices, jobs, subscriptions] =
    await Promise.all([
      api("/v1/me"),
      api("/v1/access"),
      api("/v1/identities"),
      api("/v1/devices"),
      api("/v1/jobs"),
      api("/v1/subscriptions")
    ]);

  state.profile = profile;
  state.access = access;
  state.identities = identities;
  state.devices = devices;
  state.jobs = jobs;
  state.subscriptions = subscriptions;

  try {
    state.paymentProducts = await api("/v1/payment-products");
  } catch {
    state.paymentProducts = null;
  }

  $("#signed-out").hidden = true;
  $("#dashboard").hidden = false;
  $("#header-login").textContent = "Кабинет";
  renderAccount();
  renderDevices();
  renderJobs();
  setTimeout(() => window.ScrollTrigger?.refresh?.(), 0);
}

function signedOut(message = "") {
  $("#signed-out").hidden = false;
  $("#dashboard").hidden = true;
  $("#header-login").textContent = "Войти";
  if (message) setStatus("#auth-status", message, "error");
  setTimeout(() => window.ScrollTrigger?.refresh?.(), 0);
}

async function sha256Hex(text) {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(text)
  );
  return [...new Uint8Array(digest)]
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

async function createJobRequestHash(job) {
  const canonical = JSON.stringify({
    intentId: job.intentId.toLowerCase(),
    kind: job.kind,
    executor: job.executor,
    deviceId: job.deviceId ? job.deviceId.toLowerCase() : null,
    sourceId: job.sourceId,
    quality: job.quality,
    inputArtifactIds: job.inputArtifactIds,
    trimStartMs: job.trimStartMs,
    trimDurationMs: job.trimDurationMs
  });
  return sha256Hex(canonical);
}


function chooseServerQuality(available, requested) {
  const values = Array.isArray(available) ? available.filter(Boolean) : [];
  if (values.length === 0) return "best";
  if (requested === "best") return values[0];
  if (values.includes(requested)) return requested;

  const ceiling = Number.parseInt(requested, 10);
  if (!Number.isFinite(ceiling)) return values[0];

  const numeric = values
    .map((value) => ({ value, height: Number.parseInt(value, 10) }))
    .filter((item) => Number.isFinite(item.height))
    .sort((a, b) => b.height - a.height);
  return numeric.find((item) => item.height <= ceiling)?.value
    || numeric.at(-1)?.value
    || values[0];
}

async function authorizedFetch(path, options = {}, retry = true) {
  const headers = new Headers(options.headers || {});
  if (accessToken()) headers.set("Authorization", "Bearer " + accessToken());
  const response = await fetch(path, { ...options, headers });
  if (response.status === 401 && retry && refreshToken()) {
    await refreshSession();
    return authorizedFetch(path, options, false);
  }
  return response;
}

function downloadName(response, jobId) {
  const header = response.headers.get("Content-Disposition") || "";
  const utf8 = header.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8) {
    try { return decodeURIComponent(utf8[1]); } catch {}
  }
  const plain = header.match(/filename="?([^";]+)"?/i);
  return plain?.[1] || ("VideoGrabber-" + jobId.slice(0, 10));
}

function clearResultActions() {
  $("#result-actions").replaceChildren();
}

async function downloadJobResult(jobId) {
  setStatus("#job-status", "Файл готов. Начинаю скачивание…");
  const link = await api(
    "/v1/jobs/" + encodeURIComponent(jobId) + "/download-link",
    { method: "POST" }
  );
  if (!link?.url) throw new Error("download_link_unavailable");

  const anchor = document.createElement("a");
  anchor.className = "button button-primary";
  anchor.href = link.url;
  anchor.textContent = "Скачать готовый файл";
  $("#result-actions").replaceChildren(anchor);

  anchor.click();
  setStatus(
    "#job-status",
    "Готово. Скачивание началось. Если браузер его не открыл — нажмите «Скачать готовый файл».",
    "success"
  );
}

async function submitJob(event) {
  event.preventDefault();
  clearResultActions();
  setStatus("#job-status", "Проверяю ссылку и создаю задание…");

  const target = $("#download-target").value;
  const deviceId = $("#device-select").value;
  const url = $("#source-url").value.trim();
  const requestedQuality = $("#quality").value;
  const kind = $("#operation").value;

  if (kind === "course_download" && target !== "desktop") {
    $("#download-target").value = "desktop";
    renderSelectedDevice();
    setStatus(
      "#job-status",
      "Полный курс скачивается через Windows VideoGrabber. Выберите компьютер и откройте приложение.",
      "error"
    );
    return;
  }

  if (target === "desktop" && !deviceId) {
    setStatus(
      "#job-status",
      "Для отправки в Windows выберите зарегистрированный компьютер. Для обычного видео можно выбрать «В браузер — без приложения».",
      "error"
    );
    return;
  }

  if (!state.access?.canDownload) {
    setStatus("#job-status", "Лимит загрузок исчерпан. Выберите тариф, чтобы продолжить.", "error");
    openPlanDialog(
      recommendedPlanForOperation(kind),
      "Эта операция сейчас недоступна: бесплатный лимит исчерпан."
    );
    return;
  }
  if (kind === "mp3" && !accessFeature("mp3", state.access?.canEdit)) {
    setStatus("#job-status", "MP3 доступен на платных тарифах.", "error");
    openPlanDialog(
      "start",
      "Free предназначен для 10 обычных загрузок видео. MP3 входит в расширенные тарифы."
    );
    return;
  }
  if (kind === "course_download" && !accessFeature("course_download", state.access?.canDownloadCourse)) {
    setStatus("#job-status", "Полный курс доступен на тарифе Full Course.", "error");
    openPlanDialog(
      "full_course",
      "Скачивание полного курса — функция максимального тарифа Full Course."
    );
    return;
  }

  try {
    let source;
    let quality = requestedQuality;

    if (target === "browser") {
      const analyzed = await api("/v1/sources/analyze", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source: url })
      });
      if (!Array.isArray(analyzed) || analyzed.length === 0)
        throw new Error("source_analysis_failed");
      source = analyzed[0];
      quality = chooseServerQuality(source.qualities, requestedQuality);
    } else {
      source = await api("/v1/sources/register-desktop", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source: url, quality })
      });
    }

    const job = {
      intentId: crypto.randomUUID().toLowerCase(),
      requestHash: "",
      kind,
      executor: target === "browser" ? "server_worker" : "desktop_worker",
      deviceId: target === "browser" ? null : deviceId.toLowerCase(),
      sourceId: source.sourceId,
      quality,
      inputArtifactIds: [],
      trimStartMs: null,
      trimDurationMs: null
    };
    job.requestHash = await createJobRequestHash(job);

    const created = await api("/v1/jobs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(job)
    });

    setStatus(
      "#job-status",
      target === "browser"
        ? "Задание принято. VideoGrabber скачивает файл на сервере; Windows-приложение не требуется."
        : isOnline(state.devices.find((item) => item.deviceId === deviceId) || {})
          ? "Задание отправлено в Windows VideoGrabber."
          : "Задание поставлено в очередь и начнётся, когда Windows VideoGrabber станет online.",
      "success"
    );
    $("#source-url").value = "";
    await refreshJobs();
    watchJob(created.jobId, target);
  } catch (error) {
    const message =
      error.message === "access_unavailable"
        ? "Лимит тарифа исчерпан или операция не входит в тариф."
        : error.message === "job_source_unavailable"
          ? "Источник или выбранное качество сейчас недоступны."
          : error.message === "source_unavailable"
            ? "Площадка сообщает, что этот ролик недоступен. Проверьте, открывается ли именно эта ссылка в обычном браузере. Бесплатная загрузка за такую попытку не списывается."
            : error.message === "source_bot_check"
              ? "YouTube временно включил bot-check для серверного запроса. Это не означает, что вам нужно входить в YouTube. Повторите попытку позже — бесплатная загрузка не списывается."
              : error.message === "source_login_required"
              ? "Для этой ссылки YouTube требует вход или доступ к закрытому контенту. Используйте Windows VideoGrabber со встроенным браузером."
              : error.message === "source_rate_limited"
                ? "YouTube временно ограничил серверные запросы. Попробуйте ещё раз позже или отправьте загрузку в Windows VideoGrabber. Бесплатная загрузка не списывается."
                : error.message === "source_runtime_incomplete"
                  ? "Серверный модуль загрузки временно недоступен. Бесплатная загрузка не списывается."
                  : error.message === "source_analysis_failed"
                    ? "Не удалось определить видео по этой ссылке. Бесплатная загрузка не списывается; для закрытых страниц используйте Windows VideoGrabber."
                    : "Не удалось создать задание: " + error.message;
    setStatus("#job-status", message, "error");
  }
}

async function watchJob(jobId, target = "browser") {
  for (let pass = 0; pass < 600; pass++) {
    await new Promise((resolve) => setTimeout(resolve, 3000));
    if (!accessToken()) return;
    try {
      const job = await api("/v1/jobs/" + encodeURIComponent(jobId));
      const index = state.jobs.findIndex((item) => item.jobId === job.jobId);
      if (index >= 0) state.jobs[index] = job;
      else state.jobs.push(job);
      renderJobs();

      if (job.state === "completed") {
        await loadAccessOnly();
        if (target === "browser" || job.executor === "server_worker") {
          try {
            await downloadJobResult(jobId);
          } catch (error) {
            setStatus(
              "#job-status",
              "Файл готов, но браузер не начал скачивание автоматически. Нажмите «Скачать» в очереди. " + error.message,
              "error"
            );
          }
        } else {
          setStatus(
            "#job-status",
            "Готово. Результат сохранён на Windows-компьютере.",
            "success"
          );
        }
        return;
      }

      if (["failed", "cancelled", "review_required"].includes(job.state)) {
        setStatus(
          "#job-status",
          "Задание завершено со статусом: " + stateName(job.state)
            + (job.reason ? " · " + job.reason : ""),
          "error"
        );
        await loadAccessOnly();
        return;
      }
    } catch {
      return;
    }
  }

  setStatus(
    "#job-status",
    "Задание всё ещё выполняется. Его статус остаётся в очереди; можно обновить страницу позже.",
    "error"
  );
}

async function loadAccessOnly() {
  try {
    state.access = await api("/v1/access");
    renderAccount();
  } catch {}
}

async function refreshJobs() {
  state.jobs = await api("/v1/jobs");
  renderJobs();
}

async function createCheckout(product) {
  try {
    const checkout = await api("/v1/payments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sku: product.sku,
        provider: "yookassa",
        idempotencyKey: crypto.randomUUID(),
        recurring: true
      })
    });
    if (checkout.redirectUri) {
      location.assign(checkout.redirectUri);
      return;
    }
    setStatus(
      "#job-status",
      "Не удалось открыть страницу оплаты. Попробуйте ещё раз позже.",
      "error"
    );
  } catch {
    setStatus(
      "#job-status",
      "Сейчас не удалось открыть оплату. Попробуйте ещё раз позже.",
      "error"
    );
  }
}

async function logout() {
  const token = refreshToken();
  try {
    if (token) {
      await fetch("/v1/auth/logout", {
        method: "POST",
        headers: {
          "Accept": "application/json",
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ refreshToken: token })
      });
    }
  } finally {
    clearSession();
    signedOut();
  }
}

async function refreshDevicesQuietly() {
  if (!accessToken()) return;
  try {
    state.devices = await api("/v1/devices");
    renderDevices();
  } catch {}
}

const heroFeatureCatalog = {
  url: {
    title: "Вставьте ссылку",
    copy: "Скопируйте ссылку — VideoGrabber подготовит доступный сценарий загрузки.",
    accent: [0.28, 0.62, 1.0]
  },
  video: {
    title: "Скачайте видео",
    copy: "Обычное видео можно получить прямо через сайт — без открытия Windows-приложения.",
    accent: [0.22, 0.74, 1.0]
  },
  mp3: {
    title: "Получите MP3",
    copy: "Извлеките аудиодорожку из доступного видео и сохраните её отдельным файлом.",
    accent: [1.0, 0.58, 0.36]
  },
  course: {
    title: "Скачайте полный курс",
    copy: "Для курсов и закрытых страниц используйте Windows VideoGrabber с сохранением структуры.",
    accent: [0.72, 0.38, 1.0]
  },
  windows: {
    title: "Работайте в Windows",
    copy: "Локальный клиент нужен для курсов, закрытых страниц и сохранения прямо в выбранную папку.",
    accent: [0.30, 0.78, 1.0]
  },
  telegram: {
    title: "Используйте Telegram",
    copy: "Отправьте ссылку боту и продолжайте работу с тем же аккаунтом VideoGrabber.",
    accent: [0.30, 0.68, 1.0]
  }
};

function setupPerformanceDiagnostics() {
  const metrics = window.__VG_WEB_VITALS;
  const observers = [];
  const interactions = new Map();

  const observe = (type, callback, options = {}) => {
    if (
      !("PerformanceObserver" in window) ||
      !PerformanceObserver.supportedEntryTypes?.includes(type)
    ) return;

    try {
      const observer = new PerformanceObserver((list) =>
        callback(list.getEntries())
      );
      observer.observe({ type, buffered: true, ...options });
      observers.push(observer);
    } catch (error) {
      console.debug("VideoGrabber performance observer unavailable", type, error);
    }
  };

  observe("largest-contentful-paint", (entries) => {
    const entry = entries.at(-1);
    if (entry) metrics.lcp = entry.startTime;
  });

  observe("layout-shift", (entries) => {
    for (const entry of entries) {
      if (!entry.hadRecentInput)
        metrics.cls += entry.value || 0;
    }
  });

  observe("event", (entries) => {
    for (const entry of entries) {
      if (!entry.interactionId) continue;
      const current = interactions.get(entry.interactionId) || 0;
      interactions.set(
        entry.interactionId,
        Math.max(current, entry.duration || 0)
      );
    }

    const values = [...interactions.values()].sort((a, b) => a - b);
    if (!values.length) return;
    const index = Math.max(0, Math.ceil(values.length * 0.98) - 1);
    metrics.inp = values[index];
  }, { durationThreshold: 40 });

  const panel = $("#perf-debug");
  let debugTimer = null;

  const formatMs = (value) =>
    Number.isFinite(value) ? Math.round(value) + " ms" : "—";

  const renderDebug = () => {
    if (!perfDebugMode || !panel) return;
    const canvas = $("#hero-three");
    $("#perf-quality").textContent = canvas?.dataset.quality || "pending";
    $("#perf-dpr").textContent = canvas?.dataset.dpr || "—";
    $("#perf-frame").textContent =
      canvas?.dataset.frameMs && canvas.dataset.frameMs !== "0"
        ? canvas.dataset.frameMs + " ms"
        : "—";
    $("#perf-lcp").textContent = formatMs(metrics.lcp);
    $("#perf-cls").textContent = metrics.cls.toFixed(3);
    $("#perf-inp").textContent = formatMs(metrics.inp);
  };

  if (perfDebugMode && panel) {
    panel.hidden = false;
    document.documentElement.classList.add("perf-debug-mode");
    renderDebug();
    debugTimer = setInterval(renderDebug, 500);
  }

  window.addEventListener("pagehide", () => {
    observers.forEach((observer) => observer.disconnect());
    if (debugTimer !== null) clearInterval(debugTimer);
  }, { once: true });
}

function setupHeroFeatureFocus(visual) {
  const hotspots = $$(".hero-hotspot[data-feature]");
  if (!hotspots.length) return;

  const defaultAccent = [0.28, 0.62, 1.0];
  const resetFeature = () => {
    for (const hotspot of hotspots)
      hotspot.classList.remove("is-active");
    visual.dispatchEvent(new CustomEvent("videograbber:hero-accent", {
      detail: { accent: defaultAccent, feature: null }
    }));
  };

  const activate = (hotspot) => {
    const feature = heroFeatureCatalog[hotspot.dataset.feature];
    if (!feature) return;

    for (const item of hotspots)
      item.classList.toggle("is-active", item === hotspot);

    visual.dispatchEvent(new CustomEvent("videograbber:hero-accent", {
      detail: { accent: feature.accent, feature: hotspot.dataset.feature }
    }));
  };

  for (const hotspot of hotspots) {
    hotspot.addEventListener("pointerenter", () => activate(hotspot));
    hotspot.addEventListener("focus", () => activate(hotspot));
    hotspot.addEventListener("pointerleave", () => {
      if (document.activeElement !== hotspot) resetFeature();
    });
    hotspot.addEventListener("blur", () => {
      if (!hotspot.matches(":hover")) resetFeature();
    });
  }
}

function setupScrollReveal() {
  if (visualTestMode) return;
  if (matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  if (!("IntersectionObserver" in window)) return;

  const targets = [
    ...$$(".how-section"),
    ...$$(".sync-card"),
    ...$$(".price-card"),
    ...$$(".download-card")
  ];
  if (!targets.length) return;

  document.body.classList.add("motion-ready");
  targets.forEach((target, index) => {
    target.classList.add("reveal-item");
    target.style.setProperty(
      "--reveal-delay",
      Math.min(index % 4, 3) * 65 + "ms"
    );
  });

  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add("is-revealed");
        observer.unobserve(entry.target);
      }
    },
    { threshold: 0.12, rootMargin: "0px 0px -7% 0px" }
  );

  for (const target of targets)
    observer.observe(target);
}

function setupStoryStage() {
  const stage = $("#story-stage");
  const visual = $("#hero-visual");
  const heroSlot = document.querySelector(".story-slot-hero");
  if (!stage || !visual || !heroSlot) return;

  const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const compactViewport = matchMedia("(max-width: 1050px)").matches;
  const gsapApi = window.gsap;
  const scrollTriggerApi = window.ScrollTrigger;

  const dispatchStoryState = (state, progress = 0) => {
    stage.dataset.storyState = state;
    visual.dispatchEvent(new CustomEvent("videograbber:story-state", {
      detail: { state, progress }
    }));
  };

  const placeAtHeroSlot = () => {
    const rect = heroSlot.getBoundingClientRect();
    stage.classList.remove("is-managed");
    stage.style.right = "auto";
    stage.style.left = rect.left + window.scrollX + "px";
    stage.style.top = rect.top + window.scrollY + "px";
    stage.style.width = rect.width + "px";
    stage.style.height = rect.height + "px";
    stage.style.opacity = "1";
    dispatchStoryState("hero", 0);
  };

  if (
    visualTestMode ||
    reducedMotion ||
    compactViewport ||
    !gsapApi ||
    !scrollTriggerApi
  ) {
    placeAtHeroSlot();
    window.addEventListener("resize", placeAtHeroSlot, { passive: true });
    return;
  }

  gsapApi.registerPlugin(scrollTriggerApi);
  stage.classList.add("is-managed");
  stage.style.right = "auto";

  const definitions = [
    { state: "hero", trigger: "#top", slot: ".story-slot-hero" },
    { state: "workflow", trigger: "#how", slot: ".story-slot-workflow" },
    { state: "sync", trigger: "#app", slot: ".story-slot-sync" },
    { state: "pricing", trigger: "#pricing", slot: ".story-slot-pricing" },
    { state: "windows", trigger: "#download", slot: ".story-slot-windows" }
  ];

  const previousState = {
    workflow: "hero",
    sync: "workflow",
    pricing: "sync",
    windows: "pricing"
  };

  let currentState = "hero";

  const targetFor = (definition) => {
    const slot = document.querySelector(definition.slot);
    if (!slot) return null;
    const rect = slot.getBoundingClientRect();
    const top = definition.state === "hero"
      ? rect.top
      : Math.max(88, Math.min(150, rect.top));
    return {
      left: rect.left,
      top,
      width: Math.max(rect.width, 1),
      height: Math.max(rect.height, 1)
    };
  };

  const activate = (state, immediate = false) => {
    const definition = definitions.find((item) => item.state === state);
    const target = definition ? targetFor(definition) : null;
    if (!definition || !target) return;

    currentState = state;
    dispatchStoryState(state, 0);

    gsapApi.to(stage, {
      left: target.left,
      top: target.top,
      width: target.width,
      height: target.height,
      opacity: 1,
      duration: immediate ? 0 : 0.78,
      ease: immediate ? "none" : "power3.out",
      overwrite: true
    });
  };

  for (const definition of definitions.slice(1)) {
    const triggerNode = document.querySelector(definition.trigger);
    if (!triggerNode) continue;

    scrollTriggerApi.create({
      trigger: triggerNode,
      start: "top 58%",
      end: "bottom 42%",
      onEnter: () => activate(definition.state),
      onEnterBack: () => activate(definition.state),
      onLeaveBack: () => activate(previousState[definition.state] || "hero"),
      onUpdate: (self) => {
        if (currentState !== definition.state) return;
        visual.dispatchEvent(new CustomEvent("videograbber:story-progress", {
          detail: {
            state: definition.state,
            progress: self.progress,
            direction: self.direction
          }
        }));
      }
    });
  }

  const footer = document.querySelector("footer");
  if (footer) {
    scrollTriggerApi.create({
      trigger: footer,
      start: "top 96%",
      onEnter: () => {
        visual.dispatchEvent(new CustomEvent("videograbber:story-visibility", {
          detail: { visible: false }
        }));
        gsapApi.to(stage, {
          opacity: 0,
          duration: 0.35,
          ease: "power2.out",
          overwrite: true
        });
      },
      onLeaveBack: () => {
        visual.dispatchEvent(new CustomEvent("videograbber:story-visibility", {
          detail: { visible: true }
        }));
        gsapApi.to(stage, {
          opacity: 1,
          duration: 0.35,
          ease: "power2.out",
          overwrite: true
        });
      }
    });
  }

  const determineInitialState = () => {
    let state = "hero";
    for (const definition of definitions.slice(1)) {
      const node = document.querySelector(definition.trigger);
      if (!node) continue;
      if (node.getBoundingClientRect().top <= innerHeight * 0.58)
        state = definition.state;
    }
    return state;
  };

  activate(determineInitialState(), true);

  scrollTriggerApi.addEventListener("refresh", () =>
    activate(currentState, true)
  );

  window.addEventListener(
    "resize",
    () => scrollTriggerApi.refresh(),
    { passive: true }
  );
}

function scheduleThreeHero() {
  const canvas = $("#hero-three");
  const art = document.querySelector(".hero-art");
  if (!canvas || !art) return;

  if (
    matchMedia("(prefers-reduced-motion: reduce)").matches &&
    !visualTestMode
  ) {
    canvas.hidden = true;
    return;
  }

  let started = false;
  const startImport = () => {
    if (started) return;
    started = true;
    import("/web/hero-three.bundle.js").catch((error) => {
      console.warn("VideoGrabber Three.js scene unavailable", error);
      canvas.hidden = true;
      $("#hero-visual")?.classList.remove("three-ready");
    });
  };

  const schedule = () => {
    if ("requestIdleCallback" in window) {
      requestIdleCallback(startImport, { timeout: 1200 });
      return;
    }
    setTimeout(startImport, 240);
  };

  if (art.complete) schedule();
  else art.addEventListener("load", schedule, { once: true });
}

function setupHeroScene() {
  const visual = $("#hero-visual");
  if (!visual) return;

  setupHeroFeatureFocus(visual);
  if (matchMedia("(prefers-reduced-motion: reduce)").matches) return;

  const update = (event) => {
    const rect = visual.getBoundingClientRect();
    const x = Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width));
    const y = Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height));
    visual.style.setProperty("--tilt-y", ((x - .5) * 8).toFixed(2) + "deg");
    visual.style.setProperty("--tilt-x", ((.5 - y) * 6).toFixed(2) + "deg");
  };

  visual.addEventListener("pointermove", update);
  visual.addEventListener("pointerleave", () => {
    visual.style.setProperty("--tilt-x", "0deg");
    visual.style.setProperty("--tilt-y", "0deg");
  });
}

async function start() {
  setupPerformanceDiagnostics();
  setupStoryStage();
  scheduleThreeHero();
  setupHeroScene();
  setupScrollReveal();
  captureTelegramAccountLink();
  captureDesktopFlow();

  for (const card of $$(".price-card[data-plan]")) {
    const activate = () => openPlanDialog(card.dataset.plan);
    const setPricingFocus = (plan) =>
      $("#hero-visual")?.dispatchEvent(
        new CustomEvent("videograbber:pricing-focus", {
          detail: { plan }
        })
      );

    card.addEventListener("pointerenter", () =>
      setPricingFocus(card.dataset.plan)
    );
    card.addEventListener("pointerleave", () => setPricingFocus(null));

    const actionButton = card.querySelector(".plan-action");
    actionButton?.addEventListener("focus", () =>
      setPricingFocus(card.dataset.plan)
    );
    actionButton?.addEventListener("blur", () => setPricingFocus(null));

    card.addEventListener("click", (event) => {
      if (event.target.closest("a")) return;
      activate();
    });
  }

  $("#plan-dialog-close").addEventListener("click", closePlanDialog);
  $("#plan-dialog-x").addEventListener("click", closePlanDialog);
  $("#plan-dialog-action").addEventListener("click", () =>
    handlePlanAction(requestedPlan || "start")
  );
  $("#plan-dialog").addEventListener("click", (event) => {
    if (event.target === $("#plan-dialog")) closePlanDialog();
  });

  const initial = new URLSearchParams(location.search).get("plan");
  if (initial && planCatalog[initial]) {
    setTimeout(() => openPlanDialog(initial), 80);
  }

  $("#google-login").addEventListener("click", async () => {
    try {
      await beginGoogleSignIn();
    } catch (error) {
      console.error("Google sign-in failed", error);
      setStatus("#auth-status", "Не удалось начать вход. Попробуйте ещё раз.", "error");
    }
  });

  $("#email-login-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const button = $("#email-login");
    button.disabled = true;
    setStatus("#auth-status", "Отправляю безопасную ссылку для входа…");
    try {
      await sendMagicLink($("#login-email").value);
      setStatus(
        "#auth-status",
        "Готово. Проверьте почту и откройте одноразовую ссылку. Пароль не нужен.",
        "success"
      );
      button.textContent = "Отправить ещё раз";
    } catch (error) {
      const message = String(error.message || error);
      const translated = ({
        "Проверьте адрес e-mail.":
          "Проверьте адрес e-mail.",
        "Email address not authorized":
          "Для этого адреса пока недоступен вход по почте.",
        "email rate limit exceeded":
          "Слишком много писем. Подождите немного и попробуйте снова."
      })[message] || "Не удалось отправить ссылку для входа. Попробуйте ещё раз позже.";
      setStatus("#auth-status", translated, "error");
    } finally {
      button.disabled = false;
    }
  });

  $("#header-login").addEventListener("click", () => {
    $("#app").scrollIntoView({ behavior: "smooth", block: "start" });
  });
  $("#download-form").addEventListener("submit", submitJob);
  $("#operation").addEventListener("change", renderCourseHint);
  $("#download-target").addEventListener("change", renderCourseHint);
  $("#device-select").addEventListener("change", renderSelectedDevice);
  $("#refresh-jobs").addEventListener("click", async () => {
    try { await refreshJobs(); }
    catch (error) { setStatus("#job-status", error.message, "error"); }
  });
  $("#logout").addEventListener("click", logout);

  try {
    const completed = await completeSupabaseCallback();
    if (completed) setStatus("#job-status", "Вход подтверждён.", "success");
  } catch (error) {
    console.error("Auth callback failed", error);
    clearSession();
    signedOut("Не удалось завершить вход. Попробуйте войти ещё раз.");
    return;
  }

  if (!accessToken() && refreshToken()) {
    try { await refreshSession(); }
    catch { clearSession(); }
  }

  if (!accessToken()) {
    signedOut();
    return;
  }

  try {
    await completeTelegramAccountLinkIfPending();
  } catch (error) {
    localStorage.removeItem(keys.telegramLink);
    setStatus(
      "#auth-status",
      error.message === "telegram_link_reconciliation_required"
        ? "Telegram-аккаунт содержит данные, требующие ручной проверки перед объединением."
        : "Не удалось привязать Telegram. Попробуйте ещё раз позже.",
      "error"
    );
  }

  try {
    if (await completeDesktopHandoffIfPending()) return;
  } catch (error) {
    localStorage.removeItem(keys.desktop);
    setStatus(
      "#auth-status",
      "Не удалось передать вход в Windows VideoGrabber. Повторите попытку.",
      "error"
    );
  }

  try {
    await loadDashboard();
  } catch (error) {
    if (error.status === 401) {
      clearSession();
      signedOut("Сессия истекла. Войдите снова.");
      return;
    }
    console.error("Dashboard loading failed", error);
    signedOut("Не удалось загрузить данные аккаунта. Обновите страницу или войдите снова.");
    return;
  }

  setInterval(refreshDevicesQuietly, 10_000);
}

start();
