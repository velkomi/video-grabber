const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const file = __dirname + '/../../src/VideoGrabber.Platform.Api/wwwroot/admin/promotions.js';
function setup() { const context = { Date, Error }; context.globalThis = context; vm.runInNewContext(fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '', context); assert.ok(context.VideoGrabberAdminPromotions, 'owner promotion controller must exist'); return context.VideoGrabberAdminPromotions; }
const values = {code: 'OWNER_CODE', discount: '15', currency: 'RUB', sku: '', starts: '2026-10-08T00:00', ends: '2026-11-08T00:00', maxUses: '100', perAccountLimit: '1', budget: '300000', active: false};
test('owner draft accepts only suggested10/15/20 percentages, explicit budget units and UTC interval', () => {
  const definition = setup().definition(values);
  assert.equal(definition.discountBasisPoints, 1500); assert.equal(definition.budgetMinor, 300000); assert.equal(definition.active, false);
  assert.equal(definition.startsAt, '2026-10-08T00:00:00.000Z'); assert.equal(definition.sku, null);
  for (const discount of ['0','25','100']) assert.throws(() => setup().definition({...values, discount}), /10.*15.*20/u);
});
test('invalid code, negative/fractional budget and backwards interval never reach admin write', () => {
  const p = setup();
  for (const patch of [{code: ''}, {code: '<script>'}, {budget: '-1'}, {budget: '1.5'}, {maxUses: '0'}, {ends: '2026-01-01T00:00'}, {currency: 'USD'}]) assert.throws(() => p.definition({...values, ...patch}));
});
test('owner request errors use neutral MFA/disabled guidance and exclude raw campaign values', () => {
  const p = setup(); assert.match(p.userError({status:401}), /2FA/u); assert.match(p.userError({status:503}), /недоступ/u);
  assert.doesNotMatch(p.userError({message:'token=secret OWNER_CODE'}), /secret|OWNER_CODE/u);
});
