import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import * as THREE from '../../src/VideoGrabber.Platform.Api/wwwroot/web/vendor/three.module.js';

const source = fs.readFileSync(new URL('../../src/VideoGrabber.Platform.Api/wwwroot/web/hero-three.js', import.meta.url), 'utf8');
const helpers = source.slice(source.indexOf('function visibleSceneTargets('), source.indexOf('function createFeatureCard('));
const context = vm.createContext({THREE});
vm.runInContext(helpers, context);
vm.runInContext(source.slice(source.indexOf('function roundedRectShape('),source.indexOf('function makeCardTexture(')),context);
vm.runInContext(source.slice(source.indexOf('function prepareStoryArtifact('),source.indexOf('function makeOrbitMaterial(')),context);

test('shared referral materials retain their opacity when prepared and faded into view',()=>{
  const gift=context.createReferralBonus(); context.prepareStoryArtifact(gift.group); context.setStoryArtifactOpacity(gift.group,1);
  gift.group.traverse(mesh=>{if(mesh.material)assert.equal(mesh.material.opacity,1);});
});

test('referral sculpture connects two play symbols, has bonus tokens and is half the gift size',()=>{
  const gift=context.createReferralBonus(); gift.group.updateMatrixWorld(true);
  const bounds=new THREE.Box3().setFromObject(gift.group); const size=bounds.getSize(new THREE.Vector3());
  assert.ok(size.z>.3, 'real depth');
  assert.ok(Math.max(size.x,size.y,size.z)*gift.desktopScale<2.7,'compact sculpture');
  let plays=0,coins=0;
  gift.group.traverse(mesh=>{if(mesh.userData.referralPlay)plays++;if(mesh.userData.bonusToken)coins++;
    if(mesh.geometry) for(const value of mesh.geometry.attributes.position.array) assert.ok(Number.isFinite(value));});
  assert.equal(plays,2); assert.ok(coins>=2);
  assert.ok(context.sceneCameraDistance('bonus',1)<context.sceneCameraDistance('hero',1));
});

test('hidden meshes and meshes under hidden ancestors cannot trigger scene navigation', () => {
  const root = new THREE.Group();
  const hiddenGroup = new THREE.Group();
  hiddenGroup.visible = false;
  const hiddenChild = new THREE.Mesh(new THREE.BoxGeometry(), new THREE.MeshBasicMaterial());
  hiddenGroup.add(hiddenChild);
  root.add(hiddenGroup);
  const visible = new THREE.Mesh(new THREE.BoxGeometry(), new THREE.MeshBasicMaterial());
  root.add(visible);
  const hiddenLabel = new THREE.Mesh(new THREE.PlaneGeometry(), new THREE.MeshBasicMaterial());
  hiddenLabel.visible = false;
  root.add(hiddenLabel);
  const picked = context.visibleSceneTargets([hiddenChild, visible, hiddenLabel]);
  assert.equal(picked.length, 1);
  assert.equal(picked[0], visible);
});
test('only the active story objects accept navigation during a scene crossfade',()=>{
  const workflow=new THREE.Mesh(new THREE.BoxGeometry(),new THREE.MeshBasicMaterial());
  const pricing=new THREE.Mesh(new THREE.BoxGeometry(),new THREE.MeshBasicMaterial());
  workflow.userData.storyState='workflow'; pricing.userData.storyState='pricing';
  assert.deepEqual([...context.visibleSceneTargets([workflow,pricing],'workflow')],[workflow]);
});
test('fading hero and unclassified meshes cannot consume a workflow click',()=>{
  const hero=new THREE.Mesh(); hero.userData.storyState='hero';
  const unknown=new THREE.Mesh();
  assert.equal(context.visibleSceneTargets([hero,unknown],'workflow').length,0);
});
test('raycasting also activates a workflow mesh outside its HTML caption',()=>{
  const snippet=source.slice(source.indexOf('  const raycastAt ='),source.indexOf('  visual.addEventListener("pointermove"'));
  const env={storyState:'workflow',setRaycastFeature(){},canvas:{getBoundingClientRect:()=>({left:0,top:0,right:300,bottom:200,width:300,height:200})},
    rayPointer:{},raycaster:{setFromCamera(){},intersectObjects:()=>[{object:{userData:{feature:'workflow-url'}}}]},
    visibleSceneTargets:items=>items,pickTargets:[],camera:{}};
  vm.runInNewContext(snippet+'\nresult=raycastAt(150,100);',env);
  assert.equal(env.result,'workflow-url');
});
test('device screens show the entire illustration with normalized UVs and have real chassis depth',()=>{
  const shape=source.slice(source.indexOf('function roundedRectShape('),source.indexOf('function makeCardTexture('));
  const device=source.slice(source.indexOf('function createStudioDevice('),source.indexOf('function createSyncArtifact('));
  vm.runInContext(shape+device,context);
  const model=context.createStudioDevice(2.7,1.7,new THREE.Texture(),true);
  const screen=model.children[1];
  const uv=screen.geometry.attributes.uv;
  for(let i=0;i<uv.count;i++){assert.ok(uv.getX(i)>=0&&uv.getX(i)<=1);assert.ok(uv.getY(i)>=0&&uv.getY(i)<=1);}
  assert.ok(model.userData.deviceDepth>0 && model.userData.deviceDepth<.13,'device chassis must be slim');
  assert.ok(model.children.some(mesh=>mesh.isInstancedMesh),'keyboard has physical keys');
});
test('trackpad is separated from the laptop deck instead of sharing its surface',()=>{
  const model=context.createStudioDevice(2.7,1.7,new THREE.Texture(),true);
  model.updateMatrixWorld(true);
  const deck=model.children.find(mesh=>mesh.name==='chassis-deck') || model.children[3];
  const pad=model.children.find(mesh=>mesh.name==='trackpad') || model.children[5];
  const deckBox=new THREE.Box3().setFromObject(deck), padBox=new THREE.Box3().setFromObject(pad);
  assert.ok(padBox.min.y>deckBox.max.y+.006,'trackpad intersects or is coplanar with the deck');
});

