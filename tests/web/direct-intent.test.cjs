const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const vm=require('node:vm');
test('uncertain direct requests retain their account/source intent across retry and page reload',async()=>{
 const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js','utf8');
 const start=source.indexOf('async function directRequestIntent(');assert.ok(start>=0,'directRequestIntent must exist');
 const end=source.indexOf('\nasync function submitJob(',start);
 const stored=new Map();let serial=0;
 const env={state:{profile:{accountId:'account-a'}},keys:{directIntent:'direct-intent'},sha256Hex:async s=>s.includes('other')?'b'.repeat(64):'a'.repeat(64),crypto:{randomUUID:()=>`00000000-0000-4000-8000-${String(++serial).padStart(12,'0')}`},sessionStorage:{getItem:k=>stored.get(k)||null,setItem:(k,v)=>stored.set(k,v),removeItem:k=>stored.delete(k)}};
 vm.createContext(env);vm.runInContext(source.slice(start,end),env);
 const first=await env.directRequestIntent('https://source/video.mp4');
 assert.equal(await env.directRequestIntent('https://source/video.mp4'),first);
 const reloaded={...env};vm.createContext(reloaded);vm.runInContext(source.slice(start,end),reloaded);
 assert.equal(await reloaded.directRequestIntent('https://source/video.mp4'),first);
 assert.notEqual(await env.directRequestIntent('https://source/other.mp4'),first);
 assert.ok(![...stored.values()].join('').includes('https://'),'raw source URLs must not be stored');
});
