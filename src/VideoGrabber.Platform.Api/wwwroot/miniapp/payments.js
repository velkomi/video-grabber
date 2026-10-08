const $ = (selector) => document.querySelector(selector);

let catalog = null;
const promotions = window.VideoGrabberPromotions;
const promotionController = promotions.createController(api, promotions.referralCode);
let promotionForm = null;
let billingVersion = 0;
let billingAccountId = null;
let accountRefreshSeen = false;

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
  promotionForm?.synchronize();
}

function productLabel(product) {
  const names = { free: "Free", start: "Start", unlimited_video: "Unlimited Video", full_course: "Full Course" };
  return names[product.planId] || (product.credits ? product.credits + " загрузок" : "Подписка VideoGrabber");
}
function subscriptionLabel(state) {
  return { active: "Активна", cancelled: "Автопродление отключено", canceled: "Автопродление отключено", expired: "Завершена", pending: "Ожидает подтверждения", past_due: "Нужна оплата" }[state] || "Проверяем подписку";
}
async function loadProducts(version = billingVersion) {
  const next = await api("/v1/payment-products?surface=telegram");
  if (version !== billingVersion) return;
  catalog = next;
  const select = $("#payment-product");
  const buy = $("#payment-buy");
  const availability = $("#payment-availability");
  select.replaceChildren();
  buy.disabled = true;
  availability.className = "notice";
  availability.textContent = "Проверяю доступные тарифы Telegram Stars…";
  for (const product of catalog.products || []) {
    const price = product.prices?.stars;
    if (!price) continue;
    const option = document.createElement("option");
    option.value = product.sku;
    option.textContent =
      productLabel(product) + " • " + String(price.minorUnits) + " Stars";
    select.append(option);
  }
  updateRecurring();
  const hasProducts = select.options.length > 0;
  select.disabled = !hasProducts;
  if (!hasProducts) {
    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "Пока недоступно";
    select.append(placeholder);
  }
  const primaryReady = window.VideoGrabberApi.isPrimaryAccount();
  buy.disabled = true;
  if (!primaryReady) {
    availability.className = "notice error";
    availability.textContent = "Перед оплатой свяжите аккаунт через команду /link в чате с ботом.";
  } else if (!hasProducts) {
    availability.className = "notice";
    availability.textContent = "Оплата через Stars пока недоступна. Попробуйте позже.";
  } else {
    availability.className = "notice success";
    availability.textContent = "Telegram Stars доступны для этого аккаунта.";
  }
}

async function refreshSubscriptions(version = billingVersion) {
  if (version !== billingVersion) return;
  const host = $("#subscription-list");
  host.replaceChildren();
  const rows = await api("/v1/subscriptions");
  if (version !== billingVersion) return;
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
      subscriptionLabel(subscription.state)
      + " • до " + new Date(subscription.paidThrough).toLocaleDateString("ru-RU")
      + (subscription.autoRenew ? " • автопродление включено" : "");
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
  const version = billingVersion;
  const product = selectedProduct();
  if (!product) {
    status("Выберите доступный тариф.", "error");
    return;
  }
  const recurring = $("#payment-recurring").checked;
  const intent = paymentIntentKey(product.sku, recurring);
  try {
    const fields = promotionController.disabled
      ? { idempotencyKey: intent.value }
      : promotionController.paymentFields({ sku: product.sku, provider: "stars", recurring });
    status("Открываем оплату…");
    const checkout = await api("/v1/payments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sku: product.sku,
        provider: "stars",
        ...fields,
        recurring
      })
    });
    if (version !== billingVersion) return;
    if (!checkout.redirectUri)
      throw new Error("Не удалось открыть оплату. Попробуйте позже.");

    const webApp = window.Telegram?.WebApp;
    if (!webApp?.openInvoice)
      throw new Error("Откройте Mini App внутри Telegram для оплаты");
    webApp.openInvoice(checkout.redirectUri, async (invoiceStatus) => {
      if (invoiceStatus === "paid") {
        sessionStorage.removeItem(intent.key);
        status(
          "Платёж получен. Проверяем доступ…",
          "success");
        try {
          await window.VideoGrabberApi.loadAll();
        } catch { }
      } else if (invoiceStatus === "cancelled") {
        status("Оплата отменена. Доступ не изменён.");
      } else if (invoiceStatus === "failed") {
        status("Telegram сообщил об ошибке оплаты. Доступ не изменён.", "error");
      } else {
        status("Оплата ожидает подтверждения. Проверьте доступ чуть позже.");
      }
    });
  } catch (error) {
    status("Не удалось открыть оплату. " + window.VideoGrabberApi.userError(error), "error");
  }
});

function clearBilling() {
  billingVersion++;
  promotionController.reset();
  catalog = null;
  promotionForm = null;
  for (const selector of ["#referral-card", "#promotion-checkout", "#subscription-list", "#payment-history"])
    $(selector).replaceChildren();
  $("#referral-card").hidden = true;
  $("#promotion-checkout").hidden = true;
  $("#payment-buy").disabled = true;
  $("#payment-product").disabled = true;
}

async function refreshBilling(event) {
  if (event.phase === "loading") { clearBilling(); return; }
  accountRefreshSeen = true;
  clearBilling();
  billingAccountId = event.accountId;
  const version = billingVersion;
  const current = () => version === billingVersion && billingAccountId === window.VideoGrabberApi.currentAccountId();
  try {
    await loadProducts(version);
    if (!current()) return;
    await promotionController.load();
    if (!current()) return;
    promotions.renderSummary($("#referral-card"), promotionController);
    if (!promotionController.disabled) {
      promotionForm = promotions.mountCheckout($("#promotion-checkout"), promotionController,
        () => ({ sku: selectedProduct()?.sku, provider: "stars", recurring: $("#payment-recurring").checked }),
        ready => { if (current()) $("#payment-buy").disabled = !ready || !selectedProduct() || !window.VideoGrabberApi.isPrimaryAccount(); });
    } else $("#payment-buy").disabled = !selectedProduct() || !window.VideoGrabberApi.isPrimaryAccount();
    await refreshPayments();
    await refreshSubscriptions(version);
  } catch (error) {
    if (!current()) return;
    $("#payment-buy").disabled = true;
    status("Оплата временно недоступна. Обновите данные или попробуйте позже.", "error");
  }
}

$("#payment-recurring").addEventListener("change", () => promotionForm?.synchronize());

async function boot() {
  for (let attempt = 0; attempt < 50 && !window.VideoGrabberApi; attempt++)
    await new Promise((resolve) => setTimeout(resolve, 100));
  if (!window.VideoGrabberApi) return;
  window.VideoGrabberApi.onAccountRefresh(refreshBilling);
  try {
    await window.VideoGrabberApi.ready;
    if (!accountRefreshSeen) await refreshBilling({ phase: "loaded", accountId: window.VideoGrabberApi.currentAccountId() });
  } catch (error) {
    $("#payment-buy").disabled = true;
    status("Оплата временно недоступна. Попробуйте позже.", "error");
  }
}

boot();
