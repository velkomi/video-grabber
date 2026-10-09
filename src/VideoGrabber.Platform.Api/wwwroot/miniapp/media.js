const $ = (selector) => document.querySelector(selector);

let analyzed = null;
let capabilities = null;

function api(path, options) {
  if (!window.VideoGrabberApi) throw new Error("Mini App session ещё не готова");
  return window.VideoGrabberApi.api(path, options);
}

function status(message, kind = "") {
  window.VideoGrabberApi?.setStatus(message, kind);
}

function hex(buffer) {
  return Array.from(new Uint8Array(buffer))
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

async function canonicalHash(request) {
  const canonical = {
    intentId: request.intentId.toLowerCase(),
    kind: request.kind,
    executor: request.executor,
    deviceId: request.deviceId ? request.deviceId.toLowerCase() : null,
    sourceId: request.sourceId,
    quality: request.quality,
    inputArtifactIds: request.inputArtifactIds.map((id) => id.toLowerCase()),
    trimStartMs: request.trimStartMs,
    trimDurationMs: request.trimDurationMs
  };
  const bytes = new TextEncoder().encode(JSON.stringify(canonical));
  return hex(await crypto.subtle.digest("SHA-256", bytes));
}

function pendingIntent(payloadKey) {
  const raw = sessionStorage.getItem("vg_pending_media_intent");
  if (raw) {
    try {
      const saved = JSON.parse(raw);
      if (saved.payloadKey === payloadKey && typeof saved.intentId === "string")
        return saved.intentId;
    } catch { }
  }
  const intentId = crypto.randomUUID();
  sessionStorage.setItem(
    "vg_pending_media_intent",
    JSON.stringify({ payloadKey, intentId }));
  return intentId;
}

function parseInputs() {
  const text = $("#media-inputs").value.trim();
  if (!text) return [];
  const ids = text.split(",").map((x) => x.trim()).filter(Boolean);
  const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  if (ids.some((id) => !guid.test(id))) throw new Error("Проверьте номера готовых файлов.");
  return ids;
}

const mediaLabels = {
  download: "Видео", video: "Видео", course_download: "Полный курс", mp3: "Аудио MP3",
  trim: "Обрезать видео", join: "Объединить видео", transcribe: "Текст из видео", transcription: "Текст из видео",
  desktop_worker: "Мой компьютер Windows", server_worker: "Онлайн", server: "Онлайн", telegram: "Telegram",
  best: "Лучшее доступное", worst: "Минимальный размер", audio: "Только аудио"
};
function mediaLabel(value) {
  return mediaLabels[value] || (/^\d+p$/.test(value) ? value : "Доступный вариант");
}
function mediaError(error) {
  const message = error?.message || "";
  return /^[А-Яа-яЁё]/.test(message) ? message : window.VideoGrabberApi.userError(error);
}
function fillSelect(select, values) {
  select.replaceChildren();
  for (const value of values) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = mediaLabel(value);
    select.append(option);
  }
}

function accessFeature(access, feature, fallback) {
  const overrides = access?.featureOverrides || {};
  if (Object.prototype.hasOwnProperty.call(overrides, feature))
    return overrides[feature] === true;
  return Boolean(fallback);
}

async function loadCapabilities() {
  capabilities = await api("/v1/capabilities");
  const access = window.VideoGrabberApi.currentAccess();
  const permitted = capabilities.operations
    .filter((x) => x.available)
    .filter((x) => {
      if (x.operation === "course_download")
        return accessFeature(access, "course_download", access?.canDownloadCourse);
      if (x.operation === "mp3") return accessFeature(access, "mp3", access?.canEdit);
      if (x.operation === "trim") return accessFeature(access, "trim", access?.canEdit);
      if (x.operation === "join") return accessFeature(access, "join", access?.canEdit);
      if (["transcribe", "transcription"].includes(x.operation))
        return accessFeature(access, "transcribe", access?.canEdit);
      return accessFeature(access, "download", access?.canDownload);
    })
    .map((x) => x.operation);
  fillSelect($("#media-operation"), permitted);
  $("#media-analyze").disabled =
    !window.VideoGrabberApi.isPrimaryAccount() || permitted.length === 0;
  updateExecutors();
  $("#media-direct-telegram").hidden = capabilities.reason !== "client_only_media";
}

