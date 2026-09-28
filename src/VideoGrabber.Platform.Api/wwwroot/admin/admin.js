"use strict";

const $ = (id) => document.getElementById(id);

const features = [
  ["download", "Скачивание видео", "Обычные загрузки видео."],
  ["mp3", "MP3 / аудио", "Извлечение или скачивание аудио."],
  ["trim", "Обрезка видео", "Редактор: обрезка фрагментов."],
  ["join", "Объединение видео", "Редактор: склейка файлов."],
  ["transcribe", "Транскрибация", "Локальное распознавание речи."],
  ["course_download", "Видео-уроки / курс", "Сохранение доступных клиенту материалов курса."],
  ["browser_download", "Скачать через браузер", "Получение готового файла прямо с сайта."],
  ["telegram_delivery", "Доставка в Telegram", "Отправка готового результата в привязанный Telegram."]
];

let currentAccountId = null;
let mfaEnrolled = false;
let mfaExpiresAt = 0;
let mfaTimerHandle = null;

function baseToken() {
  return sessionStorage.getItem("vg_web_access") || "";
}

function adminToken() {
  return sessionStorage.getItem("vg_admin_access") || "";
}

function setAdminToken(session) {
  sessionStorage.setItem("vg_admin_access", session.accessToken);
  sessionStorage.setItem("vg_admin_expires", session.expiresAt);
  mfaExpiresAt = new Date(session.expiresAt).getTime();
}

function clearAdminToken() {
  sessionStorage.removeItem("vg_admin_access");
  sessionStorage.removeItem("vg_admin_expires");
  mfaExpiresAt = 0;
}

async function api(path, options = {}, mode = "admin") {
  const headers = new Headers(options.headers || {});
  headers.set("Accept", "application/json");
  const token = mode === "base" ? baseToken() : (adminToken() || baseToken());
  if (token) headers.set("Authorization", "Bearer " + token);
  const response = await fetch(path, { ...options, headers, credentials: "same-origin" });

  if (response.status === 401 && mode === "admin") {
    clearAdminToken();
    showMfaGate("Срок 2FA-сессии истёк. Введите новый код.");
  }
  if (!response.ok) {
    let detail = "";
    try {
      const body = await response.json();
      detail = body.detail || body.code || "";
    } catch {}
    throw new Error("HTTP " + response.status + (detail ? " · " + detail : ""));
  }
  if (response.status === 204) return null;
  return response.json();
}

function status(text, kind = "") {
  const node = $("status");
  node.textContent = text || "";
  node.className = "status" + (kind ? " " + kind : "");
}

function mfaStatus(text, kind = "") {
  const node = $("mfa-status");
  node.textContent = text || "";
  node.className = "status" + (kind ? " " + kind : "");
}

function requireReason() {
  const value = $("reason").value.trim();
  if (!value) {
    status("Укажите причину изменения.", "error");
    throw new Error("reason_required");
  }
  return value;
}

function showMfaGate(message = "") {
  $("admin-console").hidden = true;
  $("mfa-card").hidden = false;
  $("mfa-state").textContent = mfaEnrolled ? "TOTP подключён" : "Нужно подключить";
  if (message) mfaStatus(message, "error");
  $("mfa-code").focus();
}

function showConsole() {
  $("mfa-card").hidden = true;
  $("admin-console").hidden = false;
  startMfaTimer();
}

function startMfaTimer() {
  if (mfaTimerHandle) clearInterval(mfaTimerHandle);
  const tick = () => {
    const remaining = Math.max(0, mfaExpiresAt - Date.now());
    const seconds = Math.ceil(remaining / 1000);
    const node = $("mfa-timer");
    node.textContent = "2FA: " + Math.floor(seconds / 60) + ":" + String(seconds % 60).padStart(2, "0");
    if (remaining <= 0) {
      clearInterval(mfaTimerHandle);
      clearAdminToken();
      showMfaGate("2FA-сессия завершена. Введите новый код.");
    }
  };
  tick();
  mfaTimerHandle = setInterval(tick, 1000);
}

