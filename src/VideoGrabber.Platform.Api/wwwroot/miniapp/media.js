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
  if (ids.some((id) => !guid.test(id))) throw new Error("Некорректный artifact ID");
  return ids;
}

function fillSelect(select, values) {
  select.replaceChildren();
  for (const value of values) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = value;
    select.append(option);
  }
}

async function loadCapabilities() {
  capabilities = await api("/v1/capabilities");
  fillSelect(
    $("#media-operation"),
    capabilities.operations.filter((x) => x.available).map((x) => x.operation));
  updateExecutors();
}

function updateExecutors() {
  const operation = capabilities?.operations
    ?.find((x) => x.operation === $("#media-operation").value);
  fillSelect($("#media-executor"), operation?.executors || []);
}

$("#media-operation").addEventListener("change", updateExecutors);

$("#media-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const raw = $("#media-url").value.trim();
  try {
    status("Анализирую источник…");
    const rows = await api("/v1/sources/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ source: raw })
    });
    if (!rows.length) throw new Error("Media не найдено");
    analyzed = rows[0];
    fillSelect($("#media-quality"), analyzed.qualities);
    $("#media-options").hidden = false;
    status("Источник проверен. Выберите операцию и качество.", "success");
  } catch (error) {
    status("Анализ не выполнен: " + error.message, "error");
  }
});

$("#media-create").addEventListener("click", async () => {
  if (!analyzed) return;
  try {
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
        throw new Error("Укажите корректные границы trim в миллисекундах");
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
    status("Создаю задание…");
    const job = await api("/v1/jobs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });
    sessionStorage.removeItem("vg_pending_media_intent");
    status("Задание создано: " + job.state, "success");
    await refreshJobs();
  } catch (error) {
    status("Задание не создано: " + error.message, "error");
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
  return states[job.state] || job.state;
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
      job.jobId + " • " + russianState(job) + " • " + job.reason;
    row.append(label);
    if (["queued","waiting_for_worker","running"].includes(job.state)) {
      const cancel = document.createElement("button");
      cancel.type = "button";
      cancel.textContent = "Отменить";
      cancel.addEventListener("click", () => mutateJob(job.jobId, "cancel"));
      row.append(cancel);
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
    await loadCapabilities();
    await refreshJobs();
    connectEvents();
  } catch (error) {
    status("Media UI недоступен: " + error.message, "error");
  }
}

boot();
