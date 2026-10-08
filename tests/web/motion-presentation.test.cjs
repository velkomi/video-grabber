const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
test('the stage applies reduced motion before a separate renderer media-query notification arrives',()=>{
  const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js','utf8');
  const apply=source.slice(source.indexOf('  const applyMotionPreference ='),source.indexOf('  const updateMotionPreference ='));
  const listener=source.slice(source.indexOf('  visual.addEventListener("videograbber:story-visibility"'),source.indexOf('  visual.addEventListener("videograbber:story-state"'));
  let callback,ready=true,stops=0,starts=0;
  const canvas={hidden:false,dataset:{}};
  const context={canvas,visual:{classList:{remove(){ready=false;}},addEventListener(_,fn){callback=fn;}},
    contextLost:false,graphicsFailed:false,disposed:false,renderer:{getContext:()=>({isContextLost:()=>false})},
    stopLoop(){stops++;},render(){ready=true;return true;},startLoop(){starts++;},document:{hidden:false}};
  vm.runInNewContext('let reducedMotion=false,storyStageVisible=true; const visualTestMode=false,heroVisible=true;\n'+apply+listener,context);
  callback({detail:{visible:false,reducedMotion:true}});
  assert.equal(canvas.hidden,true); assert.equal(ready,false); assert.ok(stops>0);
  callback({detail:{visible:true,reducedMotion:false}});
  assert.equal(canvas.hidden,false); assert.equal(ready,true); assert.ok(starts>0);
});