async function initMfa() {
  if (!baseToken()) {
    $("auth-error").hidden = false;
    return;
  }

  try {
    const state = await api("/v1/admin/mfa/status", {}, "base");
    if (!state.isAdmin) {
      $("auth-error").hidden = false;
      return;
    }
    mfaEnrolled = state.enrolled;
    $("mfa-card").hidden = false;

    const savedExpiry = new Date(sessionStorage.getItem("vg_admin_expires") || 0).getTime();
    if (adminToken() && savedExpiry > Date.now() + 5000) {
      mfaExpiresAt = savedExpiry;
      showConsole();
      return;
    }

    if (!mfaEnrolled) {
      const enrollment = await api("/v1/admin/mfa/enroll", {
        method: "POST"
      }, "base");
      $("mfa-enroll").hidden = false;
      $("mfa-secret").value = enrollment.secret;
      $("mfa-uri").href = enrollment.otpAuthUri;
      $("mfa-state").textContent = "Первичная привязка";
      mfaStatus("Добавьте ключ в Authenticator и введите первый код.");
    } else {
      $("mfa-enroll").hidden = true;
      $("mfa-state").textContent = "TOTP подключён";
      mfaStatus("Введите код из приложения-аутентификатора.");
    }
  } catch (error) {
    $("auth-error").hidden = false;
    $("auth-error").querySelector("p").textContent =
      "Админ-панель недоступна: " + error.message;
  }
}

$("copy-secret").addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText($("mfa-secret").value);
    mfaStatus("Секретный ключ скопирован.", "success");
  } catch {
    $("mfa-secret").select();
  }
});

$("mfa-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const code = $("mfa-code").value.trim();
  mfaStatus("Проверяю код…");
  try {
    const session = await api(
      mfaEnrolled ? "/v1/admin/mfa/verify" : "/v1/admin/mfa/confirm",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ code })
      },
      "base"
    );
    setAdminToken(session);
    mfaEnrolled = true;
    $("mfa-code").value = "";
    showConsole();
    status("2FA подтверждена. Можно работать с клиентами.", "success");
  } catch (error) {
    mfaStatus("Код не принят: " + error.message, "error");
  }
});

function formatDate(value) {
  return value ? new Date(value).toLocaleString("ru-RU") : "—";
}

async function loadAccount(accountId) {
  const [account, grants, overrides, audit] = await Promise.all([
    api("/v1/admin/accounts/" + encodeURIComponent(accountId)),
    api("/v1/admin/accounts/" + encodeURIComponent(accountId) + "/grants"),
    api("/v1/admin/accounts/" + encodeURIComponent(accountId) + "/features"),
    api("/v1/admin/accounts/" + encodeURIComponent(accountId) + "/audit")
  ]);

  currentAccountId = account.accountId;
  $("account-id").textContent = account.accountId;
  $("account-role").textContent = account.role;
  $("account-blocked").textContent = account.blocked ? "Да" : "Нет";
  $("account-purchase").textContent = formatDate(account.firstPurchaseAt);
  $("account").hidden = false;
  renderFeatures(overrides);
  renderGrants(grants);
  renderAudit(audit);
}

function renderFeatures(rows) {
  const byFeature = new Map(rows.map(row => [row.feature, row]));
  const host = $("feature-grid");
  host.replaceChildren();

  for (const [feature, title, description] of features) {
    const row = byFeature.get(feature);
    const card = document.createElement("div");
    card.className = "feature-card";

    const text = document.createElement("div");
    const heading = document.createElement("b");
    heading.textContent = title;
    const note = document.createElement("small");
    note.textContent = description + (row?.validUntil ? " · до " + formatDate(row.validUntil) : "");
    text.append(heading, note);

    const select = document.createElement("select");
    select.dataset.feature = feature;
    for (const [value, label] of [
      ["inherit", "По тарифу"],
      ["allow", "Разрешить"],
      ["deny", "Запретить"]
    ]) {
      const option = document.createElement("option");
      option.value = value;
      option.textContent = label;
      select.append(option);
    }
    select.value = row ? (row.enabled ? "allow" : "deny") : "inherit";
    card.append(text, select);
    host.append(card);
  }
}

