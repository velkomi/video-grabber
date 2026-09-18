const $ = (selector) => document.querySelector(selector);

let catalog = null;

function api(path, options) {
  if (!window.VideoGrabberApi) throw new Error("Mini App session ещё не готова");
  return window.VideoGrabberApi.api(path, options);
}

function status(message, kind = "") {
  window.VideoGrabberApi?.setStatus(message, kind);
}

function paymentIntentKey(sku, recurring) {
  const key = "vg_stars_purchase_" + sku + "_" + (recurring ? "r" : "o");
  let value = sessionStorage.getItem(key);
  if (!value) {
    value = crypto.randomUUID();
    sessionStorage.setItem(key, value);
  }
  return { key, value };
}

function selectedProduct() {
  return catalog?.products?.find(
    (product) => product.sku === $("#payment-product").value) || null;
}

function updateRecurring() {
  const product = selectedProduct();
  const checkbox = $("#payment-recurring");
  checkbox.disabled = !product?.recurringAllowed;
  if (checkbox.disabled) checkbox.checked = false;
}

async function loadProducts() {
  catalog = await api("/v1/payment-products?surface=telegram");
  const select = $("#payment-product");
  select.replaceChildren();
  for (const product of catalog.products || []) {
    const price = product.prices?.stars;
    if (!price) continue;
    const option = document.createElement("option");
    option.value = product.sku;
    option.textContent =
      product.sku + " • " + String(price.minorUnits) + " " + price.currency;
    select.append(option);
  }
  updateRecurring();
}

async function refreshSubscriptions() {
  const host = $("#subscription-list");
  host.replaceChildren();
  const rows = await api("/v1/subscriptions");
  if (!rows.length) {
    host.textContent = "Активных подписок пока нет.";
    return;
  }
  for (const subscription of rows) {
    const row = document.createElement("div");
    row.className = "item";
    const label = document.createElement("span");
    label.className = "item-text";
    label.textContent =
      subscription.provider + " • " + subscription.state
      + " • оплачено до " + new Date(subscription.paidThrough).toLocaleString()
      + " • auto-renew=" + (subscription.autoRenew ? "on" : "off");
    row.append(label);
    if (subscription.autoRenew && subscription.state === "active") {
      const cancel = document.createElement("button");
      cancel.type = "button";
      cancel.textContent = "Отключить автопродление";
      cancel.addEventListener("click", async () => {
        if (!confirm("Отключить автопродление? Уже оплаченный период сохранится.")) return;
        await api(
          "/v1/subscriptions/" + encodeURIComponent(subscription.subscriptionId) + "/cancel",
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ idempotencyKey: crypto.randomUUID() })
          });
        await refreshSubscriptions();
        status("Автопродление отключено. Оплаченный период сохранён.", "success");
      });
      row.append(cancel);
    }
    host.append(row);
  }
}
async function refreshPayments() {
  const host = $("#payment-history");
  host.replaceChildren();
  // Payment history is surfaced through bot /payments for now; Mini App shows
  // current purchase result via its account/access refresh after invoice closes.
  const note = document.createElement("span");
  note.className = "item-text";
  note.textContent =
    "Историю и поддержку можно открыть командой /payments или /paysupport в личном чате.";
  host.append(note);
}

$("#payment-product").addEventListener("change", updateRecurring);

$("#payment-buy").addEventListener("click", async () => {
  const product = selectedProduct();
  if (!product) {
    status("Товар Stars недоступен.", "error");
    return;
  }
  const recurring = $("#payment-recurring").checked;
  const intent = paymentIntentKey(product.sku, recurring);
  try {
    status("Создаю защищённый Stars invoice…");
    const checkout = await api("/v1/payments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sku: product.sku,
        provider: "stars",
        idempotencyKey: intent.value,
        recurring
      })
    });
    if (!checkout.redirectUri)
      throw new Error("Telegram invoice link отсутствует");

    const webApp = window.Telegram?.WebApp;
    if (!webApp?.openInvoice)
      throw new Error("Откройте Mini App внутри Telegram для оплаты");
    webApp.openInvoice(checkout.redirectUri, async (invoiceStatus) => {
      if (invoiceStatus === "paid") {
        sessionStorage.removeItem(intent.key);
        status(
          "Telegram сообщил об оплате. Проверяю серверное подтверждение…",
          "success");
        try {
          await window.VideoGrabberApi.loadAll();
        } catch { }
      } else if (invoiceStatus === "cancelled") {
        status("Оплата отменена. Доступ не изменён.");
      } else if (invoiceStatus === "failed") {
        status("Telegram сообщил об ошибке оплаты. Доступ не изменён.", "error");
      } else {
        status("Статус invoice: " + String(invoiceStatus));
      }
    });
  } catch (error) {
    status("Stars invoice не создан: " + error.message, "error");
  }
});

async function boot() {
  for (let attempt = 0; attempt < 50 && !window.VideoGrabberApi; attempt++)
    await new Promise((resolve) => setTimeout(resolve, 100));
  if (!window.VideoGrabberApi) return;
  try {
    await loadProducts();
    await refreshPayments();
    await refreshSubscriptions();
  } catch (error) {
    status("Платежи недоступны: " + error.message, "error");
  }
}

boot();
