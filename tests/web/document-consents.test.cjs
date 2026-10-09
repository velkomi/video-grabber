const {test}=require('node:test');const assert=require('node:assert/strict');
const {confirmCourseRights}=require('../../src/VideoGrabber.Platform.Api/wwwroot/assets/document-consents.js');
test('concurrent submission performs one action and releases the lock after failure',async()=>{
  const {singleFlight}=require('../../src/VideoGrabber.Platform.Api/wwwroot/assets/document-consents.js');
  let release,calls=0;const pending=new Promise(resolve=>release=resolve);
  const guarded=singleFlight(async()=>{calls++;await pending;throw new Error('retry');});
  const first=guarded();await guarded();assert.equal(calls,1);release();await assert.rejects(first,/retry/);
  await assert.rejects(guarded(),/retry/);assert.equal(calls,2);
});
test('unchecked confirmation does not read documents or create acceptance',async()=>{let called=false;await assert.rejects(confirmCourseRights(async()=>{called=true;},'test',false),/content_rights/);assert.equal(called,false);});
test('acceptance is tied to exact current server document and operation',async()=>{const calls=[];const api=async(path,options)=>{calls.push({path,options});return {documents:[{id:'terms',version:'v2',sha256:'a'.repeat(64)}]};};await confirmCourseRights(api,'operation-id',true);assert.equal(calls.length,2);const body=JSON.parse(calls[1].options.body);assert.equal(body.documentHash,'a'.repeat(64));assert.equal(body.version,'v2');assert.equal(body.intentId,'operation-id');assert.equal(body.purpose,'course_rights');assert.deepEqual(Object.keys(body).sort(),['decision','documentHash','documentId','intentId','purpose','version']);});
test('failed or invalid catalog cannot record acceptance',async()=>{let posts=0;await assert.rejects(confirmCourseRights(async(path)=>{if(path==='/v1/consents')posts++;return{documents:[]};},'id',true),/documents_unavailable/);assert.equal(posts,0);});
test('record failure propagates rather than becoming successful confirmation',async()=>{await assert.rejects(confirmCourseRights(async(path)=>{if(path==='/v1/consents')throw new Error('offline');return{documents:[{id:'terms',version:'v1',sha256:'b'.repeat(64)}]};},'id',true),/offline/);});