function updateExecutors() {
  $("#media-course-rights-row").hidden = $("#media-operation").value !== "course_download";
  const operation = capabilities?.operations
    ?.find((x) => x.operation === $("#media-operation").value);
  fillSelect($("#media-executor"), operation?.executors || []);
}

$("#media-operation").addEventListener("change", () => {
  $("#media-course-rights").checked = false;
  updateExecutors();
  analyzed = null;
  $("#media-options").hidden = true;
});

$("#media-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const raw = $("#media-url").value.trim();
  try {
    if (!window.VideoGrabberApi.isPrimaryAccount())
      throw new Error("Сначала свяжите Telegram с основным аккаунтом через /link");
    const access = window.VideoGrabberApi.currentAccess();
    const kind = $("#media-operation").value;
    const effective = kind === "course_download"
      ? accessFeature(access, "course_download", access?.canDownloadCourse)
      : kind === "mp3"
        ? accessFeature(access, "mp3", access?.canEdit)
        : kind === "trim"
          ? accessFeature(access, "trim", access?.canEdit)
          : kind === "join"
            ? accessFeature(access, "join", access?.canEdit)
            : ["transcribe", "transcription"].includes(kind)
              ? accessFeature(access, "transcribe", access?.canEdit)
              : accessFeature(access, "download", access?.canDownload);
    if (!effective)
      throw new Error("Эта функция недоступна для текущих условий аккаунта.");
    status(kind === "course_download"
      ? "Проверяю ссылку курса для Windows…"
      : "Анализирую источник…");

    if (kind === "course_download" || capabilities?.reason === "client_only_media") {
      analyzed = await api("/v1/sources/register-desktop", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source: raw, quality: "best" })
      });
    } else {
      const rows = await api("/v1/sources/analyze", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source: raw })
      });
      if (!rows.length) throw new Error("Видео по этой ссылке не найдено.");
      analyzed = rows[0];
    }

    fillSelect($("#media-quality"), analyzed.qualities);
    updateExecutors();
    $("#media-options").hidden = false;
    status(
      kind === "course_download"
        ? "Курс будет скачан зарегистрированным Windows VideoGrabber. Выберите качество."
        : "Выберите качество и место обработки.",
      "success");
  } catch (error) {
    status("Анализ не выполнен: " + mediaError(error), "error");
  }
});

