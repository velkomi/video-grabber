const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('an idle import queued before reduced motion does not start until normal motion is restored', () => {
  const source = fs.readFileSync(path.join(__dirname,'../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js'),'utf8');
  const loader = source.slice(source.indexOf('function scheduleThreeHero()'), source.indexOf('function setupHeroScene()'))
    .replace(/import\("\/web\/hero-three\.bundle\.js\?v=[^"]+"\)/,'loadBundle()');
  let reduced = false;
  const callbacks = [];
  const changes = [];
  const imports = [];
  const motion = {get matches() {return reduced;}, addEventListener(_,fn) {changes.push(fn);}, removeEventListener() {}};
  const canvas = {hidden:false};
  const art = {complete:true};
  const context = {$: selector => selector === '#hero-three' ? canvas : {classList:{remove() {}}},
    document:{querySelector: () => art}, matchMedia: () => motion, visualTestMode:false,
    window:{requestIdleCallback() {},addEventListener() {}},
    requestIdleCallback: fn => callbacks.push(fn),
    loadBundle: () => {imports.push(reduced); return Promise.resolve();}};
  vm.runInNewContext(loader + '\nscheduleThreeHero();',context);
  assert.equal(callbacks.length,1);
  reduced = true;
  callbacks.shift()();
  assert.equal(imports.length,0);
  reduced = false;
  changes.forEach(fn => fn());
  callbacks.shift()();
  assert.deepEqual(imports,[false]);
});
