(function (global) {
  "use strict";
  const intentKey = "vg_referral_intent";
  function clearStoredIntent(storage) { try { storage?.removeItem(intentKey); } catch {} }
  function capture(location, history, storage) {
    const url = new URL(location.href);
    const code = url.searchParams.get("ref") || "";
    const hasRef = url.searchParams.has("ref");
    if (hasRef) {
      url.searchParams.delete("ref");
      history.replaceState(null, "", url.pathname + url.search + url.hash);
    }
    if (hasRef) {
      if (!/^[A-Za-z0-9_-]{1,64}$/.test(code)) { clearStoredIntent(storage); return ""; }
      try { storage?.setItem(intentKey, JSON.stringify({ code, at: Date.now() })); } catch {}
      return code;
    }
    try {
      const pending = JSON.parse(storage?.getItem(intentKey) || "null");
      if (pending && /^[A-Za-z0-9_-]{1,64}$/.test(pending.code) && Number.isFinite(pending.at) && pending.at <= Date.now() && Date.now() - pending.at < 30 * 86400000) return pending.code;
    } catch {}
    clearStoredIntent(storage); return "";
  }
  function userError(error) {
    if (error?.status === 401) return "Войдите в аккаунт и повторите действие.";
    if (error?.status === 409) return "Предложение уже использовано или недоступно для этого аккаунта.";
    if (error?.status === 400) return "Проверьте промокод и выбранный тариф.";
    if (error?.status === 503) return "Бонусы и промокоды пока недоступны.";
    return "Не удалось обновить данные. Попробуйте ещё раз.";
  }
  function money(amount) {
    if (!amount || !Number.isSafeInteger(amount.minorUnits)) return "—";
    if (amount.currency === "XTR") return amount.minorUnits + " Stars";
    if (amount.currency === "RUB") return (amount.minorUnits / 100).toLocaleString("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " ₽";
    return "—";
  }
  function safeLink(value, telegram = false) {
    try {
      const url = new URL(value);
      if (url.protocol !== "https:" || url.username || url.password || (telegram && url.hostname !== "t.me")) return null;
      return url.href;
    } catch { return null; }
  }
  function createController(api, referralCode = "", storage = global.sessionStorage) {
    let pendingCode = referralCode, summary = null, disabled = false, currentQuote = null, generation = 0, ordinary = false, intent = null, accountVersion = 0, intentContext = null, quoteContext = null;
    const contextFor = request => JSON.stringify([request.sku, request.provider, !!request.recurring]);
    function invalidate() { generation++; currentQuote = null; ordinary = false; intent = null; intentContext = null; quoteContext = null; }
    return {
      get summary() { return summary; }, get disabled() { return disabled; }, get version() { return generation; }, claimMessage: "",
      reset() { accountVersion++; summary = null; disabled = false; this.claimMessage = ""; invalidate(); },
      clearIntent() { pendingCode = ""; this.reset(); clearStoredIntent(storage); },
      async load() {
        const version = ++accountVersion;
        summary = null;
        if (pendingCode) {
          try {
            await api("/v1/referrals/claim", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: pendingCode }) });
            if (version !== accountVersion) return null;
            pendingCode = ""; clearStoredIntent(storage); this.claimMessage = "Приглашение сохранено. Условия скидки проверим при оплате.";
          } catch (error) {
            if (version !== accountVersion) return null;
            if (error.status === 503) { disabled = true; invalidate(); return null; }
            if (error.status === 400 || error.status === 409) { pendingCode = ""; clearStoredIntent(storage); }
            this.claimMessage = userError(error);
          }
        }
        try { const value = await api("/v1/referrals"); if (version !== accountVersion) return null; summary = value; disabled = false; return summary; }
        catch (error) { if (version !== accountVersion) return null; if (error.status === 503) { disabled = true; invalidate(); return null; } throw error; }
      },
      invalidate,
      async quote(request) {
        invalidate(); const version = generation;
        if (disabled) { ordinary = true; return null; }
        if (request.recurring) {
          if (request.promoCode?.trim() || request.useBonuses) throw new Error("Для промокода и бонусов отключите автопродление.");
          ordinary = true; return null;
        }
        const payload = { sku: request.sku, provider: request.provider, promoCode: request.promoCode?.trim() || null, useBonuses: !!request.useBonuses, recurring: !!request.recurring };
        try {
          const result = await api("/v1/promotions/quote", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
          if (version === generation) { currentQuote = result; quoteContext = contextFor(request); }
          return version === generation ? result : null;
        } catch (error) {
          if (version !== generation) return null;
          if (error.status === 503) { disabled = true; ordinary = true; return null; }
          throw error;
        }
      },
      paymentFields(request) {
        if (!disabled && !ordinary && (!currentQuote?.quoteId || !(Date.parse(currentQuote.expiresAt) > Date.now()))) throw new Error("Обновите расчёт перед оплатой.");
        if (request) {
          const context = contextFor(request);
          if (currentQuote && quoteContext !== context) throw new Error("Обновите расчёт для выбранного тарифа.");
          if (intentContext !== context) { intent = null; intentContext = context; }
        }
        intent ||= global.crypto.randomUUID();
        return { idempotencyKey: intent, ...(currentQuote ? { quoteId: currentQuote.quoteId } : {}) };
      }
    };
  }
  function node(document, tag, text, className) {
    const element = document.createElement(tag);
    if (text != null) element.textContent = text;
    if (className) element.className = className;
    return element;
  }
  function renderSummary(host, controller) {
    host.replaceChildren(); host.hidden = controller.disabled;
    if (controller.disabled || !controller.summary) return;
    const document = host.ownerDocument, value = controller.summary;
    host.append(node(document, "h3", "Приглашения и бонусы"));
    if (controller.claimMessage) host.append(node(document, "p", controller.claimMessage));
    host.append(node(document, "p", "Другу — 10% на первую разовую оплату подписки. Вам — 10% от оплаченной суммы. Бонусы доступны через 14 дней и действуют 365 дней.", "promotion-note"));
    host.append(node(document, "p", "Приглашено: " + value.invited + " · Оплатили: " + value.paid));
    const actions = node(document, "div", null, "promotion-actions");
    const web = safeLink(value.webLink), telegram = safeLink(value.telegramLink, true);
    if (web) {
      const copy = node(document, "button", "Копировать ссылку", "button button-quiet secondary"); copy.type = "button";
      const result = node(document, "p", "", "promotion-note"); result.setAttribute("aria-live", "polite");
      copy.addEventListener("click", async () => {
        try { await global.navigator.clipboard.writeText(web); result.textContent = "Ссылка скопирована."; }
        catch { result.textContent = "Копирование недоступно. Откройте ссылку и скопируйте адрес."; }
      });
      actions.append(copy);
      const link = node(document, "a", "Открыть ссылку", "text-link"); link.href = web; link.target = "_blank"; link.rel = "noopener noreferrer";
      actions.append(link); host.append(actions, result);
    }
    if (telegram) {
      const link = node(document, "a", "Поделиться в Telegram", "text-link");
      link.href = "https://t.me/share/url?url=" + encodeURIComponent(telegram); link.target = "_blank"; link.rel = "noopener noreferrer"; host.append(link);
    }
    const balances = value.balances || [];
    if (!balances.length) host.append(node(document, "p", "Бонусов пока нет.", "promotion-note"));
    for (const balance of balances) {
      const line = amount => money({ currency: balance.currency, minorUnits: amount });
      host.append(node(document, "p", "Доступно: " + line(balance.availableMinor) + " · Ожидает: " + line(balance.pendingMinor)));
      if (balance.reservedMinor > 0) host.append(node(document, "p", "В счетах на оплату: " + line(balance.reservedMinor), "promotion-note"));
      if (balance.debtMinor > 0) host.append(node(document, "p", "Возврат уменьшит будущие бонусы: " + line(balance.debtMinor), "promotion-note"));
    }
    const history = node(document, "details"); history.append(node(document, "summary", "История бонусов"));
    const labels = { reward: "Начисление", earn: "Начисление", spend: "Оплата бонусами", refund: "Возврат", restore: "Возврат бонусов", expire: "Истечение", clawback: "Пересчёт после возврата", offset: "Зачёт возврата", offset_debit: "Зачёт возврата", offset_credit: "Зачёт возврата", reserve: "Резерв", release: "Возврат резерва" };
    for (const event of (value.history || []).slice(0, 20)) {
      let text = (labels[event.kind] || "Изменение бонусов") + " · " + money(event.amount) + " · " + new Date(event.createdAt).toLocaleDateString("ru-RU");
      if (event.availableAt) text += " · доступно с " + new Date(event.availableAt).toLocaleDateString("ru-RU");
      if (event.expiresAt) text += " · до " + new Date(event.expiresAt).toLocaleDateString("ru-RU");
      history.append(node(document, "p", text, "promotion-note"));
    }
    if (!value.history?.length) history.append(node(document, "p", "Начислений пока нет.", "promotion-note"));
    host.append(history, node(document, "p", "Бонусы не выводятся. Скидка и бонусы вместе — до 30% цены. RUB и Stars учитываются отдельно.", "promotion-note"));
  }
  function mountCheckout(host, controller, getRequest, changed = () => {}) {
    const document = host.ownerDocument; host.replaceChildren(); host.hidden = controller.disabled;
    const promo = node(document, "input"); promo.type = "text"; promo.maxLength = 64; promo.autocomplete = "off";
    const label = node(document, "label", "Промокод"); label.append(promo);
    const use = node(document, "input"); use.type = "checkbox";
    const useLabel = node(document, "label", "Использовать доступные бонусы", "promotion-check"); useLabel.prepend(use);
    const calculate = node(document, "button", "Рассчитать сумму", "button button-quiet secondary"); calculate.type = "button";
    const result = node(document, "div", "Обновите расчёт перед оплатой.", "promotion-breakdown"); result.setAttribute("aria-live", "polite"); result.setAttribute("role", "status");
    const error = node(document, "p", "", "promotion-error"); error.setAttribute("role", "alert");
    const note = node(document, "p", "Для разовой скидки и бонусов отключите автопродление. Промокод и приглашение не складываются — выбирается большая скидка.", "promotion-note");
    host.append(label, useLabel, note, calculate, result, error);
    let revision = 0;
    function invalidate() { revision++; controller.invalidate(); result.replaceChildren(node(document, "p", "Обновите расчёт перед оплатой.")); changed(false); }
    function synchronize() {
      invalidate(); const recurring = !!getRequest().recurring;
      promo.disabled = recurring; use.disabled = recurring; if (recurring) { promo.value = ""; use.checked = false; }
      error.textContent = "";
    }
    promo.addEventListener("input", invalidate); use.addEventListener("change", invalidate);
    async function calculateQuote() {
      const version = revision; calculate.disabled = true; error.textContent = ""; changed(false);
      try {
        const pending = controller.quote({ ...getRequest(), promoCode: promo.value, useBonuses: use.checked });
        const controllerVersion = controller.version;
        const quote = await pending;
        if (version !== revision || controllerVersion !== controller.version) return;
        result.replaceChildren();
        if (quote) {
          for (const [name, amount] of [["Цена", quote.original], ["Скидка", quote.discount], ["Бонусы", quote.bonus], ["К оплате", quote.payable]]) result.append(node(document, "p", name + ": " + money(amount)));
          result.append(node(document, "p", "Расчёт действует до " + new Date(quote.expiresAt).toLocaleTimeString("ru-RU"), "promotion-note"));
        } else result.append(node(document, "p", "Оплата по цене тарифа без разовых скидок."));
        host.hidden = controller.disabled; changed(true);
      } catch (failure) { if (version === revision) error.textContent = /автопродлен|расчёт/u.test(failure.message) ? failure.message : userError(failure); }
      finally { calculate.disabled = false; }
    }
    calculate.addEventListener("click", calculateQuote); synchronize();
    return { synchronize, calculate: calculateQuote };
  }
  global.VideoGrabberPromotions = { capture, createController, userError, money, safeLink, renderSummary, mountCheckout };
  if (global.location && global.history) {
    let storage; try { storage = global.sessionStorage; } catch {}
    global.VideoGrabberPromotions.referralCode = capture(global.location, global.history, storage);
  }
})(globalThis);
