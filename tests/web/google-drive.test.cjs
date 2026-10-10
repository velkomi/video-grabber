const { test } = require('node:test');
const assert = require('node:assert/strict');
const drive = require('../../src/VideoGrabber.Platform.Api/wwwroot/web/google-drive.js');
const consent = { access_token: 'test-only-token', expires_in: 3600, scope: drive.scope };

test('quota counts all Google usage precisely and treats missing limit as unknown', () => {
  const q = drive.quotaFromAbout({ storageQuota: { limit: '9007199254741000', usage: '9007199254740993', usageInDrive: '1' } });
  assert.equal(q.free, 7n);
  assert.equal(drive.quotaFromAbout({ storageQuota: { usage: '4' } }).free, null);
  assert.throws(() => drive.quotaFromAbout({}), /cloud_quota_unknown/);
});

test('tokens belong to the connected account and expiry or clearing blocks access', async () => {
  let now = 0, calls = 0;
  const client = drive.createClient({ now: () => now, fetch: async () => { calls++; throw new Error('unexpected'); } });
  client.connect(consent, 'a');
  assert.ok(client.connected('a'));
  await assert.rejects(client.readQuota('b'), /cloud_account_changed/);
  now = 3600000;
  assert.equal(client.connected('a'), false);
  await assert.rejects(client.readQuota('a'), /cloud_consent_required/);
  client.clear();
  assert.equal(calls, 0);
  assert.throws(() => client.connect({ ...consent, scope: 'openid' }, 'a'), /cloud_consent_required/);
});

test('session destinations never send the token outside Google', () => {
  for (const url of ['https://evil.example/upload?upload_id=x', 'https://www.googleapis.com.evil.example/upload/drive/v3/files?upload_id=x',
    'https://x@www.googleapis.com/upload/drive/v3/files?upload_id=x', 'https://www.googleapis.com/else?upload_id=x'])
    assert.throws(() => drive.uploadUrl(url), /cloud_upload_invalid/);
  assert.throws(() => drive.sourceUrl('http://video.example/x.mp4'), /cloud_source_invalid/);
  assert.throws(() => drive.sourceUrl('https://secret@video.example/x.mp4'), /cloud_source_invalid/);
});

function fixture({ size = 4, actual = size, free = 99999999, location, switchAccount = false } = {}) {
  const calls = [], progress = [];
  let client;
  const fetch = async (url, options) => {
    calls.push({ url, options });
    if (url.includes('/about?')) return Response.json({ storageQuota: { usage: '0', limit: String(free) } });
    if (url.includes('/files?q=')) return Response.json({ files: [{ id: 'owned_folder' }] });
    if (url === 'https://source.example/video.mp4') return new Response(new Uint8Array(actual), {
      headers: { 'Content-Length': String(size), 'Content-Type': 'video/mp4' } });
    if (options.method === 'POST') return new Response(null, { status: 200,
      headers: { Location: location || 'https://www.googleapis.com/upload/drive/v3/files?upload_id=test' } });
    if (switchAccount) client.clear();
    const range = options.headers['Content-Range'];
    const end = Number(range.match(/-(\d+)\//)[1]);
    if (end + 1 < size) return new Response(null, { status: 308, headers: { Range: `bytes=0-${end}` } });
    return Response.json({ id: 'new_file', size: String(size), name: 'Видео.mp4' });
  };
  client = drive.createClient({ fetch });
  client.connect(consent, 'account-a');
  const run = () => client.upload({ accountId: 'account-a', url: 'https://source.example/video.mp4', bytes: size,
    mediaType: 'video/mp4', onProgress: x => progress.push(x) });
  return { client, run, calls, progress };
}

test('uploads in bounded chunks, verifies size and does not share or overwrite files', async () => {
  const f = fixture({ size: 8 * 1024 * 1024 + 9 });
  const result = await f.run();
  assert.equal(result.bytes, BigInt(8 * 1024 * 1024 + 9));
  assert.equal(result.url, 'https://drive.google.com/file/d/new_file/view');
  assert.deepEqual(f.calls.filter(x => x.options.method === 'PUT').map(x => x.options.body.length), [8 * 1024 * 1024, 9]);
  const source = f.calls.find(x => x.url.startsWith('https://source.'));
  assert.equal(source.options.credentials, 'omit');
  assert.equal(source.options.headers, undefined);
  assert.ok(f.calls.every(x => !x.url.includes('permissions') && x.options.method !== 'PATCH' && x.options.method !== 'DELETE'));
  assert.equal(f.progress.length, 2);
});

test('insufficient quota prevents any source fetch or file creation', async () => {
  const f = fixture({ free: 3 });
  await assert.rejects(f.run(), /cloud_quota_exceeded/);
  assert.equal(f.calls.length, 1);
});

test('mismatched source sizes stop before the final upload', async () => {
  for (const actual of [2, 6]) {
    const f = fixture({ actual });
    await assert.rejects(f.run(), /cloud_source_(truncated|size_changed)/);
    assert.equal(f.calls.filter(x => x.options.method === 'PUT').length, 0);
  }
});

test('unexpected upload destination is rejected before any PUT', async () => {
  const f = fixture({ location: 'https://evil.example/?upload_id=x' });
  await assert.rejects(f.run(), /cloud_upload_invalid/);
  assert.equal(f.calls.filter(x => x.options.method === 'PUT').length, 0);
});

test('clearing the account during upload cannot report another account as successful', async () => {
  const f = fixture({ switchAccount: true });
  await assert.rejects(f.run(), error => error.name === 'AbortError' || error.message === 'cloud_account_changed');
  assert.equal(f.progress.length, 0);
  assert.equal(f.client.connected('account-a'), false);
});

test('an account switch while quota is in flight prevents fetching the source', async () => {
  let client, calls = 0;
  client = drive.createClient({ fetch: async () => {
    calls++;
    client.connect(consent, 'account-b');
    return Response.json({ storageQuota: { usage: '0', limit: '999' } });
  } });
  client.connect(consent, 'account-a');
  await assert.rejects(client.upload({ accountId: 'account-a', url: 'https://source.example/video.mp4',
    bytes: 4, mediaType: 'video/mp4' }), /cloud_account_changed/);
  assert.equal(calls, 1);
  assert.equal(client.connected('account-a'), false);
  assert.equal(client.connected('account-b'), true);
});
