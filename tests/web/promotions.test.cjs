const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const file = path.join(__dirname, '../../src/VideoGrabber.Platform.Api/wwwroot/assets/promotions.js');
function setup() {
  const context = { URL, URLSearchParams, Date, Error, crypto: require('node:crypto').webcrypto };
  context.globalThis = context;
  vm.runInNewContext(fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '', context);
  assert.ok(context.VideoGrabberPromotions, 'shared promotion controller must exist');
  return context.VideoGrabberPromotions;
}
const summary = { code: 'opaque', webLink: 'https://example.test/web/?ref=opaque', telegramLink: 'https://t.me/ExampleBot?start=ref_opaque', invited: 2, paid: 1, balances: [{ currency: 'RUB', availableMinor: 500, pendingMinor: 1000, reservedMinor: 0, debtMinor: 0 }], history: [] };
const quote = { quoteId: 'q-1', original: { minorUnits: 150000, currency: 'RUB' }, discount: { minorUnits: 15000, currency: 'RUB' }, bonus: { minorUnits: 500, currency: 'RUB' }, payable: { minorUnits: 134500, currency: 'RUB' }, expiresAt: '2099-01-01T00:00:00Z' };
test('capture removes only referral from browser URL and retains it in memory until authenticated claim succeeds', async () => {
  const p = setup(); const replaced = [];
  const code = p.capture({ href: 'https://example.test/web/?ref=opaque&desktop=abc#app' }, { replaceState: (_, __, url) => replaced.push(url) });
  assert.equal(code, 'opaque'); assert.equal(replaced[0], '/web/?desktop=abc#app');
  const requests = []; const c = p.createController(async (url, options) => { requests.push([url, options]); return summary; }, code);
  await c.load(); await c.load();
  assert.equal(requests.filter(x => x[0].endsWith('/claim')).length, 1);
  assert.deepEqual(JSON.parse(requests[0][1].body), { code: 'opaque' });
  assert.equal(c.summary.balances[0].pendingMinor, 1000);
});
test('503 hides marketing without fake balance and permits ordinary checkout', async () => {
  const c = setup().createController(async () => { throw Object.assign(new Error('private code'), { status: 503 }); });
  await c.load(); assert.equal(c.disabled, true); assert.equal(c.summary, null);
  assert.equal(await c.quote({ sku: 'start', provider: 'yookassa', recurring: true }), null);
  assert.equal(c.paymentFields().quoteId, undefined);
});
test('quote uses server money, binds checkout to quote and keeps retry idempotency', async () => {
  let request;
  const c = setup().createController(async (url, options) => { request = [url, options]; return url.endsWith('/quote') ? quote : summary; });
  await c.load(); const result = await c.quote({ sku: 'start', provider: 'yookassa', promoCode: 'SAVE10', useBonuses: true, recurring: false });
  assert.equal(result.payable.minorUnits, 134500);
  assert.deepEqual(JSON.parse(request[1].body), { sku: 'start', provider: 'yookassa', promoCode: 'SAVE10', useBonuses: true, recurring: false });
  const first = c.paymentFields(); assert.equal(first.quoteId, 'q-1'); assert.equal(c.paymentFields().idempotencyKey, first.idempotencyKey);
  c.invalidate(); assert.throws(() => c.paymentFields(), /расчёт/u);
});
test('recurring Stars cannot request one-time promotions and can pay undiscounted', async () => {
  let calls = 0; const c = setup().createController(async () => { calls++; return summary; });
  await c.load(); await assert.rejects(c.quote({ sku: 'start', provider: 'stars', recurring: true, promoCode: 'SAVE10', useBonuses: true }), /автопродлен/u);
  assert.equal(calls, 1);
  assert.equal(await c.quote({ sku: 'start', provider: 'stars', recurring: true, promoCode: '', useBonuses: false }), null);
  assert.equal(c.paymentFields().quoteId, undefined);
});
test('server errors show neutral text without leaking referral or provider values', async () => {
  const p = setup(); assert.doesNotMatch(p.userError({ message: 'token=secret', status: 400 }), /secret|token/u);
  assert.match(p.userError({ status: 409 }), /уже|недоступ/u);
});
test('expired quotes and late responses after a form change cannot be purchased', async () => {
  let resolve;
  const c = setup().createController(async url => url.endsWith('/quote') ? new Promise(r => { resolve = r; }) : summary);
  await c.load(); const pending = c.quote({ sku: 'start', provider: 'stars', recurring: false });
  c.invalidate(); resolve(quote); await pending; assert.throws(() => c.paymentFields(), /расчёт/u);
  const expired = setup().createController(async url => url.endsWith('/quote') ? { ...quote, expiresAt: '2000-01-01T00:00:00Z' } : summary);
  await expired.load(); await expired.quote({ sku: 'start', provider: 'stars' }); assert.throws(() => expired.paymentFields(), /расчёт/u);
});
test('links refuse script URLs and untrusted Telegram hosts; currencies stay separate', () => {
  const p = setup(); assert.equal(p.safeLink('javascript:alert(1)'), null); assert.equal(p.safeLink('https://evil.test/', true), null);
  assert.equal(p.safeLink(summary.telegramLink, true), summary.telegramLink);
  assert.match(p.money({ minorUnits: 1250, currency: 'RUB' }), /12,50/u);
  assert.equal(p.money({ minorUnits: 12, currency: 'XTR' }), '12 Stars');
});
test('opaque referral survives OAuth reload only within tab TTL; claim and logout clear intent', async () => {
  const storage = new Map(); const tab = { getItem: k => storage.get(k) || null, setItem: (k, v) => storage.set(k, v), removeItem: k => storage.delete(k) };
  const p = setup();
  p.capture({ href: 'https://example.test/?ref=opaque' }, { replaceState() {} }, tab);
  assert.equal(p.capture({ href: 'https://example.test/' }, { replaceState() {} }, tab), 'opaque');
  const c = p.createController(async () => summary, 'opaque', tab); await c.load(); assert.equal(storage.size, 0);
  p.capture({ href: 'https://example.test/?ref=opaque' }, { replaceState() {} }, tab);
  c.clearIntent(); assert.equal(storage.size, 0);
  tab.setItem('vg_referral_intent', JSON.stringify({ code: 'opaque', at: Date.now() - 31 * 86400000 }));
  assert.equal(p.capture({ href: 'https://example.test/' }, { replaceState() {} }, tab), ''); assert.equal(storage.size, 0);
});
class Element {
  constructor(document, tag) { this.ownerDocument = document; this.tagName = tag; this.children = []; this.attributes = {}; this.handlers = {}; this.value = ''; this.checked = false; this.textContent = ''; }
  append(...children) { this.children.push(...children); }
  prepend(child) { this.children.unshift(child); }
  replaceChildren(...children) { this.children = children; this.textContent = ''; }
  setAttribute(name, value) { this.attributes[name] = value; }
  addEventListener(name, callback) { this.handlers[name] = callback; }
  text() { return this.textContent + this.children.map(c => c.text()).join(' '); }
  all() { return [this, ...this.children.flatMap(c => c.all())]; }
}
function host() { const document = { createElement(tag) { return new Element(document, tag); } }; return new Element(document, 'section'); }
test('summary renders real pending/available/history, safe text and accessible copying; disabled card disappears', async () => {
  const p = setup(); const c = p.createController(async () => ({ ...summary, history: [{ kind: '<script>', amount: { minorUnits: 1000, currency: 'RUB' }, createdAt: '2026-10-08', availableAt: '2026-10-22', expiresAt: '2027-10-22' }] }));
  await c.load(); const card = host(); p.renderSummary(card, c);
  assert.match(card.text(), /Доступно: 5,00 ₽ · Ожидает: 10,00 ₽/u);
  assert.match(card.text(), /История бонусов.*доступно с/u);
  assert.doesNotMatch(card.text(), /<script>/u);
  const copy = card.all().find(x => x.tagName === 'button'); await copy.handlers.click();
  assert.match(card.text(), /Копирование недоступно/u);
  assert.ok(card.all().some(x => x.attributes['aria-live'] === 'polite'));
  const disabled = p.createController(async () => { throw { status: 503 }; }); await disabled.load(); p.renderSummary(card, disabled);
  assert.equal(card.hidden, true); assert.equal(card.children.length, 0);
});
test('checkout DOM shows quoted money; promo and recurring changes disable purchase until recalculation', async () => {
  const p = setup(); const c = p.createController(async url => url.endsWith('/quote') ? quote : summary); await c.load();
  const card = host(); const ready = []; const request = { sku: 'start', provider: 'yookassa', recurring: false };
  const ui = p.mountCheckout(card, c, () => request, value => ready.push(value));
  assert.equal(ready.at(-1), false); await ui.calculate(); assert.equal(ready.at(-1), true);
  assert.match(card.text(), /К оплате: 1[\s\u00a0]?345,00 ₽/u);
  const input = card.all().find(x => x.type === 'text'); input.value = 'OTHER'; input.handlers.input(); assert.equal(ready.at(-1), false);
  request.recurring = true; ui.synchronize(); assert.equal(input.disabled, true); assert.equal(input.value, '');
  await ui.calculate(); assert.equal(ready.at(-1), true); assert.equal(c.paymentFields().quoteId, undefined);
});
test('invalid coupon is announced accessibly and never enables checkout', async () => {
  const p = setup(); const c = p.createController(async url => { if (url.endsWith('/quote')) throw { status: 400, message: 'token=secret' }; return summary; }); await c.load();
  const card = host(); const ready = []; const ui = p.mountCheckout(card, c, () => ({ sku: 'start', provider: 'stars' }), x => ready.push(x));
  await ui.calculate(); const error = card.all().find(x => x.attributes.role === 'alert');
  assert.match(error.text(), /Проверьте промокод/u); assert.doesNotMatch(error.text(), /secret/u); assert.equal(ready.at(-1), false);
});
test('a summary arriving after logout cannot restore former account balances', async () => {
  let resolve;
  const c = setup().createController(() => new Promise(r => { resolve = r; }));
  const pending = c.load(); c.clearIntent(); resolve(summary); await pending;
  assert.equal(c.summary, null);
});
test('ordinary checkout retries retain their key only for the same product and billing choice', async () => {
  const c = setup().createController(async () => { throw { status:503 }; }); await c.load();
  const request = {sku:'start',provider:'yookassa',recurring:true}; const first = c.paymentFields(request);
  assert.equal(c.paymentFields(request).idempotencyKey,first.idempotencyKey);
  assert.notEqual(c.paymentFields({...request,sku:'full_course'}).idempotencyKey,first.idempotencyKey);
});
test('website checkout with missing payment URL rejects visibly instead of silently leaving review open', async () => {
  const p = setup(); const c = p.createController(async () => { throw {status:503}; }); await c.load();
  const messages = [];
  const context = { promotionController:c, api:async()=>({}), location:{assign(){throw new Error('unexpected navigation');}}, setStatus:(...args)=>messages.push(args) };
  const web = fs.readFileSync(path.join(__dirname,'../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js'),'utf8');
  vm.runInNewContext(web.slice(web.indexOf('async function sendCheckout('),web.indexOf('async function logout(')),context);
  await assert.rejects(context.sendCheckout({sku:'start'},true));
  assert.match(messages.at(-1)[1],/оплат/u);
});
test('old quote503 after account reset cannot disable current promotions or allow ordinary checkout', async () => {
  let reject;
  const c = setup().createController(async url => url.endsWith('/quote') ? new Promise((_, r)=>{reject=r;}) : summary);
  await c.load(); const old = c.quote({sku:'start',provider:'stars'});
  c.reset(); await c.load(); reject({status:503}); await old;
  assert.equal(c.disabled,false); assert.equal(c.summary,summary); assert.throws(()=>c.paymentFields(),/расчёт/u);
});
