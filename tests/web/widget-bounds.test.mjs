import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import * as THREE from '../../src/VideoGrabber.Platform.Api/wwwroot/web/vendor/three.module.js';
const source=fs.readFileSync(new URL('../../src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js',import.meta.url),'utf8');
const noop=()=>{};
const ctx=new Proxy({measureText:()=>({width:40})},{get:(object,key)=>key in object?object[key]:noop,set:(o,k,v)=>(o[k]=v,true)});
const env=vm.createContext({THREE,document:{createElement:()=>({width:0,height:0,getContext:()=>ctx})},Image:class {}});
vm.runInContext(source.slice(source.indexOf('function visibleSceneTargets(')),env);
const renderer={capabilities:{getMaxAnisotropy:()=>1}};
test('full model projection exposes cropping even when its center is still visible',()=>{
  const model=new THREE.Mesh(new THREE.BoxGeometry(4,2,1),new THREE.MeshBasicMaterial());
  model.position.set(2,0,0);
  const camera=new THREE.PerspectiveCamera(34,1,.1,60);camera.position.z=10.6;camera.updateMatrixWorld();
  const center=model.position.clone().project(camera);
  assert.ok(Math.abs(center.x)<1,'center-only check would pass');
  assert.ok(env.projectedSceneBounds(model,camera).maxX>1,'full geometry detects the cut edge');
});
test('compact workflow and pricing surfaces fit entirely at the reported narrow canvas sizes',()=>{
  const original=THREE.TextureLoader.prototype.load;
  THREE.TextureLoader.prototype.load=()=>new THREE.Texture();
  try{
    for(const state of ['workflow','pricing'])for(const width of [351,613,645,995,1200]){
      const artifact=state==='workflow'?env.createWorkflowArtifact(renderer):env.createPricingArtifact(renderer);
      const compact=width<1050;
      env.layoutStoryArtifacts({workflow:state==='workflow'?artifact:{controls:[]},pricing:state==='pricing'?artifact:{controls:[]}},compact);
      const scale=compact?artifact.mobileScale*Math.max(1,Math.min(1.35,width/450)):artifact.desktopScale;
      artifact.group.scale.setScalar(scale);
      const root=new THREE.Group();root.scale.setScalar(state==='workflow'?.92:.90);root.add(artifact.group);
      const camera=new THREE.PerspectiveCamera(34,width/300,.1,60);
      camera.position.set(0,.05,env.sceneCameraDistance(state,camera.aspect));camera.lookAt(0,0,0);camera.updateMatrixWorld();
      for(const angle of [-.1,0,.1]){
        root.rotation.y=angle;root.updateMatrixWorld(true);
        for(const control of artifact.controls){
          const bounds=env.projectedSceneBounds(control.group,camera);
          assert.ok(bounds.minX>-.97&&bounds.maxX<.97,`horizontal crop: ${state} ${width}`);
          assert.ok(bounds.minY>-.94&&bounds.maxY<.94,`vertical crop: ${state} ${width}`);
        }
      }
    }
  }finally{THREE.TextureLoader.prototype.load=original;}
});
