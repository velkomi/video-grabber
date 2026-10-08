const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js','utf8');

test('lost WebGL context never renders or marks the failed canvas ready on a resize',()=>{
  const body=source.slice(source.indexOf('  const render = () => {'),source.indexOf('  const tick = (time) => {'));
  let calls=0;
  const context={contextLost:true,graphicsFailed:false,disposed:false,reducedMotion:false,visualTestMode:false,scene:{},camera:{},
    renderer:{getContext:()=>({isContextLost:()=>true}),render(){calls++;throw Error('lost_context_render');}}};
  vm.runInNewContext(body+'\nthis.paint=render;',context);
  assert.doesNotThrow(()=>context.paint());
  assert.equal(calls,0);
});

test('normal-motion notification cannot expose a lost canvas or restart its animation',()=>{
  const body=source.slice(source.indexOf('  const applyMotionPreference ='),source.indexOf('  const updateMotionPreference ='));
  let ready=false,starts=0;
  const canvas={hidden:true,dataset:{}};
  const context={canvas,contextLost:true,graphicsFailed:false,disposed:false,reducedMotion:false,visualTestMode:false,
    renderer:{getContext:()=>({isContextLost:()=>true})},visual:{classList:{remove(){ready=false;}}},
    stopLoop(){},render(){ready=true;return true;},startLoop(){starts++;}};
  vm.runInNewContext(body+'\napplyMotionPreference(false);',context);
  assert.equal(canvas.hidden,true);
  assert.equal(ready,false);
  assert.equal(starts,0);
});
