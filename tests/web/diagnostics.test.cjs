const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
function boot(dialogs={},sessionStorage,Clock=Date) {
  const calls=[];
  const listeners={};
  const observers={};
  const window={sessionStorage,fetch:async (input,options)=>{calls.push(options);return {status:503,ok:false};},addEventListener:(n,f)=>listeners[n]=f};
  const context={window,location:{href:'http://localhost/web/?token=secret',origin:'http://localhost',search:''},
    document:{addEventListener(n,f){listeners['document:'+n]=f;},querySelector(selector){return dialogs[selector]??null;}},URL,URLSearchParams,Headers,Request,Date:Clock,Math,
    MutationObserver:class {constructor(callback){this.callback=callback;}observe(dialog){observers[dialog.id]=this.callback;}},
    crypto:require('node:crypto').webcrypto,performance:{now:()=>25},setInterval(){},console};
  vm.runInNewContext(fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/diagnostics.js','utf8'),context);
  return {window,calls,listeners,observers};
}
test('API trace excludes query, body and credentials, while preserving rejection and correlation',async()=>{
  const {window,calls}=boot();
  await window.fetch('/v1/jobs/secret-id?token=secret',{method:'POST',body:'private-email',headers:{Authorization:'Bearer secret'}});
  const events=window.VGDiagnostics.snapshot();
  const serialized=JSON.stringify(events);
  assert.doesNotMatch(serialized,/secret|private-email|Authorization/);
  const end=events.find(e=>e.event==='http.request'&&e.outcome==='failed');
  assert.equal(end.route,'/v1/jobs/{id}');
  assert.equal(end.status,503);
  assert.match(calls[0].headers.get('X-Correlation-Id'),/^[a-f0-9]{32}$/);
  assert.equal(end.traceId,calls[0].headers.get('X-Correlation-Id'));
});

test('a bounded graphics incident survives reload without restoring untrusted fields',()=>{
  let saved='[]';
  const storage={getItem:()=>saved,setItem:(_,value)=>{saved=value;}};
  const first=boot({},storage);
  first.listeners['videograbber:webgl']({detail:{event:'context_lost',state:'hero',dpr:1}});
  const hostile=JSON.parse(saved);hostile[0].message='token=secret';saved=JSON.stringify(hostile);
  const second=boot({},storage);
  const recovered=second.window.VGDiagnostics.snapshot().find(e=>e.event==='webgl.context_lost');
  assert.equal(recovered.previousPage,true);
  assert.equal(recovered.state,'hero');
  assert.doesNotMatch(JSON.stringify(second.window.VGDiagnostics.snapshot()),/secret/);
});

test('repeated reloads preserve the original graphics incident time and trace',()=>{
  let saved='[]';
  const storage={getItem:()=>saved,setItem:(_,value)=>{saved=value;}};
  const clock=time=>class extends Date {constructor(value){super(value??time);}};
  const first=boot({},storage,clock('2026-10-07T09:00:00.000Z'));
  first.listeners['videograbber:webgl']({detail:{event:'context_lost',state:'hero'}});
  const original=JSON.parse(saved)[0];
  const second=boot({},storage,clock('2026-10-07T12:00:00.000Z'));
  const replay=second.window.VGDiagnostics.snapshot().find(e=>e.event==='webgl.context_lost');
  assert.equal(replay.timestamp,original.timestamp);
  assert.equal(replay.traceId,original.traceId);
  assert.equal(replay.replayedAt,'2026-10-07T12:00:00.000Z');
  second.listeners['videograbber:webgl']({detail:{event:'context_restored',state:'hero'}});
  const third=boot({},storage,clock('2026-10-07T15:00:00.000Z'));
  assert.equal(third.window.VGDiagnostics.snapshot().find(e=>e.event==='webgl.context_lost').timestamp,original.timestamp);
});
test('ring stays bounded and never exports exception messages',()=>{
  const {window,listeners}=boot();
  for(let i=0;i<900;i++) listeners.error({error:new Error('token=secret')});
  assert.equal(window.VGDiagnostics.snapshot().length,500);
  assert.equal(window.VGDiagnostics.dropped,401);
  assert.doesNotMatch(JSON.stringify(window.VGDiagnostics.snapshot()),/secret/);
});
test('third party fetch is untouched and never gets a correlation header',async()=>{
  const {window,calls}=boot();
  await window.fetch('https://provider.example/auth?token=secret',{headers:{apikey:'secret'}});
  assert.equal(calls[0].headers.apikey,'secret');
  assert.equal(window.VGDiagnostics.snapshot().length,1);
});
test('clicks and option changes record stable control names without input values',()=>{
  const {window,listeners}=boot();
  const node={id:'header-login',closest:()=>null,getAttribute:()=>null};
  listeners['document:click']({target:{closest:()=>node,value:'private-email'}});
  listeners['document:change']({target:{id:'device-select',value:'secret-device-id'}});
  listeners['document:change']({target:{id:'login-email',value:'private-email'}});
  const data=window.VGDiagnostics.snapshot();
  assert.equal(data.length,3);
  assert.equal(data[1].action,'header-login');
  assert.equal(data[2].action,'device-select');
  assert.doesNotMatch(JSON.stringify(data),/private-email|secret-device-id/);
});
test('native dialog open and Escape close are recorded without dialog text',()=>{
  const {window,listeners,observers}=boot({'#info-dialog':{id:'info-dialog',textContent:'private-email'}});
  listeners['document:DOMContentLoaded']();
  observers['info-dialog']([{oldValue:null},{oldValue:''}]);
  const events=window.VGDiagnostics.snapshot();
  assert.equal(events[1].event,'dialog.open'); assert.equal(events[2].event,'dialog.close');
  assert.doesNotMatch(JSON.stringify(events),/private-email/);
});

test('WebGL loss and recovery are recorded with allowlisted fields only',()=>{
  const {window,listeners}=boot();
  assert.equal(typeof listeners['videograbber:webgl'],'function');
  listeners['videograbber:webgl']({detail:{event:'context_lost',state:'sync',quality:'balanced',dpr:1,
    message:'token=secret',url:'https://private.example/video?secret'}});
  listeners['videograbber:webgl']({detail:{event:'context_restored',state:'sync',quality:'balanced',dpr:1}});
  const data=window.VGDiagnostics.snapshot();
  assert.equal(data[1].event,'webgl.context_lost');
  assert.equal(data[1].outcome,'failed');
  assert.equal(data[2].event,'webgl.context_restored');
  assert.equal(data[2].outcome,'succeeded');
  assert.doesNotMatch(JSON.stringify(data),/secret|private\.example/);
});
