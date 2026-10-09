import test from 'node:test';
import assert from 'node:assert/strict';

let support;
try { support = await import('../../src/VideoGrabber.Platform.Api/wwwroot/support/state.mjs'); }
catch (error) { if (error.code !== 'ERR_MODULE_NOT_FOUND') throw error; }
const fields = {topic:'sign_in',contact:'client@example.test',message:'Не получается войти после подтверждения.'};

test('retry keeps the same request identity and concurrent submission is suppressed', () => {
  assert.ok(support?.SupportDraft, 'Support submission state is not implemented');
  const draft = new support.SupportDraft();
  const first = draft.begin(fields);
  assert.equal(draft.begin(fields), null);
  draft.release();
  assert.equal(draft.begin(fields).requestId, first.requestId);
});

test('editing a submitted message creates a new request identity', () => {
  assert.ok(support?.SupportDraft, 'Support submission state is not implemented');
  const draft = new support.SupportDraft();
  const first = draft.begin(fields);
  draft.release();
  const changed = draft.begin({...fields,message:fields.message+' Дополнение.'});
  assert.notEqual(changed.requestId, first.requestId);
});

test('server details and secrets are never echoed by the customer error message', () => {
  assert.ok(support?.supportError, 'Support error mapper is not implemented');
  const error = support.supportError(500, 'private-database-password-value');
  assert.equal(error.includes('private-database'), false);
  assert.match(support.supportError(429, 'support_rate_limited'), /позже|подождите/i);
});
