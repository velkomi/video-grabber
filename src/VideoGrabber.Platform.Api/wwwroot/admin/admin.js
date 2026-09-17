const $ = (id) => document.getElementById(id);
const status = $("status");
const accountSection = $("account");
let currentAccountId = null;

function cookie(name) {
  const item = document.cookie.split("; ").find(v => v.startsWith(name + "="));
  return item ? decodeURIComponent(item.split("=", 2)[1]) : "";
}

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  headers.set("Accept", "application/json");
  const csrf = cookie("vg_csrf");
  if (csrf && options.method && options.method !== "GET") headers.set("X-CSRF-Token", csrf);
  const response = await fetch(path, { credentials: "same-origin", ...options, headers });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  if (response.status === 204) return null;
  return response.json();
}

async function loadAccount(accountId) {
  const account = await api(`/v1/admin/accounts/${encodeURIComponent(accountId)}`);
  currentAccountId = account.accountId;
  $("account-id").textContent = account.accountId;
  $("account-role").textContent = account.role;
  $("account-blocked").textContent = account.blocked ? "Да" : "Нет";
  accountSection.hidden = false;
  await loadAudit();
}
async function loadAudit() {
  const events = await api(`/v1/admin/accounts/${encodeURIComponent(currentAccountId)}/audit`);
  $("audit").textContent = events.length
    ? events.map(e => `${e.createdAt} ${e.eventType} ${e.details}`).join("\n")
    : "Событий нет.";
}

$("lookup-form").addEventListener("submit", async (event) => {
  event.preventDefault(); status.textContent = "Поиск…"; accountSection.hidden = true;
  try {
    const identity = $("identity").value.trim();
    const accounts = await api(`/v1/admin/accounts?identity=${encodeURIComponent(identity)}`);
    if (accounts.length === 0) { status.textContent = "Аккаунт не найден."; return; }
    if (accounts.length > 1) { status.textContent = "Найдено несколько аккаунтов; уточните идентификатор."; return; }
    await loadAccount(accounts[0].accountId); status.textContent = "Аккаунт загружен.";
  } catch (error) { status.textContent = `Ошибка: ${error.message}`; }
});

accountSection.addEventListener("click", async (event) => {
  const action = event.target.dataset?.action; if (!action || !currentAccountId) return;
  const reason = $("reason").value.trim();
  if (!reason) { status.textContent = "Укажите причину изменения."; return; }
  const suffix = action === "reset" ? "devices/reset" : action;
  status.textContent = "Сохранение…";
  try {
    await api(`/v1/admin/accounts/${encodeURIComponent(currentAccountId)}/${suffix}`,
      { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reason }) });
    await loadAccount(currentAccountId); status.textContent = "Изменение сохранено и записано в аудит.";
  } catch (error) { status.textContent = `Ошибка: ${error.message}`; }
});