test('the optical sculpture has outward-facing surface normals on both caps', () => {
  const sculpture = context.createStudioPlayObject(new THREE.MeshPhysicalMaterial());
  for (const mesh of sculpture.children) {
    mesh.geometry.computeBoundingBox();
    const box = mesh.geometry.boundingBox;
    const center = (box.min.z+box.max.z)/2;
    const half = (box.max.z-box.min.z)/2;
    const positions = mesh.geometry.attributes.position;
    const normals = mesh.geometry.attributes.normal;
    for (let i=0; i<positions.count; i++) {
      if (positions.getZ(i)>center+half*.9) assert.ok(normals.getZ(i)>.5,'front cap normal points inward');
      if (positions.getZ(i)<center-half*.9) assert.ok(normals.getZ(i)<-.5,'back cap normal points inward');
    }
  }
});

test('the rounded glass play sculpture has real depth and stays inside the viewport during pointer motion', () => {
  for (const [width, height] of [[590,460], [351,320], [300,220]]) {
    const sculpture = context.createStudioPlayObject(new THREE.MeshPhysicalMaterial());
    assert.equal(sculpture.children.length, 2);
    for (const mesh of sculpture.children) {
      mesh.geometry.computeBoundingBox();
      const bounds = mesh.geometry.boundingBox;
      assert.ok(bounds.max.z - bounds.min.z > .2);
      assert.ok(mesh.geometry.attributes.position.count > 600);
    }
    const parent = new THREE.Group();
    parent.add(sculpture);
    const camera = new THREE.PerspectiveCamera(34, width / height, .1, 60);
    camera.position.set(0, .05, Math.max(10.6, 12.2 / camera.aspect));
    camera.lookAt(0,0,0);
    camera.updateMatrixWorld();
    for (const tilt of [-.18, 0, .18]) {
      parent.rotation.y = tilt;
      parent.updateMatrixWorld(true);
      // Project the surface vertices, not empty corners of a rotated world-axis box.
      for (const mesh of sculpture.children) {
        const positions = mesh.geometry.attributes.position;
        for (let i = 0; i < positions.count; i++) {
          const screen = new THREE.Vector3().fromBufferAttribute(positions,i).applyMatrix4(mesh.matrixWorld).project(camera);
          assert.ok(Math.abs(screen.x) < .96, `horizontal clipping at ${width}x${height}`);
          assert.ok(Math.abs(screen.y) < .96, `vertical clipping at ${width}x${height}`);
        }
      }
    }
  }
});