$("#media-create").addEventListener("click", async () => {
  if (!analyzed) return;
  try {
    if (!window.VideoGrabberApi.isPrimaryAccount())
      throw new Error("Сначала свяжите Telegram с основным аккаунтом через /link");
    const access = window.VideoGrabberApi.currentAccess();
    if (!access?.canDownload)
      throw new Error("Лимит загрузок исчерпан. Выберите подписку.");
    const kind = $("#media-operation").value;
    const executor = $("#media-executor").value;
    const inputs = parseInputs();
    const devices = executor === "desktop_worker"
      ? (await api("/v1/devices")).filter((x) => !x.revoked)
      : [];
    const deviceId = executor === "desktop_worker"
      ? (devices[0]?.deviceId || null)
      : null;
    if (executor === "desktop_worker" && !deviceId)
      throw new Error("Нет активного Windows-устройства");

    let trimStartMs = null;
    let trimDurationMs = null;
    if (kind === "trim") {
      trimStartMs = Number($("#media-trim-start").value);
      trimDurationMs = Number($("#media-trim-duration").value);
      if (!Number.isSafeInteger(trimStartMs) || trimStartMs < 0
          || !Number.isSafeInteger(trimDurationMs) || trimDurationMs <= 0)
        throw new Error("Укажите начало и длительность в миллисекундах.");
    }

    const payloadKey = JSON.stringify({
      kind, executor, deviceId,
      sourceId: analyzed.sourceId,
      quality: $("#media-quality").value,
      inputArtifactIds: inputs,
      trimStartMs, trimDurationMs
    });
    const request = {
      intentId: pendingIntent(payloadKey),
      requestHash: "",
      kind,
      executor,
      deviceId,
      sourceId: analyzed.sourceId,
      quality: $("#media-quality").value,
      inputArtifactIds: inputs,
      trimStartMs,
      trimDurationMs
    };
    request.requestHash = await canonicalHash(request);
    if (kind === "course_download") {
      await window.VideoGrabberDocuments.confirmCourseRights(api, request.intentId, $("#media-course-rights").checked);
      $("#media-course-rights").checked = false;
    }
    status("Создаю задание…");
    const job = await api("/v1/jobs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });
    sessionStorage.removeItem("vg_pending_media_intent");
    status("Загрузка добавлена в очередь.", "success");
    await refreshJobs();
  } catch (error) {
    status("Задание не создано: " + mediaError(error), "error");
  }
});

function russianState(job) {
  const states = {
    queued: "в очереди",
    waiting_for_worker: "ожидает ваш компьютер",
    running: "выполняется",
    cancel_requested: "отмена запрошена",
    cancelled: "отменено",
    completed: "готово",
    review_required: "нужна проверка",
    failed: "ошибка"
  };
  return states[job.state] || "проверяем состояние";
}

async function mutateJob(jobId, action) {
  await api("/v1/jobs/" + encodeURIComponent(jobId) + "/" + action, {
    method: "POST"
  });
  await refreshJobs();
}

async function refreshJobs() {
  const host = $("#media-jobs");
  const jobs = await api("/v1/jobs");
  host.replaceChildren();
  if (!jobs.length) {
    host.textContent = "Очередь пуста.";
    return;
  }
  for (const job of jobs.slice(-20).reverse()) {
    const row = document.createElement("div");
    row.className = "item";
    const label = document.createElement("span");
    label.className = "item-text";
    label.textContent =
      (mediaLabels[job.kind] || "Загрузка") + " • " + russianState(job);
    row.append(label);
    if (["queued","waiting_for_worker","running"].includes(job.state)) {
      const cancel = document.createElement("button");
      cancel.type = "button";
      cancel.textContent = "Отменить";
      cancel.addEventListener("click", () => mutateJob(job.jobId, "cancel"));
      row.append(cancel);
    } else if (job.state === "completed" && job.artifactId) {
      const send = document.createElement("button");
      send.type = "button";
      send.textContent = "Отправить";
      send.addEventListener("click", () => requestDelivery(job));
      row.append(send);
    } else if (job.state === "failed") {
      const retry = document.createElement("button");
      retry.type = "button";
      retry.textContent = "Повторить";
      retry.addEventListener("click", () => mutateJob(job.jobId, "retry"));
      row.append(retry);
    }
    host.append(row);
  }
}

async function refreshDestinations() {
  const select = $("#media-destination");
  const rows = (await api("/v1/destinations")).filter((x) => !x.revoked);
  select.replaceChildren();
  for (const destination of rows) {
    const option = document.createElement("option");
    option.value = destination.destinationId;
    option.textContent = window.VideoGrabberApi.destinationLabel(destination);
    select.append(option);
  }
}

function deliveryKey(jobId, destinationId) {
  const storageKey = "vg_delivery_intent_" + jobId + "_" + destinationId;
  let value = sessionStorage.getItem(storageKey);
  if (!value) {
    value = crypto.randomUUID();
    sessionStorage.setItem(storageKey, value);
  }
  return { storageKey, value };
}

async function requestDelivery(job) {
  const destinationId = $("#media-destination").value;
  if (!destinationId) {
    status("Сначала добавьте и выберите проверенного получателя.", "error");
    return;
  }
  const key = deliveryKey(job.jobId, destinationId);
  try {
    status("Отправляю готовый файл…");
    const delivery = await api("/v1/deliveries", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jobId: job.jobId,
        artifactId: job.artifactId,
        destinationId,
        idempotencyKey: key.value
      })
    });
    if (delivery.state === "delivered") {
      sessionStorage.removeItem(key.storageKey);
      status("Файл отправлен в Telegram.", "success");
    } else if (delivery.state === "delivery_unknown") {
      status("Telegram мог принять файл, но подтверждение потеряно. Не повторяйте автоматически.", "error");
    } else {
      status("Не удалось подтвердить доставку. Проверьте чат.", "error");
    }
    await refreshDeliveries();
  } catch (error) {
    status("Ошибка доставки: " + mediaError(error), "error");
  }
}

