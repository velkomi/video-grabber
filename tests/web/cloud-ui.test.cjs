const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(require('node:path').join(__dirname,'../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js'),'utf8');
const render = source.slice(source.indexOf('function renderSelectedDevice()'),source.indexOf('let visibleJobCount'));

test('returning to Google Drive with an already loaded SDK enables its connect button', () => {
  const nodes = new Map();
  const node = name => {
    if (!nodes.has(name)) nodes.set(name, {disabled: true, hidden: false, value: '', textContent: ''});
    return nodes.get(name);
  };
  node('#download-target').value = 'google_drive';
  const context = vm.createContext({$: node, window: {google: {accounts: {oauth2: {}}}},
    googleDriveConfig: {enabled: true}, jobSubmissionLocked: false, directRequestInFlight: false,
    ensureGoogleDriveSdk: () => { throw new Error('The SDK is already loaded'); }});
  vm.runInContext(render + '\nrenderSelectedDevice();',context);
  assert.equal(node('#google-drive-connect').disabled,false);
  assert.equal(node('#google-drive-field').hidden,false);
  assert.equal(node('#device-field').hidden,true);
  assert.equal(node('#submit-job').disabled,false);
});
