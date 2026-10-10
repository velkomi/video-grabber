const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function harness(initialWidth, viewportHeight = 900) {
  const source = fs.readFileSync(path.join(__dirname, '../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js'), 'utf8');
  const code = source.slice(source.indexOf('function setupStoryStage()'), source.indexOf('function scheduleThreeHero()'));
  let width = initialWidth;
  let scrollY = 0;
  const windowEvents = new Map();
  const mediaListeners = [];
  const triggers = new Set();
  const stage = { style: {}, dataset: {}, classList: {
    values: new Set(), add(x) { this.values.add(x); }, remove(x) { this.values.delete(x); },
    contains(x) { return this.values.has(x); }
  }};
  const visual = { dispatchEvent() {} };
  const element = (top = 100) => ({ getBoundingClientRect: () => ({left: 650, top: top-scrollY, bottom: top-scrollY+450, width: width > 1050 ? 600 : 350, height: 450}) });
  const nodes = new Map([['#story-stage', stage], ['#hero-visual', visual], ['footer', element(2400)]]);
  for (const [i, name] of ['top', 'how', 'app', 'pricing', 'download'].entries()) nodes.set('#' + name, element(i * 450));
  for (const name of ['hero', 'workflow', 'sync', 'pricing', 'windows']) nodes.set('.story-slot-' + name, element());
  const media = new Map();
  const matchMedia = query => {
    if (!media.has(query)) media.set(query, {get matches() {return query.includes('max-width') ? width <= 1050 : false;},
      addEventListener(_, fn) {mediaListeners.push({query, fn});}, removeEventListener(_, fn) {
        const index = mediaListeners.findIndex(x => x.fn === fn); if (index >= 0) mediaListeners.splice(index, 1);
      }});
    return media.get(query);
  };
  const window = {scrollX: 0, get scrollY() {return scrollY;}, innerHeight: viewportHeight, matchMedia,
    addEventListener(name, fn) {if (!windowEvents.has(name)) windowEvents.set(name, new Set()); windowEvents.get(name).add(fn);},
    removeEventListener(name, fn) {windowEvents.get(name)?.delete(fn);},
    gsap: {registerPlugin() {}, to(node, options) {Object.assign(node.style, options);}, killTweensOf() {}},
    ScrollTrigger: {create() {const trigger = {kill() {triggers.delete(trigger);}}; triggers.add(trigger); return trigger;},
      addEventListener() {}, removeEventListener() {}, refresh() {}}
  };
  const context = {window, document: {querySelector: selector => nodes.get(selector),documentElement:{scrollHeight:2400}},
    $: selector => nodes.get(selector), matchMedia, innerHeight: viewportHeight, visualTestMode: false,
    CustomEvent: class {constructor(type, args) {this.type = type; this.detail = args?.detail;}}, requestAnimationFrame: fn => fn(),
    setTimeout: fn => fn(), clearTimeout() {}};
  vm.runInNewContext(code + '\nsetupStoryStage();', context);
  return {stage, triggers, bonus(visible) {
    if (visible) { nodes.set('#referral-card', element(1250)); nodes.set('.story-slot-bonus', element(1350)); }
    else { nodes.delete('#referral-card'); nodes.delete('.story-slot-bonus'); }
    nodes.set('#pricing', element(2100)); nodes.set('#download', element(2550));
    for (const fn of [...(windowEvents.get('videograbber:referral-layout') || [])]) fn();
  }, scroll(value) {scrollY=value; for (const fn of [...(windowEvents.get('scroll') || [])]) fn();}, resize(next) {
    const previous = width; width = next;
    if ((previous <= 1050) !== (next <= 1050)) {
      for (const listener of [...mediaListeners]) if (listener.query.includes('max-width')) listener.fn({matches: next <= 1050});
    }
    for (const fn of [...(windowEvents.get('resize') || [])]) fn();
  }};
}

test('a page opened on mobile enables desktop story after widening, then releases it on mobile', () => {
  const app = harness(390);
  assert.equal(app.stage.classList.contains('is-managed'), false);
  app.resize(1440);
  assert.equal(app.stage.classList.contains('is-managed'), true);
  assert.ok(app.triggers.size > 0);
  app.resize(390);
  assert.equal(app.stage.classList.contains('is-managed'), false);
  assert.equal(app.triggers.size, 0);
});

test('repeated breakpoint transitions do not retain old ScrollTriggers', () => {
  const app = harness(1440);
  const initialCount = app.triggers.size;
  assert.ok(initialCount > 0);
  for (let i = 0; i < 4; i++) {
    app.resize(390);
    assert.equal(app.triggers.size, 0);
    app.resize(1440);
    assert.equal(app.triggers.size, initialCount);
  }
});

test('a tall desktop viewport keeps the opening scene on hero before the reader scrolls', () => {
  const app = harness(1440,1758);
  assert.equal(app.stage.dataset.storyState,'hero');
});

test('the final Windows scene activates when scrolling reaches the page end', () => {
  const app = harness(1440,900);
  app.scroll(1500);
  assert.equal(app.stage.dataset.storyState,'windows');
});

test('bonus artwork added after account load receives the shared scene, then releases it on logout',()=>{
  const app=harness(1440); const initial=app.triggers.size;
  app.bonus(true); app.scroll(1050);
  assert.equal(app.stage.dataset.storyState,'bonus');
  assert.equal(app.triggers.size,initial+1);
  app.bonus(false);
  assert.equal(app.stage.dataset.storyState,'sync');
  assert.equal(app.triggers.size,initial);
});
