const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
test('scene activation requires a primary press and release from the same pointer inside the scene',()=>{
  const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js','utf8');
  const snippet=source.slice(source.indexOf('  visual.addEventListener("pointerup"'),source.indexOf('  visual.addEventListener("pointerleave"'));
  const listeners={}; let activations=0;
  const context={pointerDown:null,raycastAt:()=> 'workflow-url',visual:{addEventListener:(name,fn)=>listeners[name]=fn,dispatchEvent(){activations++;}},
    CustomEvent:class {},document:{querySelector:()=>null},CSS:{escape:value=>value}};
  vm.runInNewContext(snippet,context);
  const event={pointerId:1,button:0,isPrimary:true,clientX:50,clientY:50,target:{closest:()=>null}};
  listeners.pointerup(event); assert.equal(activations,0);
  context.pointerDown={id:1,moved:0}; listeners.pointerup({...event,button:2}); assert.equal(activations,0);
  context.pointerDown={id:2,moved:0}; listeners.pointerup(event); assert.equal(activations,0);
  context.pointerDown={id:1,moved:15}; listeners.pointerup(event); assert.equal(activations,0);
  context.pointerDown={id:1,moved:0}; listeners.pointerup(event); assert.equal(activations,1);
});