function deliveryLabel(state) {
  return { delivered: "Файл доставлен", delivery_unknown: "Проверьте получение файла в чате", pending: "Отправляем файл", sending: "Отправляем файл", failed: "Не удалось отправить файл", queued: "Ожидает отправки" }[state] || "Проверяем доставку";
}

async function refreshDeliveries() {
  const host = $("#media-deliveries");
  const rows = await api("/v1/deliveries");
  host.replaceChildren();
  for (const delivery of rows.slice(-10).reverse()) {
    const row = document.createElement("div");
    row.className = "item";
    const label = document.createElement("span");
    label.className = "item-text";
    label.textContent = deliveryLabel(delivery.state);
    row.append(label);
    if (delivery.state === "delivery_unknown") {
      const retry = document.createElement("button");
      retry.type = "button";
      retry.textContent = "Повторить — возможен дубль";
      retry.addEventListener("click", async () => {
        if (!confirm("Telegram мог уже получить файл. Повторная отправка может создать дубликат. Продолжить?")) return;
        await api("/v1/deliveries/" + encodeURIComponent(delivery.deliveryId) + "/retry-unknown", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ acknowledgedWarning: "I understand this may send a duplicate" })
        });
        await refreshDeliveries();
      });
      row.append(retry);
    }
    host.append(row);
  }
}
function connectEvents() {
  const events = new EventSource("/v1/jobs/events");
  const refresh = () => refreshJobs().catch(() => {});
  for (const type of [
    "job_queued","job_waiting_for_worker","job_started",
    "job_completed","job_cancelled","job_cancel_requested"
  ]) events.addEventListener(type, refresh);
}

async function boot() {
  for (let attempt = 0; attempt < 50 && !window.VideoGrabberApi; attempt++)
    await new Promise((resolve) => setTimeout(resolve, 100));
  if (!window.VideoGrabberApi) return;
  try {
    await window.VideoGrabberApi.ready;
    await loadCapabilities();
    await refreshDestinations();
    await refreshJobs();
    await refreshDeliveries();
    connectEvents();
  } catch (error) {
    status("Загрузки недоступны: " + mediaError(error), "error");
  }
}

boot();

let directTelegramIntent = null;
let directTelegramSending = false;
$("#media-url").addEventListener("input", () => { directTelegramIntent = null; });
$("#media-destination").addEventListener("change", () => { directTelegramIntent = null; });
$("#media-direct-telegram").addEventListener("click", async () => {
  if (directTelegramSending) return;
  const source = $("#media-url").value.trim();
  const destinationId = $("#media-destination").value;
  if (!source || !destinationId) { status("Укажите прямую ссылку на MP4 и выберите связанный Telegram-чат.", "error"); return; }
  directTelegramIntent ||= crypto.randomUUID().toLowerCase();
  directTelegramSending = true;
  try {
    const result = await api("/v1/direct-downloads/telegram", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ source, destinationId, intentId: directTelegramIntent })
    });
    status(result.state === "delivered" || result.state === "already_delivered"
      ? "Telegram получил готовый файл напрямую с источника."
      : result.state === "pending" ? "Этот запрос уже обрабатывается; повторная отправка не запускалась."
      : "Отправка не подтверждена. Проверьте чат перед новым запросом; автоматического повтора нет.", result.state === "review_required" ? "error" : "success");
  } catch (error) {
    status(error.message === "desktop_execution_required"
      ? "Для этой ссылки нужно приложение VideoGrabber для Windows."
      : "Не удалось выполнить прямую отправку. Проверьте получателя, тариф и предыдущий запрос.", "error");
  } finally { directTelegramSending = false; }
});
