(function (global) {
  "use strict";
  function definition(value) {
    const code = value.code.trim().toUpperCase();
    if (!/^[A-Z0-9_-]{1,64}$/.test(code)) throw new Error("Введите код: буквы, цифры, дефис или подчёркивание, до 64 символов.");
    const discount = Number(value.discount);
    if (![10, 15, 20].includes(discount)) throw new Error("Выберите скидку 10, 15 или 20%.");
    if (!["RUB", "XTR"].includes(value.currency)) throw new Error("Выберите валюту RUB или Stars.");
    const positive = input => { const number = Number(input); if (!Number.isSafeInteger(number) || number <= 0) throw new Error("Лимиты и бюджет должны быть положительными целыми числами."); return number; };
    const starts = new Date(value.starts + "Z"), ends = new Date(value.ends + "Z");
    if (!Number.isFinite(starts.getTime()) || !Number.isFinite(ends.getTime()) || starts >= ends) throw new Error("Укажите срок UTC: окончание должно быть позже начала.");
    const sku = value.sku.trim(); if (sku.length > 80) throw new Error("SKU не должен превышать 80 символов.");
    return { code, discountBasisPoints: discount * 100, sku: sku || null, currency: value.currency, startsAt: starts.toISOString(), endsAt: ends.toISOString(), maxUses: positive(value.maxUses), perAccountLimit: positive(value.perAccountLimit), budgetMinor: positive(value.budget), firstPurchaseOnly: true, active: !!value.active };
  }
  function userError(error) {
    if (error?.status === 401 || error?.status === 403) return "Нужны права владельца и действующая 2FA-сессия. Подтвердите 2FA ещё раз.";
    if (error?.status === 503) return "Управление промокодами пока недоступно.";
    if (error?.status === 400 || error?.status === 409) return "Проверьте код, ограничения и срок действия. Изменение не сохранено.";
    return "Не удалось выполнить действие. Обновите данные и повторите попытку.";
  }
  function attach(document, api, fresh) {
    const $ = id => document.getElementById(id), host = $("promotions-list"), status = $("promotions-status");
    function message(text, failed = false) { status.textContent = text; status.className = "status" + (failed ? " error" : ""); }
    async function load() {
      try {
        const rows = await api("/v1/admin/promotions"); host.replaceChildren();
        if (!rows.length) { host.textContent = "Промокодов пока нет."; return; }
        for (const row of rows) {
          const d = row.definition, item = document.createElement("div"); item.className = "promotion-admin-row";
          const label = document.createElement("p");
          label.textContent = d.code + " · " + (d.discountBasisPoints / 100) + "% · " + d.currency + " · " + (d.active ? "включён" : "выключен") +
            " · использовано " + row.used + "/" + d.maxUses + " · в счетах " + row.reserved;
          const detail = document.createElement("p"); detail.className = "note";
          detail.textContent = "Срок UTC: " + d.startsAt + " — " + d.endsAt + " · SKU: " + (d.sku || "подходящие подписки") +
            " · бюджет " + d.budgetMinor + " минимальных единиц · потрачено " + row.spentMinor + " · в счетах " + row.reservedMinor;
          item.append(label, detail);
          const edit = document.createElement("button"); edit.type = "button"; edit.className = "button quiet"; edit.textContent = "Открыть настройки";
          edit.addEventListener("click", () => {
            $("promo-code").value = d.code; $("promo-discount").value = String(d.discountBasisPoints / 100); $("promo-currency").value = d.currency;
            $("promo-sku").value = d.sku || ""; $("promo-starts").value = d.startsAt.slice(0, 16); $("promo-ends").value = d.endsAt.slice(0, 16);
            $("promo-max-uses").value = d.maxUses; $("promo-per-account").value = d.perAccountLimit; $("promo-budget").value = d.budgetMinor; $("promo-active").checked = d.active;
            $("promo-code").focus(); message("Настройки загружены. Изменения применятся только после сохранения.");
          }); item.append(edit);
          if (d.active) {
            const disable = document.createElement("button"); disable.type = "button"; disable.className = "button quiet"; disable.textContent = "Отключить новые применения";
            disable.addEventListener("click", async () => {
              if (!fresh()) { message(userError({ status: 401 }), true); return; }
              if (!global.confirm("Отключить новые применения этого промокода? Уже созданные счета сохранят свои условия.")) return;
              disable.disabled = true;
              try { await api("/v1/admin/promotions", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...d, active: false }) }); await load(); message("Новые применения отключены."); }
              catch (error) { message(userError(error), true); }
              finally { disable.disabled = false; }
            }); item.append(disable);
          }
          host.append(item);
        }
      } catch (error) { host.replaceChildren(); message(userError(error), true); }
    }
    $("promotions-refresh").addEventListener("click", load);
    $("promotion-form").addEventListener("submit", async event => {
      event.preventDefault(); if (!fresh()) { message(userError({ status: 401 }), true); return; }
      let request;
      try { request = definition({ code: $("promo-code").value, discount: $("promo-discount").value, currency: $("promo-currency").value, sku: $("promo-sku").value, starts: $("promo-starts").value, ends: $("promo-ends").value, maxUses: $("promo-max-uses").value, perAccountLimit: $("promo-per-account").value, budget: $("promo-budget").value, active: $("promo-active").checked }); }
      catch (error) { message(error.message, true); return; }
      const save = $("promotion-save"); save.disabled = true; message("Сохраняю ограничения промокода…");
      try { await api("/v1/admin/promotions", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(request) }); await load(); message(request.active ? "Промокод сохранён и включён." : "Промокод сохранён; новые применения выключены."); }
      catch (error) { message(userError(error), true); }
      finally { save.disabled = false; }
    });
    return { load };
  }
  global.VideoGrabberAdminPromotions = { definition, userError, attach };
})(globalThis);