async function loadAudit() {
  if (!currentAccountId) return;
  renderAudit(await api(
    "/v1/admin/accounts/" + encodeURIComponent(currentAccountId) + "/audit"
  ));
}

function renderAudit(events) {
  $("audit").textContent = events.length
    ? events.map(event =>
        new Date(event.createdAt).toLocaleString("ru-RU")
        + " · " + event.eventType
        + " · " + event.details
      ).join("\n")
    : "Событий нет.";
}

function grantPlanLabel(planId) {
  return ({
    free: "Free",
    start: "Start",
    unlimited_video: "Unlimited Video",
    full_course: "Full Course"
  })[planId] || planId || "—";
}

function renderGrants(rows) {
  const host = $("grants");
  host.replaceChildren();
  if (!rows.length) {
    host.textContent = "Grants отсутствуют.";
    return;
  }

  const table = document.createElement("table");
  table.innerHTML =
    "<thead><tr><th>Источник</th><th>Тариф</th><th>Тип</th><th>Срок</th><th>Остаток</th><th></th></tr></thead>";
  const body = document.createElement("tbody");

  for (const grant of rows) {
    const tr = document.createElement("tr");
    const until = grant.validUntil ? formatDate(grant.validUntil) : "без срока";
    tr.innerHTML =
      "<td></td><td></td><td></td><td></td><td></td><td></td>";
    tr.children[0].textContent = grant.source + (grant.revoked ? " · отозван" : "");
    tr.children[1].textContent = grantPlanLabel(grant.planId);
    tr.children[2].textContent = grant.kind;
    tr.children[3].textContent = until;
    tr.children[4].textContent = String(grant.available);

    if (grant.source === "admin_gift" && !grant.revoked) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "text-button danger-text";
      button.textContent = "Отозвать";
      button.addEventListener("click", () => revokeGrant(grant.grantId));
      tr.children[5].append(button);
    }
    body.append(tr);
  }
  table.append(body);
  host.append(table);
}

async function revokeGrant(grantId) {
  const reason = requireReason();
  if (!confirm("Отозвать этот ручной grant?")) return;
  status("Отзываю grant…");
  try {
    await api("/v1/admin/grants/" + encodeURIComponent(grantId) + "/revoke", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason })
    });
    await loadAccount(currentAccountId);
    status("Grant отозван и записан в аудит.", "success");
  } catch (error) {
    status("Ошибка: " + error.message, "error");
  }
}

$("lookup-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  status("Поиск…");
  $("account").hidden = true;
  try {
    const identity = $("identity").value.trim();
    const accounts = await api(
      "/v1/admin/accounts?identity=" + encodeURIComponent(identity)
    );
    if (!accounts.length) {
      status("Аккаунт не найден.", "error");
      return;
    }
    if (accounts.length > 1) {
      status("Найдено несколько аккаунтов. Уточните идентификатор.", "error");
      return;
    }
    await loadAccount(accounts[0].accountId);
    status("Аккаунт загружен.", "success");
  } catch (error) {
    status("Ошибка: " + error.message, "error");
  }
});

$("account").addEventListener("click", async (event) => {
  const action = event.target.dataset?.action;
  if (!action || !currentAccountId) return;
  try {
    const reason = requireReason();
    const suffix = action === "reset" ? "devices/reset" : action;
    status("Сохраняю…");
    await api(
      "/v1/admin/accounts/" + encodeURIComponent(currentAccountId) + "/" + suffix,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason })
      }
    );
    await loadAccount(currentAccountId);
    status("Изменение сохранено и записано в аудит.", "success");
  } catch (error) {
    if (error.message !== "reason_required")
      status("Ошибка: " + error.message, "error");
  }
});

