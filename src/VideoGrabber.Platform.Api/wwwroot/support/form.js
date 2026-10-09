import {SupportDraft, supportError} from './state.mjs';
let dialog, form, widget, lastFocus, config, accessToken = '';
const draft = new SupportDraft();
const topics = {sign_in:'Вход в аккаунт',download:'Скачивание',subscription:'Тариф и оплата',suggestion:'Предложение',other:'Другой вопрос'};

function headers(json = false) {
  const result = new Headers({Accept:'application/json'});
  if (json) result.set('Content-Type','application/json');
  if (accessToken) result.set('Authorization','Bearer '+accessToken);
  if (json && config?.csrfToken) result.set('X-CSRF-Token', config.csrfToken);
  result.set('X-VideoGrabber-Client', location.pathname.startsWith('/miniapp/') ? 'miniapp' : 'form');
  return result;
}

function build() {
  dialog = document.createElement('dialog');
  dialog.className = 'vg-support-dialog';
  dialog.setAttribute('aria-labelledby','vg-support-title');
  dialog.innerHTML = `<div class="vg-support-heading"><div><p class="vg-support-eyebrow">Мы рядом</p><h2 id="vg-support-title">Написать в поддержку</h2></div><button type="button" class="vg-support-close" aria-label="Закрыть форму">×</button></div>
    <p class="vg-support-intro">Опишите вопрос — мы ответим по оставленному контакту.</p>
    <form class="vg-support-form">
      <label for="vg-support-topic">Тема</label><select id="vg-support-topic" name="topic"></select>
      <label for="vg-support-contact">Контакт для ответа</label><input id="vg-support-contact" name="contact" type="text" inputmode="email" autocomplete="email" maxlength="254" placeholder="Почта или @имя в Telegram" required>
      <label for="vg-support-message">Ваш вопрос</label><textarea id="vg-support-message" name="message" rows="5" minlength="20" maxlength="4000" placeholder="Что произошло и как это повторить?" required></textarea>
      <div class="vg-support-note">Пароли, коды входа и платёжные данные присылать не нужно.</div>
      <label class="vg-support-trap" aria-hidden="true">Website<input name="website" type="text" tabindex="-1" autocomplete="off" data-lpignore="true" data-1p-ignore="true"></label>
      <div class="vg-support-verification"></div>
      <p class="vg-support-status" role="status" aria-live="polite"></p>
      <p class="vg-support-privacy">Используем контакт для ответа на ваш вопрос. <a href="/info/?document=privacy" target="_blank" rel="noopener noreferrer">Подробнее о данных</a></p>
      <div class="vg-support-actions"><button type="submit" class="vg-support-send">Отправить вопрос</button><button type="button" class="vg-support-cancel">Отмена</button></div>
    </form>`;
  document.body.append(dialog);
  form = dialog.querySelector('form');
  for (const [value,label] of Object.entries(topics)) form.elements.topic.add(new Option(label,value));
  dialog.querySelector('.vg-support-close').addEventListener('click',()=>dialog.close());
  dialog.querySelector('.vg-support-cancel').addEventListener('click',()=>dialog.close());
  dialog.addEventListener('close',()=>lastFocus?.focus());
  form.addEventListener('submit', submit);
}

function status(text, kind = '') {
  const node = dialog.querySelector('.vg-support-status');
  node.textContent = text;
  node.dataset.kind = kind;
}

export async function openSupport() {
  lastFocus = document.activeElement;
  if (!dialog) build();
  if (!dialog.open) dialog.showModal();
  const send = dialog.querySelector('.vg-support-send');
  send.disabled = true;
  status('Открываю форму…');
  try {
    try { accessToken = localStorage.getItem('vg_web_access') || ''; } catch { accessToken = ''; }
    const response = await fetch('/v1/support/config',{credentials:'same-origin',cache:'no-store',headers:headers()});
    if (!response.ok) throw new Error();
    config = await response.json();
    if (!config.enabled) { status(supportError(503),'error'); return; }
    if (!widget) {
      await import('./vendor/main/altcha.min.js');
      await import('./vendor/i18n/ru.js');
      widget = document.createElement('altcha-widget');
      widget.setAttribute('challenge','/v1/support/challenge');
      widget.setAttribute('name','altcha');
      widget.setAttribute('language','ru');
      dialog.querySelector('.vg-support-verification').append(widget);
    }
    status('');
    send.disabled = draft.inFlight;
  } catch { status(supportError(503),'error'); }
}

async function submit(event) {
  event.preventDefault();
  const fields = Object.fromEntries(new FormData(form));
  const proof = fields.altcha || widget?.value;
  if (!proof) { status('Сначала выполните проверку перед отправкой.','error'); widget?.focus(); return; }
  const body = draft.begin(fields);
  if (!body) return;
  const send = dialog.querySelector('.vg-support-send');
  send.disabled = true;
  for (const name of ['topic','contact','message']) form.elements[name].disabled = true;
  status('Отправляю вопрос…');
  try {
    const response = await fetch('/v1/support/requests',{method:'POST',credentials:'same-origin',headers:headers(true),
      body:JSON.stringify({...body,altcha:String(proof),website:String(fields.website || '')})});
    const result = await response.json().catch(()=>({}));
    if (!response.ok) {
      if (response.status === 403 && result.code === 'csrf_required') {
        const refreshed = await fetch('/v1/support/config',{credentials:'same-origin',cache:'no-store',headers:headers()});
        if (refreshed.ok) config = await refreshed.json();
        status('Защита формы обновлена. Нажмите «Отправить вопрос» ещё раз.','error');
        return;
      }
      status(supportError(response.status,result.code),'error');
      if (response.status === 403) widget?.reset?.();
      return;
    }
    const ticket = String(result.ticketId || '').replaceAll('-','').slice(0,8).toUpperCase();
    status('Обращение сохранено. Номер VG-'+ticket+'.','success');
    form.elements.message.value = '';
    draft.reset();
    widget?.reset?.();
  } catch { status(supportError(0),'error'); }
  finally { draft.release(); send.disabled = false; for (const name of ['topic','contact','message']) form.elements[name].disabled = false; }
}

document.addEventListener('click',event=>{
  const target = event.target instanceof Element ? event.target.closest('[data-open-support]') : null;
  if (!target) return;
  event.preventDefault();
  openSupport();
});
window.openVideoGrabberSupport = openSupport;
if (location.pathname.startsWith('/miniapp/')) {
  const footer = document.querySelector('.information-footer');
  if (footer) { const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Поддержка'; button.dataset.openSupport = ''; button.className = 'vg-support-inline'; footer.prepend(button); }
}
if (new URLSearchParams(location.search).get('support') === '1') openSupport();
