const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js','utf8');
test('recent job pages start with five newest entries and leave the stored history intact',()=>{
 const start=source.indexOf('function recentJobPage(');
 assert.ok(start>=0,'recentJobPage must exist');
 const end=source.indexOf('\nfunction renderJobs()',start);
 const scope={};vm.createContext(scope);vm.runInContext(source.slice(start,end),scope);
 const jobs=Array.from({length:14},(_,i)=>({jobId:i}));
 assert.deepEqual(Array.from(scope.recentJobPage(jobs).map(j=>j.jobId)),[13,12,11,10,9]);
 assert.equal(scope.recentJobPage(jobs,10).length,10);
 assert.equal(scope.recentJobPage(jobs,1000).length,14);
 assert.equal(jobs[0].jobId,0);
 assert.equal(scope.recentJobPage([],5).length,0);
});