async function createGrant(payload) {
  payload.accountId = currentAccountId;
  payload.reason = requireReason();
  payload.idempotencyKey = crypto.randomUUID();
  return api("/v1/admin/grants", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
}

$("grant-plan-button").addEventListener("click", async () => {
  if (!currentAccountId) return;
  try {
    const duration = $("grant-duration").value;
    const permanent = duration === "permanent";
    status("Выдаю тариф…");
    await createGrant({
      kind: permanent ? "permanent" : "time",
      days: permanent ? 0 : Number(duration),
      credits: 0,
      expiresAt: null,
      planId: $("grant-plan").value
    });
    await loadAccount(currentAccountId);
    status("Ручной тариф выдан.", "success");
  } catch (error) {
    if (error.message !== "reason_required")
      status("Ошибка: " + error.message, "error");
  }
});

$("grant-all-button").addEventListener("click", async () => {
  if (!currentAccountId) return;
  try {
    const reason = requireReason();
    if (!confirm("Выдать этому клиенту безлимит на весь текущий функционал навсегда?")) return;

    status("Выдаю безлимит…");
    await api("/v1/admin/grants", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        accountId: currentAccountId,
        kind: "permanent",
        days: 0,
        credits: 0,
        expiresAt: null,
        reason,
        idempotencyKey: crypto.randomUUID(),
        planId: "full_course"
      })
    });

    for (const [feature] of features) {
      await api(
        "/v1/admin/accounts/" + encodeURIComponent(currentAccountId)
          + "/features/" + encodeURIComponent(feature),
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ enabled: true, validUntil: null, reason })
        }
      );
    }
    await loadAccount(currentAccountId);
    status("Безлимит на весь текущий функционал выдан.", "success");
  } catch (error) {
    if (error.message !== "reason_required")
      status("Ошибка: " + error.message, "error");
  }
});

$("grant-credits-button").addEventListener("click", async () => {
  if (!currentAccountId) return;
  try {
    const credits = Number($("grant-credits").value);
    if (!Number.isInteger(credits) || credits <= 0)
      throw new Error("Укажите положительное целое число кредитов.");
    status("Добавляю кредиты…");
    await createGrant({
      kind: "credits",
      days: 0,
      credits,
      expiresAt: null,
      planId: null
    });
    await loadAccount(currentAccountId);
    status("Кредиты добавлены.", "success");
  } catch (error) {
    if (error.message !== "reason_required")
      status("Ошибка: " + error.message, "error");
  }
});

function featureUntilIso() {
  const value = $("feature-until").value;
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime()))
    throw new Error("Некорректный срок точечных прав.");
  return date.toISOString();
}

$("save-features").addEventListener("click", async () => {
  if (!currentAccountId) return;
  try {
    const reason = requireReason();
    const validUntil = featureUntilIso();
    status("Сохраняю точечные права…");

    for (const select of $("feature-grid").querySelectorAll("select[data-feature]")) {
      const feature = select.dataset.feature;
      if (select.value === "inherit") {
        await api(
          "/v1/admin/accounts/" + encodeURIComponent(currentAccountId)
            + "/features/" + encodeURIComponent(feature),
          {
            method: "DELETE",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason })
          }
        );
      } else {
        await api(
          "/v1/admin/accounts/" + encodeURIComponent(currentAccountId)
            + "/features/" + encodeURIComponent(feature),
          {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              enabled: select.value === "allow",
              validUntil,
              reason
            })
          }
        );
      }
    }

    await loadAccount(currentAccountId);
    status("Точечные права сохранены.", "success");
  } catch (error) {
    if (error.message !== "reason_required")
      status("Ошибка: " + error.message, "error");
  }
});

$("inherit-features").addEventListener("click", () => {
  for (const select of $("feature-grid").querySelectorAll("select[data-feature]"))
    select.value = "inherit";
  status("Выбрано «По тарифу». Нажмите «Сохранить права», чтобы применить.");
});

$("refresh-account").addEventListener("click", async () => {
  if (!currentAccountId) return;
  try {
    await loadAccount(currentAccountId);
    status("Данные обновлены.", "success");
  } catch (error) {
    status("Ошибка: " + error.message, "error");
  }
});

initMfa();
